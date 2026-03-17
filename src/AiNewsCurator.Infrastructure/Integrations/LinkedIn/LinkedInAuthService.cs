using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using AiNewsCurator.Application.DTOs;
using AiNewsCurator.Domain.Entities;
using AiNewsCurator.Domain.Interfaces;
using Microsoft.Extensions.Options;

namespace AiNewsCurator.Infrastructure.Integrations.LinkedIn;

public sealed class LinkedInAuthService : ILinkedInAuthService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ISettingsRepository _settingsRepository;
    private readonly AppOptions _options;

    public LinkedInAuthService(
        IHttpClientFactory httpClientFactory,
        ISettingsRepository settingsRepository,
        IOptions<AppOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _settingsRepository = settingsRepository;
        _options = options.Value;
    }

    public async Task<LinkedInAuthState> GetStatusAsync(CancellationToken cancellationToken)
    {
        var accessToken = await _settingsRepository.GetAsync(LinkedInSettingsKeys.AccessToken, cancellationToken);
        var memberUrn = await _settingsRepository.GetAsync(LinkedInSettingsKeys.MemberUrn, cancellationToken);
        var memberName = await _settingsRepository.GetAsync(LinkedInSettingsKeys.MemberName, cancellationToken);
        var memberEmail = await _settingsRepository.GetAsync(LinkedInSettingsKeys.MemberEmail, cancellationToken);
        var tokenUpdatedAt = await _settingsRepository.GetAsync(LinkedInSettingsKeys.TokenUpdatedAt, cancellationToken);

        return new LinkedInAuthState
        {
            IsConfigured =
                !string.IsNullOrWhiteSpace(_options.LinkedInClientId) &&
                !string.IsNullOrWhiteSpace(_options.LinkedInClientSecret) &&
                !string.IsNullOrWhiteSpace(_options.LinkedInRedirectUri),
            HasAccessToken = accessToken is not null,
            HasMemberUrn = memberUrn is not null,
            MemberName = memberName?.Value,
            MemberEmail = memberEmail?.Value,
            TokenUpdatedAt = tokenUpdatedAt is null ? null : DateTimeOffset.Parse(tokenUpdatedAt.Value)
        };
    }

    public async Task<string> CreateAuthorizationUrlAsync(CancellationToken cancellationToken)
    {
        EnsureConfigured();

        var state = Convert.ToHexString(Guid.NewGuid().ToByteArray()).ToLowerInvariant();
        await _settingsRepository.UpsertAsync(LinkedInSettingsKeys.OAuthState, state, cancellationToken);
        await _settingsRepository.UpsertAsync(LinkedInSettingsKeys.OAuthStateUpdatedAt, DateTimeOffset.UtcNow.ToString("O"), cancellationToken);

        var scope = Uri.EscapeDataString("openid profile email w_member_social");
        var redirectUri = Uri.EscapeDataString(_options.LinkedInRedirectUri!);

        return $"https://www.linkedin.com/oauth/v2/authorization?response_type=code&client_id={Uri.EscapeDataString(_options.LinkedInClientId!)}&redirect_uri={redirectUri}&state={state}&scope={scope}";
    }

    public async Task<LinkedInAuthCallbackResult> HandleCallbackAsync(string code, string state, string? error, string? errorDescription, CancellationToken cancellationToken)
    {
        EnsureConfigured();

        if (!string.IsNullOrWhiteSpace(error))
        {
            return new LinkedInAuthCallbackResult
            {
                Success = false,
                Message = $"LinkedIn returned an error: {error}. {errorDescription}".Trim()
            };
        }

        var savedState = await _settingsRepository.GetAsync(LinkedInSettingsKeys.OAuthState, cancellationToken);
        if (savedState is null || !string.Equals(savedState.Value, state, StringComparison.Ordinal))
        {
            return new LinkedInAuthCallbackResult
            {
                Success = false,
                Message = "Invalid OAuth state."
            };
        }

        var tokenResponse = await ExchangeCodeAsync(code, cancellationToken);
        var userInfo = await FetchUserInfoAsync(tokenResponse.AccessToken, cancellationToken);
        var memberUrn = $"urn:li:person:{userInfo.Sub}";

        await _settingsRepository.UpsertAsync(LinkedInSettingsKeys.AccessToken, tokenResponse.AccessToken, cancellationToken);
        if (!string.IsNullOrWhiteSpace(tokenResponse.RefreshToken))
        {
            await _settingsRepository.UpsertAsync(LinkedInSettingsKeys.RefreshToken, tokenResponse.RefreshToken, cancellationToken);
        }

        await _settingsRepository.UpsertAsync(LinkedInSettingsKeys.MemberUrn, memberUrn, cancellationToken);
        await _settingsRepository.UpsertAsync(LinkedInSettingsKeys.MemberName, userInfo.Name ?? string.Empty, cancellationToken);
        await _settingsRepository.UpsertAsync(LinkedInSettingsKeys.MemberEmail, userInfo.Email ?? string.Empty, cancellationToken);
        await _settingsRepository.UpsertAsync(LinkedInSettingsKeys.TokenUpdatedAt, DateTimeOffset.UtcNow.ToString("O"), cancellationToken);

        return new LinkedInAuthCallbackResult
        {
            Success = true,
            Message = "LinkedIn authorization completed successfully.",
            MemberName = userInfo.Name,
            MemberEmail = userInfo.Email
        };
    }

    private async Task<LinkedInTokenResponse> ExchangeCodeAsync(string code, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient("linkedin-auth");
        using var response = await client.PostAsync(
            "https://www.linkedin.com/oauth/v2/accessToken",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["client_id"] = _options.LinkedInClientId!,
                ["client_secret"] = _options.LinkedInClientSecret!,
                ["redirect_uri"] = _options.LinkedInRedirectUri!
            }),
            cancellationToken);

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"LinkedIn token exchange failed with status {(int)response.StatusCode}: {body}");
        }

        return JsonSerializer.Deserialize<LinkedInTokenResponse>(body, JsonOptions()) ??
               throw new InvalidOperationException("Unable to deserialize LinkedIn token response.");
    }

    private async Task<LinkedInUserInfoResponse> FetchUserInfoAsync(string accessToken, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient("linkedin-auth");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await client.GetAsync("https://api.linkedin.com/v2/userinfo", cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"LinkedIn user info request failed with status {(int)response.StatusCode}: {body}");
        }

        var userInfo = JsonSerializer.Deserialize<LinkedInUserInfoResponse>(body, JsonOptions()) ??
                       throw new InvalidOperationException("Unable to deserialize LinkedIn user info response.");

        if (string.IsNullOrWhiteSpace(userInfo.Sub))
        {
            throw new InvalidOperationException("LinkedIn user info response did not include a user subject.");
        }

        return userInfo;
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(_options.LinkedInClientId) ||
            string.IsNullOrWhiteSpace(_options.LinkedInClientSecret) ||
            string.IsNullOrWhiteSpace(_options.LinkedInRedirectUri))
        {
            throw new InvalidOperationException("LinkedIn OAuth is not fully configured.");
        }
    }

    private static JsonSerializerOptions JsonOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    private sealed class LinkedInTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; set; }
    }

    private sealed class LinkedInUserInfoResponse
    {
        [JsonPropertyName("sub")]
        public string Sub { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("email")]
        public string? Email { get; set; }
    }
}

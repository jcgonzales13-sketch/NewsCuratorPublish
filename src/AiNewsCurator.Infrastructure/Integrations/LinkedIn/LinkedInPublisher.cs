using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AiNewsCurator.Application.DTOs;
using AiNewsCurator.Domain.Entities;
using AiNewsCurator.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AiNewsCurator.Infrastructure.Integrations.LinkedIn;

public sealed class LinkedInPublisher : ILinkedInPublisher
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ISettingsRepository _settingsRepository;
    private readonly AppOptions _options;
    private readonly ILogger<LinkedInPublisher> _logger;

    public LinkedInPublisher(
        IHttpClientFactory httpClientFactory,
        ISettingsRepository settingsRepository,
        IOptions<AppOptions> options,
        ILogger<LinkedInPublisher> logger)
    {
        _httpClientFactory = httpClientFactory;
        _settingsRepository = settingsRepository;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<LinkedInPublishResult> PublishTextPostAsync(PostDraft draft, CancellationToken cancellationToken)
    {
        var validation = await ValidateCredentialsAsync(cancellationToken);
        if (!validation.Success)
        {
            return new LinkedInPublishResult
            {
                Success = false,
                ErrorMessage = validation.ErrorMessage,
                RequestPayload = string.Empty,
                ResponsePayload = string.Empty
            };
        }

        var credentials = await GetCredentialsAsync(cancellationToken);
        var payloadObject = new
        {
            author = credentials.MemberUrn,
            lifecycleState = "PUBLISHED",
            specificContent = new
            {
                comLinkedinUgcShareContent = new
                {
                    shareCommentary = new
                    {
                        text = draft.PostText
                    },
                    shareMediaCategory = "NONE"
                }
            },
            visibility = new
            {
                comLinkedinUgcMemberNetworkVisibility = "PUBLIC"
            }
        };

        var payload = JsonSerializer.Serialize(payloadObject);
        var client = _httpClientFactory.CreateClient("linkedin");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", credentials.AccessToken);

        using var response = await client.PostAsync(
            "https://api.linkedin.com/v2/ugcPosts",
            new StringContent(payload, Encoding.UTF8, "application/json"),
            cancellationToken);

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("LinkedIn publication failed with status {StatusCode}", response.StatusCode);
            return new LinkedInPublishResult
            {
                Success = false,
                RequestPayload = payload,
                ResponsePayload = responseBody,
                ErrorMessage = $"LinkedIn returned {(int)response.StatusCode}."
            };
        }

        return new LinkedInPublishResult
        {
            Success = true,
            PlatformPostId = response.Headers.Location?.ToString() ?? Guid.NewGuid().ToString("N"),
            RequestPayload = payload,
            ResponsePayload = responseBody
        };
    }

    public Task<OperationResult> ValidateCredentialsAsync(CancellationToken cancellationToken)
    {
        return ValidateCredentialsInternalAsync(cancellationToken);
    }

    public Task<OperationResult> RefreshAccessAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(OperationResult.Failed("Automatic token refresh is not implemented in the MVP."));
    }

    private async Task<OperationResult> ValidateCredentialsInternalAsync(CancellationToken cancellationToken)
    {
        var credentials = await GetCredentialsAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(credentials.AccessToken) || string.IsNullOrWhiteSpace(credentials.MemberUrn))
        {
            return OperationResult.Failed("LinkedIn credentials are not configured.");
        }

        var client = _httpClientFactory.CreateClient("linkedin-auth");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", credentials.AccessToken);
        using var response = await client.GetAsync("https://api.linkedin.com/v2/userinfo", cancellationToken);
        return response.IsSuccessStatusCode
            ? OperationResult.Ok()
            : OperationResult.Failed($"LinkedIn credential validation failed with status {(int)response.StatusCode}.");
    }

    private async Task<LinkedInCredentials> GetCredentialsAsync(CancellationToken cancellationToken)
    {
        var tokenSetting = await _settingsRepository.GetAsync(LinkedInSettingsKeys.AccessToken, cancellationToken);
        var memberUrnSetting = await _settingsRepository.GetAsync(LinkedInSettingsKeys.MemberUrn, cancellationToken);

        return new LinkedInCredentials
        {
            AccessToken = tokenSetting?.Value ?? _options.LinkedInAccessToken,
            MemberUrn = memberUrnSetting?.Value ?? _options.LinkedInMemberUrn
        };
    }

    private sealed class LinkedInCredentials
    {
        public string? AccessToken { get; init; }
        public string? MemberUrn { get; init; }
    }
}

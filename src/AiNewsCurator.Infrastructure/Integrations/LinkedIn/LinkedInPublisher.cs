using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
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
    private readonly INewsItemRepository _newsItemRepository;
    private readonly AppOptions _options;
    private readonly ILogger<LinkedInPublisher> _logger;

    public LinkedInPublisher(
        IHttpClientFactory httpClientFactory,
        ISettingsRepository settingsRepository,
        INewsItemRepository newsItemRepository,
        IOptions<AppOptions> options,
        ILogger<LinkedInPublisher> logger)
    {
        _httpClientFactory = httpClientFactory;
        _settingsRepository = settingsRepository;
        _newsItemRepository = newsItemRepository;
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
        var newsItem = await _newsItemRepository.GetByIdAsync(draft.NewsItemId, cancellationToken);
        var imageAssetUrn = await TryUploadImageAsync(credentials, newsItem?.ImageUrl, cancellationToken);

        if (!string.IsNullOrWhiteSpace(newsItem?.ImageUrl) && string.IsNullOrWhiteSpace(imageAssetUrn))
        {
            _logger.LogWarning("LinkedIn image upload failed for NewsItemId={NewsItemId}. Falling back to text-only post.", draft.NewsItemId);
        }

        return await PublishUgcPostAsync(credentials, draft, imageAssetUrn, cancellationToken);
    }

    public Task<OperationResult> ValidateCredentialsAsync(CancellationToken cancellationToken)
    {
        return ValidateCredentialsInternalAsync(cancellationToken);
    }

    public Task<OperationResult> RefreshAccessAsync(CancellationToken cancellationToken)
    {
        return RefreshAccessInternalAsync(cancellationToken);
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
        if (response.IsSuccessStatusCode)
        {
            return OperationResult.Ok();
        }

        if (response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
        {
            var refreshResult = await RefreshAccessInternalAsync(cancellationToken);
            if (!refreshResult.Success)
            {
                return refreshResult;
            }

            credentials = await GetCredentialsAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(credentials.AccessToken))
            {
                return OperationResult.Failed("LinkedIn access token refresh did not return a usable access token.");
            }

            client = _httpClientFactory.CreateClient("linkedin-auth");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", credentials.AccessToken);
            using var retryResponse = await client.GetAsync("https://api.linkedin.com/v2/userinfo", cancellationToken);
            if (retryResponse.IsSuccessStatusCode)
            {
                return OperationResult.Ok();
            }

            return OperationResult.Failed($"LinkedIn credential validation failed after refresh with status {(int)retryResponse.StatusCode}.");
        }

        return OperationResult.Failed($"LinkedIn credential validation failed with status {(int)response.StatusCode}.");
    }

    private async Task<LinkedInCredentials> GetCredentialsAsync(CancellationToken cancellationToken)
    {
        var tokenSetting = await _settingsRepository.GetAsync(LinkedInSettingsKeys.AccessToken, cancellationToken);
        var refreshTokenSetting = await _settingsRepository.GetAsync(LinkedInSettingsKeys.RefreshToken, cancellationToken);
        var memberUrnSetting = await _settingsRepository.GetAsync(LinkedInSettingsKeys.MemberUrn, cancellationToken);

        return new LinkedInCredentials
        {
            AccessToken = tokenSetting?.Value ?? _options.LinkedInAccessToken,
            RefreshToken = refreshTokenSetting?.Value,
            MemberUrn = memberUrnSetting?.Value ?? _options.LinkedInMemberUrn
        };
    }

    private async Task<OperationResult> RefreshAccessInternalAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.LinkedInClientId) || string.IsNullOrWhiteSpace(_options.LinkedInClientSecret))
        {
            return OperationResult.Failed("LinkedIn OAuth client credentials are not configured for token refresh.");
        }

        var credentials = await GetCredentialsAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(credentials.RefreshToken))
        {
            return OperationResult.Failed("LinkedIn refresh token is not available.");
        }

        var client = _httpClientFactory.CreateClient("linkedin-auth");
        using var response = await client.PostAsync(
            "https://www.linkedin.com/oauth/v2/accessToken",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = credentials.RefreshToken,
                ["client_id"] = _options.LinkedInClientId,
                ["client_secret"] = _options.LinkedInClientSecret
            }),
            cancellationToken);

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "LinkedIn token refresh failed with status {StatusCode}. Response: {ResponseBody}",
                response.StatusCode,
                body);
            return OperationResult.Failed($"LinkedIn token refresh failed with status {(int)response.StatusCode}: {body}");
        }

        var tokenResponse = JsonSerializer.Deserialize<LinkedInRefreshTokenResponse>(body, JsonSerializerOptions) ??
                            throw new InvalidOperationException("Unable to deserialize LinkedIn refresh token response.");

        if (string.IsNullOrWhiteSpace(tokenResponse.AccessToken))
        {
            return OperationResult.Failed("LinkedIn token refresh response did not include an access token.");
        }

        await _settingsRepository.UpsertAsync(LinkedInSettingsKeys.AccessToken, tokenResponse.AccessToken, cancellationToken);
        if (!string.IsNullOrWhiteSpace(tokenResponse.RefreshToken))
        {
            await _settingsRepository.UpsertAsync(LinkedInSettingsKeys.RefreshToken, tokenResponse.RefreshToken, cancellationToken);
        }

        await _settingsRepository.UpsertAsync(LinkedInSettingsKeys.TokenUpdatedAt, DateTimeOffset.UtcNow.ToString("O"), cancellationToken);
        return OperationResult.Ok();
    }

    private async Task<LinkedInPublishResult> PublishUgcPostAsync(
        LinkedInCredentials credentials,
        PostDraft draft,
        string? imageAssetUrn,
        CancellationToken cancellationToken)
    {
        var payloadObject = BuildPostPayload(credentials.MemberUrn!, draft, imageAssetUrn);
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
            _logger.LogWarning(
                "LinkedIn publication failed with status {StatusCode}. Response: {ResponseBody}",
                response.StatusCode,
                responseBody);
            return new LinkedInPublishResult
            {
                Success = false,
                RequestPayload = payload,
                ResponsePayload = responseBody,
                ErrorMessage = $"LinkedIn returned {(int)response.StatusCode}: {responseBody}"
            };
        }

        return new LinkedInPublishResult
        {
            Success = true,
            PlatformPostId = response.Headers.TryGetValues("X-RestLi-Id", out var ids)
                ? ids.FirstOrDefault() ?? Guid.NewGuid().ToString("N")
                : response.Headers.Location?.ToString() ?? Guid.NewGuid().ToString("N"),
            RequestPayload = payload,
            ResponsePayload = responseBody
        };
    }

    private static object BuildPostPayload(string memberUrn, PostDraft draft, string? imageAssetUrn)
    {
        object shareContent = string.IsNullOrWhiteSpace(imageAssetUrn)
            ? new
            {
                shareCommentary = new
                {
                    text = draft.PostText
                },
                shareMediaCategory = "NONE"
            }
            : new
            {
                shareCommentary = new
                {
                    text = draft.PostText
                },
                shareMediaCategory = "IMAGE",
                media = new[]
                {
                    new
                    {
                        status = "READY",
                        media = imageAssetUrn,
                        title = new
                        {
                            text = draft.TitleSuggestion ?? "AI News Curator"
                        }
                    }
                }
            };

        return new
        {
            author = memberUrn,
            lifecycleState = "PUBLISHED",
            specificContent = new Dictionary<string, object>
            {
                ["com.linkedin.ugc.ShareContent"] = shareContent
            },
            visibility = new Dictionary<string, string>
            {
                ["com.linkedin.ugc.MemberNetworkVisibility"] = "PUBLIC"
            }
        };
    }

    private async Task<string?> TryUploadImageAsync(
        LinkedInCredentials credentials,
        string? imageUrl,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            return null;
        }

        try
        {
            var imageBytes = await DownloadImageAsync(imageUrl, cancellationToken);
            if (imageBytes is null || imageBytes.Length == 0)
            {
                return null;
            }

            var registration = await RegisterImageUploadAsync(credentials, cancellationToken);
            var uploaded = await UploadImageBinaryAsync(credentials.AccessToken!, registration.UploadUrl, imageBytes, cancellationToken);
            return uploaded ? registration.Asset : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LinkedIn image upload flow failed for image {ImageUrl}", imageUrl);
            return null;
        }
    }

    private async Task<byte[]?> DownloadImageAsync(string imageUrl, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient("rss");
        using var response = await client.GetAsync(imageUrl, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if (mediaType is not null && !mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }

    private async Task<ImageUploadRegistration> RegisterImageUploadAsync(LinkedInCredentials credentials, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient("linkedin");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", credentials.AccessToken);

        var payload = JsonSerializer.Serialize(new
        {
            registerUploadRequest = new
            {
                recipes = new[] { "urn:li:digitalmediaRecipe:feedshare-image" },
                owner = credentials.MemberUrn,
                serviceRelationships = new[]
                {
                    new
                    {
                        relationshipType = "OWNER",
                        identifier = "urn:li:userGeneratedContent"
                    }
                }
            }
        });

        using var response = await client.PostAsync(
            "https://api.linkedin.com/v2/assets?action=registerUpload",
            new StringContent(payload, Encoding.UTF8, "application/json"),
            cancellationToken);

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"LinkedIn image register upload failed with status {(int)response.StatusCode}: {body}");
        }

        using var document = JsonDocument.Parse(body);
        var value = document.RootElement.GetProperty("value");
        var uploadUrl = value
            .GetProperty("uploadMechanism")
            .GetProperty("com.linkedin.digitalmedia.uploading.MediaUploadHttpRequest")
            .GetProperty("uploadUrl")
            .GetString();
        var asset = value.GetProperty("asset").GetString();

        if (string.IsNullOrWhiteSpace(uploadUrl) || string.IsNullOrWhiteSpace(asset))
        {
            throw new InvalidOperationException("LinkedIn image upload registration did not return uploadUrl and asset.");
        }

        return new ImageUploadRegistration
        {
            UploadUrl = uploadUrl,
            Asset = asset
        };
    }

    private async Task<bool> UploadImageBinaryAsync(string accessToken, string uploadUrl, byte[] imageBytes, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, uploadUrl)
        {
            Content = new ByteArrayContent(imageBytes)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

        using var client = new HttpClient();
        using var response = await client.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return true;
        }

        using var putRequest = new HttpRequestMessage(HttpMethod.Put, uploadUrl)
        {
            Content = new ByteArrayContent(imageBytes)
        };
        putRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        putRequest.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

        using var putResponse = await client.SendAsync(putRequest, cancellationToken);
        return putResponse.IsSuccessStatusCode;
    }

    private sealed class ImageUploadRegistration
    {
        public required string UploadUrl { get; init; }
        public required string Asset { get; init; }
    }

    private sealed class LinkedInCredentials
    {
        public string? AccessToken { get; init; }
        public string? RefreshToken { get; init; }
        public string? MemberUrn { get; init; }
    }

    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private sealed class LinkedInRefreshTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; set; }
    }
}

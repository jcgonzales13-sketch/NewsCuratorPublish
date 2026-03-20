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
        public string? MemberUrn { get; init; }
    }
}

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
    private readonly AppOptions _options;
    private readonly ILogger<LinkedInPublisher> _logger;

    public LinkedInPublisher(IHttpClientFactory httpClientFactory, IOptions<AppOptions> options, ILogger<LinkedInPublisher> logger)
    {
        _httpClientFactory = httpClientFactory;
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

        var payloadObject = new
        {
            author = _options.LinkedInMemberUrn,
            commentary = draft.PostText,
            visibility = "PUBLIC",
            distribution = new
            {
                feedDistribution = "MAIN_FEED",
                targetEntities = Array.Empty<string>(),
                thirdPartyDistributionChannels = Array.Empty<string>()
            },
            lifecycleState = "PUBLISHED",
            isReshareDisabledByAuthor = false
        };

        var payload = JsonSerializer.Serialize(payloadObject);
        var client = _httpClientFactory.CreateClient("linkedin");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.LinkedInAccessToken);

        using var response = await client.PostAsync(
            "https://api.linkedin.com/rest/posts",
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
        if (string.IsNullOrWhiteSpace(_options.LinkedInAccessToken) || string.IsNullOrWhiteSpace(_options.LinkedInMemberUrn))
        {
            return Task.FromResult(OperationResult.Failed("LinkedIn credentials are not configured."));
        }

        return Task.FromResult(OperationResult.Ok());
    }

    public Task<OperationResult> RefreshAccessAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(OperationResult.Failed("Automatic token refresh is not implemented in the MVP."));
    }
}

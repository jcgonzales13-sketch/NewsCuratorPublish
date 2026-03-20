using AiNewsCurator.Domain.Entities;

namespace AiNewsCurator.Api.Models.Operations;

public sealed class OperationsDraftViewModel
{
    public required PostDraft Draft { get; init; }
    public NewsItem? NewsItem { get; init; }
    public string? SourceName { get; init; }
    public Publication? LatestPublication { get; init; }
    public string? PostImageUrl => NewsItem?.ImageUrl;
    public string? PostImageOrigin => NewsItem?.ImageOrigin;
    public bool HasPostImage => !string.IsNullOrWhiteSpace(PostImageUrl);

    public bool PublishedWithImage =>
        LatestPublication?.RequestPayload?.Contains("\"shareMediaCategory\":\"IMAGE\"", StringComparison.OrdinalIgnoreCase) == true;
}

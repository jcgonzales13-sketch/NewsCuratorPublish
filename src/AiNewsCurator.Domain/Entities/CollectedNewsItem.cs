namespace AiNewsCurator.Domain.Entities;

public sealed class CollectedNewsItem
{
    public string? ExternalId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public string CanonicalUrl { get; init; } = string.Empty;
    public string? Author { get; init; }
    public DateTimeOffset PublishedAt { get; init; }
    public string Language { get; init; } = "en";
    public string? RawSummary { get; init; }
    public string? RawContent { get; init; }
}

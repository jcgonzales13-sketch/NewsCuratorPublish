using AiNewsCurator.Domain.Enums;

namespace AiNewsCurator.Domain.Entities;

public sealed class NewsItem
{
    public long Id { get; set; }
    public long SourceId { get; set; }
    public string? ExternalId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string CanonicalUrl { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string? ImageOrigin { get; set; }
    public string? Author { get; set; }
    public DateTimeOffset PublishedAt { get; set; }
    public string Language { get; set; } = "en";
    public string? RawSummary { get; set; }
    public string? RawContent { get; set; }
    public string ContentHash { get; set; } = string.Empty;
    public string TitleHash { get; set; } = string.Empty;
    public NewsItemStatus Status { get; set; } = NewsItemStatus.Collected;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

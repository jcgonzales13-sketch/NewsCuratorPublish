using AiNewsCurator.Domain.Enums;

namespace AiNewsCurator.Domain.Entities;

public sealed class PostDraft
{
    public long Id { get; set; }
    public long NewsItemId { get; set; }
    public string? TitleSuggestion { get; set; }
    public string PostText { get; set; } = string.Empty;
    public string Tone { get; set; } = "Professional";
    public string? Cta { get; set; }
    public DraftStatus Status { get; set; } = DraftStatus.Generated;
    public string ValidationErrorsJson { get; set; } = "[]";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public string? ApprovedBy { get; set; }
}

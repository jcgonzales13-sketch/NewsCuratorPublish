using AiNewsCurator.Domain.Enums;

namespace AiNewsCurator.Domain.Entities;

public sealed class Publication
{
    public long Id { get; set; }
    public long PostDraftId { get; set; }
    public string Platform { get; set; } = "LinkedIn";
    public string? PlatformPostId { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public string RequestPayload { get; set; } = string.Empty;
    public string ResponsePayload { get; set; } = string.Empty;
    public PublicationStatus Status { get; set; } = PublicationStatus.Pending;
    public string? ErrorMessage { get; set; }
}

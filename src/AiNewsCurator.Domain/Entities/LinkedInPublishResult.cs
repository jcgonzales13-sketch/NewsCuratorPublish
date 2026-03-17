namespace AiNewsCurator.Domain.Entities;

public sealed class LinkedInPublishResult
{
    public bool Success { get; init; }
    public string? PlatformPostId { get; init; }
    public string RequestPayload { get; init; } = string.Empty;
    public string ResponsePayload { get; init; } = string.Empty;
    public string? ErrorMessage { get; init; }
}

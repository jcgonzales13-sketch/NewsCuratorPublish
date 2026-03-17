namespace AiNewsCurator.Domain.Entities;

public sealed class LinkedInAuthCallbackResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public string? MemberName { get; init; }
    public string? MemberEmail { get; init; }
}

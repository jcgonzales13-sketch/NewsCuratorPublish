namespace AiNewsCurator.Domain.Entities;

public sealed class LinkedInAuthState
{
    public bool IsConfigured { get; init; }
    public bool HasAccessToken { get; init; }
    public bool HasMemberUrn { get; init; }
    public string? AuthorizationUrl { get; init; }
    public string? MemberName { get; init; }
    public string? MemberEmail { get; init; }
    public DateTimeOffset? TokenUpdatedAt { get; init; }
}

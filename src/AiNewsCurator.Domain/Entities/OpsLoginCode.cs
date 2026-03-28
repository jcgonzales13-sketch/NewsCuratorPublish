namespace AiNewsCurator.Domain.Entities;

public sealed class OpsLoginCode
{
    public long Id { get; set; }
    public long OpsUserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string CodeHash { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset? UsedAtUtc { get; set; }
    public DateTimeOffset? InvalidatedAtUtc { get; set; }
    public string? RequestIp { get; set; }
    public string? RequestUserAgent { get; set; }
    public string? ConsumeIp { get; set; }
    public string? ConsumeUserAgent { get; set; }
    public int AttemptCount { get; set; }
}

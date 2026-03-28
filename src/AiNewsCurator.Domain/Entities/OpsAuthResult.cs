namespace AiNewsCurator.Domain.Entities;

public sealed class OpsAuthResult
{
    public bool Success { get; set; }
    public bool IsRateLimited { get; set; }
    public bool IsSystemError { get; set; }
    public string? ErrorMessage { get; set; }
    public OpsUser? User { get; set; }
}

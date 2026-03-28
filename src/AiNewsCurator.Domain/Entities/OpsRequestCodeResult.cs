namespace AiNewsCurator.Domain.Entities;

public sealed class OpsRequestCodeResult
{
    public bool Accepted { get; set; }
    public bool IsRateLimited { get; set; }
    public bool IsSystemError { get; set; }
    public string Message { get; set; } = "If the email is authorized, a login code has been sent.";
}

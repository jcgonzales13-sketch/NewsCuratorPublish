using AiNewsCurator.Domain.Entities;

namespace AiNewsCurator.Domain.Interfaces;

public interface IOpsAuthService
{
    Task<OpsRequestCodeResult> RequestCodeAsync(string email, string? ipAddress, string? userAgent, CancellationToken cancellationToken);
    Task<OpsAuthResult> VerifyCodeAsync(string email, string code, string? ipAddress, string? userAgent, CancellationToken cancellationToken);
}

using AiNewsCurator.Domain.Entities;

namespace AiNewsCurator.Domain.Interfaces;

public interface IOpsLoginCodeRepository
{
    Task InvalidateActiveCodesAsync(long opsUserId, CancellationToken cancellationToken);
    Task AddAsync(OpsLoginCode code, CancellationToken cancellationToken);
    Task<OpsLoginCode?> GetLatestActiveByEmailAsync(string normalizedEmail, CancellationToken cancellationToken);
    Task UpdateAsync(OpsLoginCode code, CancellationToken cancellationToken);
    Task<int> CountRecentRequestsByEmailAsync(string normalizedEmail, DateTimeOffset sinceUtc, CancellationToken cancellationToken);
    Task<int> CountRecentRequestsByIpAsync(string ipAddress, DateTimeOffset sinceUtc, CancellationToken cancellationToken);
    Task<int> CountRecentVerificationAttemptsByIpAsync(string ipAddress, DateTimeOffset sinceUtc, CancellationToken cancellationToken);
}

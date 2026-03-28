using AiNewsCurator.Domain.Entities;

namespace AiNewsCurator.Domain.Interfaces;

public interface IOpsUserRepository
{
    Task<OpsUser?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken);
    Task<OpsUser?> FindByIdAsync(long id, CancellationToken cancellationToken);
    Task AddAsync(OpsUser user, CancellationToken cancellationToken);
    Task UpdateAsync(OpsUser user, CancellationToken cancellationToken);
}

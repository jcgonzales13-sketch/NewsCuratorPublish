using AiNewsCurator.Domain.Entities;

namespace AiNewsCurator.Domain.Interfaces;

public interface ISourceRepository
{
    Task<IReadOnlyList<Source>> GetActiveAsync(CancellationToken cancellationToken);
    Task SeedDefaultsAsync(CancellationToken cancellationToken);
}

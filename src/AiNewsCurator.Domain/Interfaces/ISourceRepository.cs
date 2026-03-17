using AiNewsCurator.Domain.Entities;

namespace AiNewsCurator.Domain.Interfaces;

public interface ISourceRepository
{
    Task<IReadOnlyList<Source>> GetAllAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<Source>> GetActiveAsync(CancellationToken cancellationToken);
    Task<Source?> GetByIdAsync(long id, CancellationToken cancellationToken);
    Task<long> InsertAsync(Source source, CancellationToken cancellationToken);
    Task UpdateAsync(Source source, CancellationToken cancellationToken);
    Task SeedDefaultsAsync(CancellationToken cancellationToken);
}

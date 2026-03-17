using AiNewsCurator.Domain.Entities;

namespace AiNewsCurator.Domain.Interfaces;

public interface ICurationResultRepository
{
    Task<long> InsertAsync(CurationResult result, CancellationToken cancellationToken);
    Task<CurationResult?> GetLatestByNewsItemIdAsync(long newsItemId, CancellationToken cancellationToken);
}

using AiNewsCurator.Domain.Entities;

namespace AiNewsCurator.Domain.Interfaces;

public interface IPublicationRepository
{
    Task<long> InsertAsync(Publication publication, CancellationToken cancellationToken);
}

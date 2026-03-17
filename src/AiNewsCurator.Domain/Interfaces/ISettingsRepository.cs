using AiNewsCurator.Domain.Entities;

namespace AiNewsCurator.Domain.Interfaces;

public interface ISettingsRepository
{
    Task<Setting?> GetAsync(string key, CancellationToken cancellationToken);
    Task UpsertAsync(string key, string value, CancellationToken cancellationToken);
}

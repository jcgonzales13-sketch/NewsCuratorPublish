using AiNewsCurator.Domain.Entities;

namespace AiNewsCurator.Domain.Interfaces;

public interface INewsCollector
{
    bool CanHandle(Source source);
    Task<IReadOnlyList<CollectedNewsItem>> CollectAsync(Source source, CancellationToken cancellationToken);
}

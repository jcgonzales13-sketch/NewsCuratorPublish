using AiNewsCurator.Domain.Entities;
using AiNewsCurator.Domain.Enums;

namespace AiNewsCurator.Domain.Interfaces;

public interface INewsItemRepository
{
    Task<NewsItem?> GetByIdAsync(long id, CancellationToken cancellationToken);
    Task<NewsItem?> GetByCanonicalUrlAsync(string canonicalUrl, CancellationToken cancellationToken);
    Task<NewsItem?> GetPublishedByCanonicalUrlAsync(string canonicalUrl, CancellationToken cancellationToken);
    Task<bool> ExistsRecentSimilarAsync(string titleHash, string contentHash, int lookbackDays, CancellationToken cancellationToken);
    Task<long> InsertAsync(NewsItem newsItem, CancellationToken cancellationToken);
    Task UpdateStatusAsync(long id, NewsItemStatus status, CancellationToken cancellationToken);
    Task UpdateImageAsync(long id, string imageUrl, string imageOrigin, CancellationToken cancellationToken);
    Task<IReadOnlyList<NewsItem>> GetCandidatesForCurationAsync(int maxItems, DateTimeOffset publishedAfter, CancellationToken cancellationToken);
    Task<IReadOnlyList<NewsItem>> GetRecentAsync(int maxItems, CancellationToken cancellationToken);
    Task<IReadOnlyList<NewsItem>> GetWithoutImageAsync(CancellationToken cancellationToken);
    Task<int> NormalizeStoredContentAsync(CancellationToken cancellationToken);
}

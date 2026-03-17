using AiNewsCurator.Domain.Entities;

namespace AiNewsCurator.Domain.Interfaces;

public interface IPostDraftRepository
{
    Task<long> InsertAsync(PostDraft draft, CancellationToken cancellationToken);
    Task<PostDraft?> GetByIdAsync(long id, CancellationToken cancellationToken);
    Task<IReadOnlyList<PostDraft>> GetByNewsItemIdAsync(long newsItemId, CancellationToken cancellationToken);
    Task<IReadOnlyList<PostDraft>> GetPendingApprovalAsync(CancellationToken cancellationToken);
    Task UpdateAsync(PostDraft draft, CancellationToken cancellationToken);
}

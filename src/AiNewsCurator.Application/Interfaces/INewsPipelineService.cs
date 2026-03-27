using AiNewsCurator.Application.DTOs;

namespace AiNewsCurator.Application.Interfaces;

public interface INewsPipelineService
{
    Task<DailyRunResult> RunDailyAsync(TriggerContext triggerContext, CancellationToken cancellationToken);
    Task<int> RunCollectAsync(TriggerContext triggerContext, CancellationToken cancellationToken);
    Task<int> RunCurateAsync(TriggerContext triggerContext, CancellationToken cancellationToken);
    Task<int> RegenerateExistingDraftsAsync(string requestedBy, CancellationToken cancellationToken);
    Task<bool> RegenerateDraftAsync(long draftId, string requestedBy, CancellationToken cancellationToken);
    Task<bool> ReprocessNewsItemAsync(long newsItemId, string requestedBy, CancellationToken cancellationToken);
    Task<bool> CreateManualDraftAsync(long newsItemId, string requestedBy, CancellationToken cancellationToken);
    Task<bool> PublishDraftAsync(long draftId, string approvedBy, CancellationToken cancellationToken);
    Task<bool> ApproveDraftAsync(long draftId, string approvedBy, CancellationToken cancellationToken);
    Task<bool> RejectDraftAsync(long draftId, string approvedBy, CancellationToken cancellationToken);
    Task<bool> DismissDraftAsync(long draftId, string approvedBy, CancellationToken cancellationToken);
    Task<bool> ReopenDraftAsync(long draftId, string approvedBy, CancellationToken cancellationToken);
}

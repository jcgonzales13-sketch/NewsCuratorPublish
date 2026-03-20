using AiNewsCurator.Domain.Entities;
using AiNewsCurator.Domain.Enums;

namespace AiNewsCurator.Api.Models.Operations;

public sealed class OperationsDashboardViewModel
{
    public IReadOnlyList<PostDraft> Drafts { get; init; } = [];
    public IReadOnlyList<ExecutionRun> Runs { get; init; } = [];
    public IReadOnlyList<Source> Sources { get; init; } = [];
    public IReadOnlyList<OperationsNewsItemViewModel> NewsItems { get; init; } = [];
    public LinkedInAuthState LinkedInStatus { get; init; } = new();
    public CreateSourceFormModel CreateSource { get; init; } = new();
    public string? FlashMessage { get; init; }
    public bool FlashIsError { get; init; }

    public int PendingDraftCount => Drafts.Count(d => d.Status == DraftStatus.PendingApproval);
    public int ApprovedDraftCount => Drafts.Count(d => d.Status == DraftStatus.Approved);
    public int ActiveSourceCount => Sources.Count(s => s.IsActive);
}

using AiNewsCurator.Domain.Entities;
using AiNewsCurator.Domain.Enums;

namespace AiNewsCurator.Api.Models.Operations;

public sealed class OperationsDashboardViewModel
{
    public IReadOnlyList<OperationsDraftViewModel> Drafts { get; init; } = [];
    public IReadOnlyList<ExecutionRun> Runs { get; init; } = [];
    public IReadOnlyList<OperationsSourceViewModel> Sources { get; init; } = [];
    public IReadOnlyList<OperationsNewsItemViewModel> NewsItems { get; init; } = [];
    public LinkedInAuthState LinkedInStatus { get; init; } = new();
    public CreateSourceFormModel CreateSource { get; init; } = new();
    public string DraftFilter { get; init; } = "review";
    public string EditorialProfileFilter { get; init; } = "all";
    public string DraftQuery { get; init; } = string.Empty;
    public string NewsQuery { get; init; } = string.Empty;
    public string SourceQuery { get; init; } = string.Empty;
    public string NewsSort { get; init; } = "relevance";
    public string PreviewMode { get; init; } = "editorial";
    public string? FlashMessage { get; init; }
    public bool FlashIsError { get; init; }
    public int ReviewDraftCount { get; init; }
    public int DismissedDraftCount { get; init; }
    public int RejectedDraftCount { get; init; }
    public int FailedDraftCount { get; init; }
    public int TotalEditableDraftCount { get; init; }
    public int DraftPage { get; init; } = 1;
    public int DraftTotalPages { get; init; } = 1;
    public int NewsPage { get; init; } = 1;
    public int NewsTotalPages { get; init; } = 1;
    public int SourcePage { get; init; } = 1;
    public int SourceTotalPages { get; init; } = 1;
    public int RunPage { get; init; } = 1;
    public int RunTotalPages { get; init; } = 1;

    public int PendingDraftCount { get; init; }
    public int ApprovedDraftCount { get; init; }
    public int ActiveSourceCount => Sources.Count(s => s.Source.IsActive);
}

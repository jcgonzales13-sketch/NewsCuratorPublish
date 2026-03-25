using AiNewsCurator.Api.Models.Operations;
using AiNewsCurator.Api.Operations;
using AiNewsCurator.Application.DTOs;
using AiNewsCurator.Application.Interfaces;
using AiNewsCurator.Domain.Entities;
using AiNewsCurator.Domain.Enums;
using AiNewsCurator.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace AiNewsCurator.Api.Controllers;

public sealed class OperationsController : Controller
{
    private readonly INewsPipelineService _pipelineService;
    private readonly IPostDraftRepository _postDraftRepository;
    private readonly IExecutionRunRepository _executionRunRepository;
    private readonly ISourceRepository _sourceRepository;
    private readonly INewsItemRepository _newsItemRepository;
    private readonly ICurationResultRepository _curationResultRepository;
    private readonly IPublicationRepository _publicationRepository;
    private readonly ILinkedInAuthService _linkedInAuthService;
    private readonly ILinkedInPublisher _linkedInPublisher;
    private readonly IAiCurationService _aiCurationService;
    private readonly INewsImageEnrichmentService _newsImageEnrichmentService;
    private readonly AppOptions _options;

    public OperationsController(
        INewsPipelineService pipelineService,
        IPostDraftRepository postDraftRepository,
        IExecutionRunRepository executionRunRepository,
        ISourceRepository sourceRepository,
        INewsItemRepository newsItemRepository,
        ICurationResultRepository curationResultRepository,
        IPublicationRepository publicationRepository,
        ILinkedInAuthService linkedInAuthService,
        ILinkedInPublisher linkedInPublisher,
        IAiCurationService aiCurationService,
        INewsImageEnrichmentService newsImageEnrichmentService,
        IOptions<AppOptions> options)
    {
        _pipelineService = pipelineService;
        _postDraftRepository = postDraftRepository;
        _executionRunRepository = executionRunRepository;
        _sourceRepository = sourceRepository;
        _newsItemRepository = newsItemRepository;
        _curationResultRepository = curationResultRepository;
        _publicationRepository = publicationRepository;
        _linkedInAuthService = linkedInAuthService;
        _linkedInPublisher = linkedInPublisher;
        _aiCurationService = aiCurationService;
        _newsImageEnrichmentService = newsImageEnrichmentService;
        _options = options.Value;
    }

    [HttpGet("/ops/login")]
    public IActionResult Login([FromQuery] string? returnUrl = null)
    {
        return View(new OperationsLoginViewModel
        {
            ReturnUrl = SanitizeReturnUrl(returnUrl)
        });
    }

    [HttpPost("/ops/login")]
    [ValidateAntiForgeryToken]
    public IActionResult Login(OperationsLoginViewModel model)
    {
        if (!string.Equals(model.ApiKey, _options.InternalApiKey, StringComparison.Ordinal))
        {
            model.ErrorMessage = "Invalid API key.";
            model.ApiKey = string.Empty;
            model.ReturnUrl = SanitizeReturnUrl(model.ReturnUrl);
            return View(model);
        }

        Response.Cookies.Append(
            OperationsAuthCookie.CookieName,
            OperationsAuthCookie.CreateValue(model.ApiKey),
            new CookieOptions
            {
                HttpOnly = true,
                IsEssential = true,
                SameSite = SameSiteMode.Lax,
                Secure = Request.IsHttps,
                Expires = DateTimeOffset.UtcNow.AddHours(8)
            });

        return Redirect(SanitizeReturnUrl(model.ReturnUrl));
    }

    [HttpPost("/ops/logout")]
    [ValidateAntiForgeryToken]
    public IActionResult Logout([FromForm] string? returnUrl = null)
    {
        Response.Cookies.Delete(OperationsAuthCookie.CookieName);
        return RedirectToAction(nameof(Login), new { returnUrl = SanitizeReturnUrl(returnUrl) });
    }

    [HttpGet("/ops")]
    public async Task<IActionResult> Index(
        [FromQuery] string? sort = null,
        [FromQuery] string? preview = null,
        [FromQuery] string? drafts = null,
        [FromQuery] string? draftQuery = null,
        [FromQuery] string? newsQuery = null,
        [FromQuery] string? sourceQuery = null,
        [FromQuery] int draftPage = 1,
        [FromQuery] int newsPage = 1,
        [FromQuery] int sourcePage = 1,
        [FromQuery] int runPage = 1,
        CancellationToken cancellationToken = default)
    {
        const int sectionPageSize = 5;

        var allDrafts = await _postDraftRepository.GetAllEditableAsync(cancellationToken);
        var normalizedDraftPage = NormalizePage(draftPage);
        var normalizedNewsPage = NormalizePage(newsPage);
        var normalizedSourcePage = NormalizePage(sourcePage);
        var normalizedRunPage = NormalizePage(runPage);

        var runs = await _executionRunRepository.GetRecentAsync(normalizedRunPage * sectionPageSize, cancellationToken);
        var sources = await _sourceRepository.GetAllAsync(cancellationToken);
        var sourceMap = sources.ToDictionary(source => source.Id, source => source.Name);
        var news = await _newsItemRepository.GetRecentAsync(normalizedNewsPage * sectionPageSize, cancellationToken);
        var linkedInStatus = await _linkedInAuthService.GetStatusAsync(cancellationToken);
        var selectedDraftFilter = NormalizeDraftFilter(drafts);
        var normalizedDraftQuery = NormalizeSearchQuery(draftQuery);
        var normalizedNewsQuery = NormalizeSearchQuery(newsQuery);
        var normalizedSourceQuery = NormalizeSearchQuery(sourceQuery);
        var filteredDrafts = FilterDrafts(allDrafts, selectedDraftFilter);
        filteredDrafts = FilterDraftsByQuery(filteredDrafts, normalizedDraftQuery);
        var pagedDrafts = Paginate(filteredDrafts, normalizedDraftPage, sectionPageSize, out var draftTotalPages);

        var draftItems = new List<OperationsDraftViewModel>(pagedDrafts.Count);
        foreach (var draft in pagedDrafts)
        {
            var newsItem = await _newsItemRepository.GetByIdAsync(draft.NewsItemId, cancellationToken);
            draftItems.Add(new OperationsDraftViewModel
            {
                Draft = draft,
                NewsItem = newsItem,
                SourceName = newsItem is not null && sourceMap.TryGetValue(newsItem.SourceId, out var sourceName) ? sourceName : null,
                LatestPublication = await _publicationRepository.GetLatestByDraftIdAsync(draft.Id, cancellationToken)
            });
        }

        var newsItems = new List<OperationsNewsItemViewModel>(news.Count);
        foreach (var item in news)
        {
            var latestCuration = await _curationResultRepository.GetLatestByNewsItemIdAsync(item.Id, cancellationToken);
            var draftsByNewsItem = await _postDraftRepository.GetByNewsItemIdAsync(item.Id, cancellationToken);
            var latestLinkedInPublication = await GetLatestLinkedInPublicationAsync(draftsByNewsItem, cancellationToken);
            newsItems.Add(new OperationsNewsItemViewModel
            {
                NewsItem = item,
                LatestCuration = latestCuration,
                SourceName = sourceMap.TryGetValue(item.SourceId, out var sourceName) ? sourceName : null,
                IsPublishedToLinkedIn = latestLinkedInPublication?.Status == PublicationStatus.Published,
                LinkedInPublishedAt = latestLinkedInPublication?.PublishedAt,
                LinkedInPostId = latestLinkedInPublication?.PlatformPostId
            });
        }

        var selectedSort = NormalizeSort(sort);
        var previewMode = NormalizePreviewMode(preview);
        newsItems = SortNewsItems(newsItems, selectedSort);
        newsItems = FilterNewsItemsByQuery(newsItems, normalizedNewsQuery);
        var pagedNewsItems = Paginate(newsItems, normalizedNewsPage, sectionPageSize, out var newsTotalPages);
        var sourceItems = sources
            .OrderByDescending(source => source.IsActive)
            .ThenByDescending(source => source.Priority)
            .Select(source => new OperationsSourceViewModel
            {
                Source = source
            })
            .ToList();
        sourceItems = FilterSourcesByQuery(sourceItems, normalizedSourceQuery);
        var pagedSourceItems = Paginate(sourceItems, normalizedSourcePage, sectionPageSize, out var sourceTotalPages);
        var pagedRuns = Paginate(runs.ToList(), normalizedRunPage, sectionPageSize, out var runTotalPages);

        return View(new OperationsDashboardViewModel
        {
            Drafts = draftItems,
            Runs = pagedRuns,
            Sources = pagedSourceItems,
            NewsItems = pagedNewsItems,
            DraftFilter = selectedDraftFilter,
            DraftQuery = normalizedDraftQuery,
            NewsQuery = normalizedNewsQuery,
            SourceQuery = normalizedSourceQuery,
            PendingDraftCount = allDrafts.Count(d => d.Status == DraftStatus.PendingApproval),
            ApprovedDraftCount = allDrafts.Count(d => d.Status == DraftStatus.Approved),
            ReviewDraftCount = allDrafts.Count(d => d.Status is DraftStatus.PendingApproval or DraftStatus.Approved),
            DismissedDraftCount = allDrafts.Count(d => d.Status == DraftStatus.Dismissed),
            RejectedDraftCount = allDrafts.Count(d => d.Status == DraftStatus.Rejected),
            FailedDraftCount = allDrafts.Count(d => d.Status == DraftStatus.Failed),
            TotalEditableDraftCount = allDrafts.Count,
            DraftPage = normalizedDraftPage > draftTotalPages ? draftTotalPages : normalizedDraftPage,
            DraftTotalPages = draftTotalPages,
            NewsPage = normalizedNewsPage > newsTotalPages ? newsTotalPages : normalizedNewsPage,
            NewsTotalPages = newsTotalPages,
            SourcePage = normalizedSourcePage > sourceTotalPages ? sourceTotalPages : normalizedSourcePage,
            SourceTotalPages = sourceTotalPages,
            RunPage = normalizedRunPage > runTotalPages ? runTotalPages : normalizedRunPage,
            RunTotalPages = runTotalPages,
            NewsSort = selectedSort,
            PreviewMode = previewMode,
            LinkedInStatus = linkedInStatus,
            FlashMessage = TempData["FlashMessage"] as string,
            FlashIsError = string.Equals(TempData["FlashType"] as string, "error", StringComparison.Ordinal),
            CreateSource = new CreateSourceFormModel()
        });
    }

    [HttpPost("/ops/actions/daily")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RunDaily([FromForm] string? returnUrl = null, CancellationToken cancellationToken = default)
    {
        var result = await _pipelineService.RunDailyAsync(BuildTriggerContext("ops-daily"), cancellationToken);
        SetFlash($"Daily run completed. Collected: {result.ItemsCollected}. Curated: {result.ItemsCurated}. Published: {result.ItemsPublished}.");
        return RedirectToReturnUrl(returnUrl);
    }

    [HttpPost("/ops/actions/collect")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RunCollect([FromForm] string? returnUrl = null, CancellationToken cancellationToken = default)
    {
        var itemsCollected = await _pipelineService.RunCollectAsync(BuildTriggerContext("ops-collect"), cancellationToken);
        SetFlash($"Collection completed with {itemsCollected} items.");
        return RedirectToReturnUrl(returnUrl);
    }

    [HttpPost("/ops/actions/curate")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RunCurate([FromForm] string? returnUrl = null, CancellationToken cancellationToken = default)
    {
        var itemsCurated = await _pipelineService.RunCurateAsync(BuildTriggerContext("ops-curate"), cancellationToken);
        SetFlash($"Curation completed with {itemsCurated} draft candidates.");
        return RedirectToReturnUrl(returnUrl);
    }

    [HttpPost("/ops/actions/normalize-news")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> NormalizeNews([FromForm] string? returnUrl = null, CancellationToken cancellationToken = default)
    {
        var itemsNormalized = await _newsItemRepository.NormalizeStoredContentAsync(cancellationToken);
        var imagesEnriched = await _newsImageEnrichmentService.BackfillMissingImagesAsync(cancellationToken);
        var draftsRegenerated = await _pipelineService.RegenerateExistingDraftsAsync("ops-normalize", cancellationToken);
        SetFlash($"Normalization completed for {itemsNormalized} news items. Images enriched: {imagesEnriched}. Drafts regenerated: {draftsRegenerated}.");
        return RedirectToReturnUrl(returnUrl);
    }

    [HttpPost("/ops/drafts/{id:long}/approve")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveDraft(long id, [FromForm] string? returnUrl = null, CancellationToken cancellationToken = default)
    {
        var success = await _pipelineService.ApproveDraftAsync(id, "ops-ui", cancellationToken);
        SetFlash(success ? "Draft approved." : "Unable to approve the draft.", !success);
        return RedirectToReturnUrl(returnUrl);
    }

    [HttpPost("/ops/drafts/{id:long}/reject")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectDraft(long id, [FromForm] string? returnUrl = null, CancellationToken cancellationToken = default)
    {
        var success = await _pipelineService.RejectDraftAsync(id, "ops-ui", cancellationToken);
        SetFlash(success ? "Draft rejected." : "Unable to reject the draft.", !success);
        return RedirectToReturnUrl(returnUrl);
    }

    [HttpPost("/ops/drafts/{id:long}/dismiss")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DismissDraft(long id, [FromForm] string? returnUrl = null, CancellationToken cancellationToken = default)
    {
        var success = await _pipelineService.DismissDraftAsync(id, "ops-ui", cancellationToken);
        SetFlash(success ? "Draft dismissed from the review queue." : "Unable to dismiss the draft.", !success);
        return RedirectToReturnUrl(returnUrl);
    }

    [HttpPost("/ops/drafts/{id:long}/reopen")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReopenDraft(long id, [FromForm] string? returnUrl = null, CancellationToken cancellationToken = default)
    {
        var success = await _pipelineService.ReopenDraftAsync(id, "ops-ui", cancellationToken);
        SetFlash(success ? "Draft moved back to the review queue." : "Unable to reopen the draft.", !success);
        return RedirectToReturnUrl(returnUrl);
    }

    [HttpPost("/ops/drafts/{id:long}/publish")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PublishDraft(long id, [FromForm] string? returnUrl = null, CancellationToken cancellationToken = default)
    {
        var success = await _pipelineService.PublishDraftAsync(id, "ops-ui", cancellationToken);
        SetFlash(success ? "Draft published on LinkedIn." : "Unable to publish the draft.", !success);
        return RedirectToReturnUrl(returnUrl);
    }

    [HttpPost("/ops/drafts/{id:long}/edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateDraft(long id, [Bind(Prefix = "EditForm")] UpdateDraftFormModel model, [FromForm] string? returnUrl = null, CancellationToken cancellationToken = default)
    {
        var draft = await _postDraftRepository.GetByIdAsync(id, cancellationToken);
        if (draft is null)
        {
            SetFlash("Draft not found.", true);
            return RedirectToReturnUrl(returnUrl);
        }

        if (draft.Status == DraftStatus.Published)
        {
            SetFlash("Published drafts cannot be edited.", true);
            return RedirectToReturnUrl(returnUrl);
        }

        var postText = model.PostText?.Trim();
        if (string.IsNullOrWhiteSpace(postText))
        {
            SetFlash("Draft text cannot be empty.", true);
            return RedirectToReturnUrl(returnUrl);
        }

        var newsItem = await _newsItemRepository.GetByIdAsync(draft.NewsItemId, cancellationToken);
        if (newsItem is null)
        {
            SetFlash("News item for this draft was not found.", true);
            return RedirectToReturnUrl(returnUrl);
        }

        var validation = await _aiCurationService.ValidatePostAsync(newsItem, postText, cancellationToken);
        ApplyDraftEdits(draft, model, validation);
        await _postDraftRepository.UpdateAsync(draft, cancellationToken);

        SetFlash(validation.IsValid
            ? "Draft updated successfully."
            : "Draft updated, but it still has editorial review notes.");
        return RedirectToReturnUrl(returnUrl);
    }

    [HttpPost("/ops/sources")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateSource([Bind(Prefix = "CreateSource")] CreateSourceFormModel model, [FromForm] string? returnUrl = null, CancellationToken cancellationToken = default)
    {
        if (!SourceInputMapper.TryBuildSource(
                model.Name,
                model.Type,
                model.Url,
                model.Language,
                model.IsActive,
                model.Priority,
                model.MaxItemsPerRun,
                SourceInputMapper.ParseCsv(model.IncludeKeywords),
                SourceInputMapper.ParseCsv(model.ExcludeKeywords),
                SourceInputMapper.ParseCsv(model.Tags),
                out var source,
                out var error))
        {
            SetFlash(error ?? "Unable to create the source.", true);
            return RedirectToReturnUrl(returnUrl);
        }

        source!.CreatedAt = DateTimeOffset.UtcNow;
        source.UpdatedAt = DateTimeOffset.UtcNow;
        await _sourceRepository.InsertAsync(source, cancellationToken);
        SetFlash("Source created successfully.");
        return RedirectToReturnUrl(returnUrl);
    }

    [HttpPost("/ops/sources/{id:long}/toggle")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleSource(long id, [FromForm] string? returnUrl = null, CancellationToken cancellationToken = default)
    {
        var source = await _sourceRepository.GetByIdAsync(id, cancellationToken);
        if (source is null)
        {
            SetFlash("Source not found.", true);
            return RedirectToReturnUrl(returnUrl);
        }

        source.IsActive = !source.IsActive;
        source.UpdatedAt = DateTimeOffset.UtcNow;
        await _sourceRepository.UpdateAsync(source, cancellationToken);
        SetFlash(source.IsActive ? "Source activated." : "Source deactivated.");
        return RedirectToReturnUrl(returnUrl);
    }

    [HttpPost("/ops/sources/{id:long}/edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateSource(long id, [Bind(Prefix = "EditForm")] UpdateSourceFormModel model, [FromForm] string? returnUrl = null, CancellationToken cancellationToken = default)
    {
        var existing = await _sourceRepository.GetByIdAsync(id, cancellationToken);
        if (existing is null)
        {
            SetFlash("Source not found.", true);
            return RedirectToReturnUrl(returnUrl);
        }

        if (!SourceInputMapper.TryBuildSource(
                model.Name,
                model.Type,
                model.Url,
                model.Language,
                model.IsActive,
                model.Priority,
                model.MaxItemsPerRun,
                SourceInputMapper.ParseCsv(model.IncludeKeywords),
                SourceInputMapper.ParseCsv(model.ExcludeKeywords),
                SourceInputMapper.ParseCsv(model.Tags),
                out var source,
                out var error))
        {
            SetFlash(error ?? "Unable to update the source.", true);
            return RedirectToReturnUrl(returnUrl);
        }

        source!.Id = existing.Id;
        source.CreatedAt = existing.CreatedAt;
        source.UpdatedAt = DateTimeOffset.UtcNow;
        await _sourceRepository.UpdateAsync(source, cancellationToken);
        SetFlash("Source updated successfully.");
        return RedirectToReturnUrl(returnUrl);
    }

    [HttpPost("/ops/news/{id:long}/draft")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateManualDraft(long id, [FromForm] string? returnUrl = null, CancellationToken cancellationToken = default)
    {
        var success = await _pipelineService.CreateManualDraftAsync(id, "ops-ui", cancellationToken);
        SetFlash(success ? "Manual draft created from the news item." : "Unable to create a draft for this news item.", !success);
        return RedirectToReturnUrl(returnUrl);
    }

    [HttpPost("/ops/linkedin/connect")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConnectLinkedIn(CancellationToken cancellationToken)
    {
        var authorizationUrl = await _linkedInAuthService.CreateAuthorizationUrlAsync(cancellationToken);
        return Redirect(authorizationUrl);
    }

    [HttpPost("/ops/linkedin/validate")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ValidateLinkedIn([FromForm] string? returnUrl = null, CancellationToken cancellationToken = default)
    {
        var result = await _linkedInPublisher.ValidateCredentialsAsync(cancellationToken);
        SetFlash(
            result.Success
                ? "LinkedIn credentials validated successfully."
                : $"LinkedIn validation failed: {result.ErrorMessage}",
            !result.Success);
        return RedirectToReturnUrl(returnUrl);
    }

    [HttpPost("/ops/linkedin/refresh")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RefreshLinkedIn([FromForm] string? returnUrl = null, CancellationToken cancellationToken = default)
    {
        var result = await _linkedInPublisher.RefreshAccessAsync(cancellationToken);
        SetFlash(
            result.Success
                ? "LinkedIn access token refreshed successfully."
                : $"LinkedIn refresh failed: {result.ErrorMessage}",
            !result.Success);
        return RedirectToReturnUrl(returnUrl);
    }

    private static TriggerContext BuildTriggerContext(string action)
    {
        return new TriggerContext
        {
            TriggerType = TriggerType.Manual,
            InitiatedBy = action
        };
    }

    private void SetFlash(string message, bool isError = false)
    {
        TempData["FlashMessage"] = message;
        TempData["FlashType"] = isError ? "error" : "success";
    }

    private async Task<Domain.Entities.Publication?> GetLatestLinkedInPublicationAsync(
        IReadOnlyList<Domain.Entities.PostDraft> drafts,
        CancellationToken cancellationToken)
    {
        Domain.Entities.Publication? latestPublication = null;

        foreach (var draft in drafts)
        {
            var publication = await _publicationRepository.GetLatestByDraftIdAsync(draft.Id, cancellationToken);
            if (publication is null || !string.Equals(publication.Platform, "LinkedIn", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (latestPublication is null ||
                publication.PublishedAt.GetValueOrDefault() > latestPublication.PublishedAt.GetValueOrDefault() ||
                publication.Id > latestPublication.Id)
            {
                latestPublication = publication;
            }
        }

        return latestPublication;
    }

    private static string NormalizeSort(string? sort)
    {
        return sort?.Trim().ToLowerInvariant() switch
        {
            "date" => "date",
            "source" => "source",
            _ => "relevance"
        };
    }

    private static string NormalizePreviewMode(string? preview)
    {
        return string.Equals(preview, "feed", StringComparison.OrdinalIgnoreCase)
            ? "feed"
            : "editorial";
    }

    private static string NormalizeDraftFilter(string? drafts)
    {
        return drafts?.Trim().ToLowerInvariant() switch
        {
            "dismissed" => "dismissed",
            "rejected" => "rejected",
            "failed" => "failed",
            "all" => "all",
            _ => "review"
        };
    }

    private static List<Domain.Entities.PostDraft> FilterDrafts(IEnumerable<Domain.Entities.PostDraft> drafts, string filter)
    {
        return filter switch
        {
            "dismissed" => drafts.Where(d => d.Status == DraftStatus.Dismissed).ToList(),
            "rejected" => drafts.Where(d => d.Status == DraftStatus.Rejected).ToList(),
            "failed" => drafts.Where(d => d.Status == DraftStatus.Failed).ToList(),
            "all" => drafts.ToList(),
            _ => drafts.Where(d => d.Status is DraftStatus.PendingApproval or DraftStatus.Approved).ToList()
        };
    }

    private static string NormalizeSearchQuery(string? query)
    {
        return query?.Trim() ?? string.Empty;
    }

    private static List<Domain.Entities.PostDraft> FilterDraftsByQuery(IEnumerable<Domain.Entities.PostDraft> drafts, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return drafts.ToList();
        }

        return drafts.Where(draft =>
                ContainsIgnoreCase(draft.TitleSuggestion, query) ||
                ContainsIgnoreCase(draft.PostText, query) ||
                ContainsIgnoreCase(draft.Tone, query) ||
                ContainsIgnoreCase(draft.Status.ToString(), query))
            .ToList();
    }

    private static List<OperationsNewsItemViewModel> FilterNewsItemsByQuery(IEnumerable<OperationsNewsItemViewModel> items, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return items.ToList();
        }

        return items.Where(item =>
                ContainsIgnoreCase(item.NewsItem.Title, query) ||
                ContainsIgnoreCase(item.NewsItem.RawSummary, query) ||
                ContainsIgnoreCase(item.SourceName, query) ||
                ContainsIgnoreCase(item.LatestCuration?.WhyRelevant, query))
            .ToList();
    }

    private static List<OperationsSourceViewModel> FilterSourcesByQuery(IEnumerable<OperationsSourceViewModel> items, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return items.ToList();
        }

        return items.Where(item =>
                ContainsIgnoreCase(item.Source.Name, query) ||
                ContainsIgnoreCase(item.Source.Url, query) ||
                ContainsIgnoreCase(item.Source.Language, query) ||
                ContainsIgnoreCase(item.EditForm.Tags, query) ||
                ContainsIgnoreCase(item.EditForm.IncludeKeywords, query) ||
                ContainsIgnoreCase(item.EditForm.ExcludeKeywords, query))
            .ToList();
    }

    private static List<OperationsNewsItemViewModel> SortNewsItems(IEnumerable<OperationsNewsItemViewModel> items, string sort)
    {
        return sort switch
        {
            "date" => items
                .OrderByDescending(item => item.NewsItem.PublishedAt)
                .ThenByDescending(item => item.NewsItem.Id)
                .ToList(),
            "source" => items
                .OrderBy(item => item.SourceName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ThenByDescending(item => item.NewsItem.PublishedAt)
                .ToList(),
            _ => items
                .OrderByDescending(item => item.LatestCuration?.RelevanceScore ?? double.MinValue)
                .ThenByDescending(item => item.LatestCuration?.ConfidenceScore ?? double.MinValue)
                .ThenByDescending(item => item.NewsItem.PublishedAt)
                .ToList()
        };
    }

    private static int NormalizePage(int page)
    {
        return page < 1 ? 1 : page;
    }

    private static List<T> Paginate<T>(IReadOnlyList<T> items, int page, int pageSize, out int totalPages)
    {
        totalPages = Math.Max(1, (int)Math.Ceiling(items.Count / (double)pageSize));
        var normalizedPage = Math.Min(Math.Max(page, 1), totalPages);
        return items
            .Skip((normalizedPage - 1) * pageSize)
            .Take(pageSize)
            .ToList();
    }

    private static bool ContainsIgnoreCase(string? value, string query)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               value.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private static string SanitizeReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl) || !Uri.TryCreate(returnUrl, UriKind.Relative, out _))
        {
            return "/ops";
        }

        return returnUrl;
    }

    private IActionResult RedirectToReturnUrl(string? returnUrl)
    {
        return Redirect(SanitizeReturnUrl(returnUrl));
    }

    private static void ApplyDraftEdits(PostDraft draft, UpdateDraftFormModel model, PostValidationResult validation)
    {
        draft.TitleSuggestion = string.IsNullOrWhiteSpace(model.TitleSuggestion)
            ? null
            : model.TitleSuggestion.Trim();
        draft.PostText = model.PostText.Trim();
        draft.ValidationErrorsJson = JsonSerializer.Serialize(validation.Errors);

        if (draft.Status == DraftStatus.Approved)
        {
            draft.Status = DraftStatus.PendingApproval;
            draft.ApprovedAt = null;
            draft.ApprovedBy = null;
        }
    }
}

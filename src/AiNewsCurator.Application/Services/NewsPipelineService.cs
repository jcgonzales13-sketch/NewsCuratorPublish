using System.Text.Json;
using AiNewsCurator.Application.DTOs;
using AiNewsCurator.Application.Interfaces;
using AiNewsCurator.Domain.Entities;
using AiNewsCurator.Domain.Enums;
using AiNewsCurator.Domain.Interfaces;
using AiNewsCurator.Domain.Rules;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AiNewsCurator.Application.Services;

public sealed class NewsPipelineService : INewsPipelineService
{
    private readonly IEnumerable<INewsCollector> _collectors;
    private readonly ISourceRepository _sourceRepository;
    private readonly INewsItemRepository _newsItemRepository;
    private readonly ICurationResultRepository _curationResultRepository;
    private readonly IPostDraftRepository _postDraftRepository;
    private readonly IPublicationRepository _publicationRepository;
    private readonly IExecutionRunRepository _executionRunRepository;
    private readonly IAiCurationService _aiCurationService;
    private readonly ILinkedInPublisher _linkedInPublisher;
    private readonly AppOptions _options;
    private readonly ILogger<NewsPipelineService> _logger;

    public NewsPipelineService(
        IEnumerable<INewsCollector> collectors,
        ISourceRepository sourceRepository,
        INewsItemRepository newsItemRepository,
        ICurationResultRepository curationResultRepository,
        IPostDraftRepository postDraftRepository,
        IPublicationRepository publicationRepository,
        IExecutionRunRepository executionRunRepository,
        IAiCurationService aiCurationService,
        ILinkedInPublisher linkedInPublisher,
        IOptions<AppOptions> options,
        ILogger<NewsPipelineService> logger)
    {
        _collectors = collectors;
        _sourceRepository = sourceRepository;
        _newsItemRepository = newsItemRepository;
        _curationResultRepository = curationResultRepository;
        _postDraftRepository = postDraftRepository;
        _publicationRepository = publicationRepository;
        _executionRunRepository = executionRunRepository;
        _aiCurationService = aiCurationService;
        _linkedInPublisher = linkedInPublisher;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<DailyRunResult> RunDailyAsync(TriggerContext triggerContext, CancellationToken cancellationToken)
    {
        var run = new ExecutionRun
        {
            StartedAt = DateTimeOffset.UtcNow,
            Status = RunStatus.Started,
            TriggerType = triggerContext.TriggerType
        };

        run.Id = await _executionRunRepository.InsertAsync(run, cancellationToken);

        try
        {
            run.ItemsCollected = await RunCollectAsync(triggerContext, cancellationToken);
            run.ItemsCurated = await RunCurateAsync(triggerContext, cancellationToken);

            var pendingDrafts = await _postDraftRepository.GetPendingApprovalAsync(cancellationToken);
            run.ItemsApproved = pendingDrafts.Count(d => d.Status is DraftStatus.PendingApproval or DraftStatus.Approved);
            run.ItemsPublished = 0;

            if (GetPublishMode() == PublishMode.Automatic)
            {
                foreach (var draft in pendingDrafts.Where(d => d.Status == DraftStatus.Approved))
                {
                    if (await PublishDraftAsync(draft.Id, "auto-publisher", cancellationToken))
                    {
                        run.ItemsPublished++;
                    }
                }
            }

            run.Status = run.ErrorCount > 0 ? RunStatus.CompletedWithWarnings : RunStatus.Completed;
            run.LogSummary = JsonSerializer.Serialize(new
            {
                run.ItemsCollected,
                run.ItemsCurated,
                run.ItemsPublished,
                run.ErrorCount
            });
            run.FinishedAt = DateTimeOffset.UtcNow;
            await _executionRunRepository.UpdateAsync(run, cancellationToken);

            return new DailyRunResult
            {
                RunId = run.Id,
                ItemsCollected = run.ItemsCollected,
                ItemsCurated = run.ItemsCurated,
                ItemsApproved = run.ItemsApproved,
                ItemsPublished = run.ItemsPublished,
                ErrorCount = run.ErrorCount,
                Status = run.Status.ToString()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Daily run failed. RunId={RunId}", run.Id);
            run.Status = RunStatus.Failed;
            run.ErrorCount++;
            run.LogSummary = ex.Message;
            run.FinishedAt = DateTimeOffset.UtcNow;
            await _executionRunRepository.UpdateAsync(run, cancellationToken);
            throw;
        }
    }

    public async Task<int> RunCollectAsync(TriggerContext triggerContext, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting collection flow. Trigger={TriggerType}", triggerContext.TriggerType);
        var sources = await _sourceRepository.GetActiveAsync(cancellationToken);
        var collectedCount = 0;

        foreach (var source in sources)
        {
            var collector = _collectors.FirstOrDefault(item => item.CanHandle(source));
            if (collector is null)
            {
                _logger.LogWarning("No collector registered for source {SourceId} type {SourceType}", source.Id, source.Type);
                continue;
            }

            IReadOnlyList<CollectedNewsItem> items;
            try
            {
                items = await collector.CollectAsync(source, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Collection failed for source {SourceId} ({SourceName}). Skipping source.", source.Id, source.Name);
                continue;
            }

            foreach (var item in items.Take(source.MaxItemsPerRun))
            {
                var canonicalUrl = UrlNormalization.Normalize(item.CanonicalUrl);
                var titleHash = HashingRules.Sha256($"{source.Name}:{item.Title}");
                var contentHash = HashingRules.Sha256(item.RawContent ?? item.RawSummary ?? item.Title);

                if (await _newsItemRepository.GetByCanonicalUrlAsync(canonicalUrl, cancellationToken) is not null)
                {
                    collectedCount++;
                    continue;
                }

                var newsItem = new NewsItem
                {
                    SourceId = source.Id,
                    ExternalId = item.ExternalId,
                    Title = item.Title,
                    Url = item.Url,
                    CanonicalUrl = canonicalUrl,
                    ImageUrl = item.ImageUrl,
                    ImageOrigin = item.ImageOrigin,
                    Author = item.Author,
                    PublishedAt = item.PublishedAt,
                    Language = item.Language,
                    RawSummary = item.RawSummary,
                    RawContent = item.RawContent,
                    TitleHash = titleHash,
                    ContentHash = contentHash,
                    Status = NewsItemStatus.Collected,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                };

                await _newsItemRepository.InsertAsync(newsItem, cancellationToken);
                collectedCount++;
            }
        }

        return collectedCount;
    }

    public async Task<int> RunCurateAsync(TriggerContext triggerContext, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting curation flow. Trigger={TriggerType}", triggerContext.TriggerType);
        var publishedAfter = DateTimeOffset.UtcNow.AddHours(-_options.NewsWindowHours);
        var candidates = await _newsItemRepository.GetCandidatesForCurationAsync(_options.MaxCandidatesForAi, publishedAfter, cancellationToken);
        var curatedCount = 0;

        foreach (var newsItem in candidates)
        {
            if (await CurateNewsItemAsync(newsItem, "auto-approval", cancellationToken))
            {
                curatedCount++;
            }
        }

        return curatedCount;
    }

    public async Task<int> RegenerateExistingDraftsAsync(string requestedBy, CancellationToken cancellationToken)
    {
        var drafts = await _postDraftRepository.GetAllEditableAsync(cancellationToken);
        var regeneratedCount = 0;

        foreach (var draft in drafts)
        {
            if (!await RegenerateDraftInternalAsync(draft, moveToPendingApproval: false, cancellationToken))
            {
                continue;
            }
            regeneratedCount++;
        }

        return regeneratedCount;
    }

    public async Task<bool> RegenerateDraftAsync(long draftId, string requestedBy, CancellationToken cancellationToken)
    {
        var draft = await _postDraftRepository.GetByIdAsync(draftId, cancellationToken);
        if (draft is null || draft.Status == DraftStatus.Published)
        {
            return false;
        }

        return await RegenerateDraftInternalAsync(draft, moveToPendingApproval: true, cancellationToken);
    }

    public async Task<bool> ReplaceDraftAsync(long draftId, string requestedBy, CancellationToken cancellationToken)
    {
        var draft = await _postDraftRepository.GetByIdAsync(draftId, cancellationToken);
        if (draft is null || draft.Status == DraftStatus.Published)
        {
            return false;
        }

        var newsItemId = draft.NewsItemId;
        draft.Status = DraftStatus.Dismissed;
        draft.ApprovedAt = DateTimeOffset.UtcNow;
        draft.ApprovedBy = requestedBy;
        await _postDraftRepository.UpdateAsync(draft, cancellationToken);

        return await CreateManualDraftAsync(newsItemId, requestedBy, cancellationToken);
    }

    public async Task<bool> ReprocessNewsItemAsync(long newsItemId, string requestedBy, CancellationToken cancellationToken)
    {
        var newsItem = await _newsItemRepository.GetByIdAsync(newsItemId, cancellationToken);
        if (newsItem is null)
        {
            return false;
        }

        var existingDrafts = await _postDraftRepository.GetByNewsItemIdAsync(newsItemId, cancellationToken);
        if (existingDrafts.Any(draft => draft.Status == DraftStatus.Published))
        {
            _logger.LogInformation("Skipping reprocess for NewsItemId={NewsItemId} because it was already published.", newsItemId);
            return false;
        }

        await _newsItemRepository.UpdateStatusAsync(newsItemId, NewsItemStatus.Collected, cancellationToken);
        newsItem.Status = NewsItemStatus.Collected;
        return await CurateNewsItemAsync(newsItem, requestedBy, cancellationToken);
    }

    public async Task<bool> CreateManualDraftAsync(long newsItemId, string requestedBy, CancellationToken cancellationToken)
    {
        var newsItem = await _newsItemRepository.GetByIdAsync(newsItemId, cancellationToken);
        if (newsItem is null)
        {
            return false;
        }

        var existingDrafts = await _postDraftRepository.GetByNewsItemIdAsync(newsItemId, cancellationToken);
        if (existingDrafts.Any(draft => draft.Status == DraftStatus.Published))
        {
            _logger.LogInformation("Skipping manual draft for NewsItemId={NewsItemId} because it was already published.", newsItemId);
            return false;
        }

        var evaluation = await _aiCurationService.EvaluateNewsAsync(newsItem, cancellationToken);
        var curationResult = await CreateAndPersistCurationResultAsync(newsItem, evaluation, cancellationToken);
        var draftId = await CreateDraftAsync(newsItem, curationResult, evaluation.LinkedInDraft, evaluation.LinkedInTitleSuggestion, requestedBy, forcePendingApproval: true, cancellationToken);

        _logger.LogInformation("Created manual draft {DraftId} for NewsItemId={NewsItemId}", draftId, newsItemId);
        return draftId > 0;
    }

    public async Task<bool> PublishDraftAsync(long draftId, string approvedBy, CancellationToken cancellationToken)
    {
        var draft = await _postDraftRepository.GetByIdAsync(draftId, cancellationToken);
        if (draft is null)
        {
            return false;
        }

        draft = await EnsureDraftIncludesOriginalArticleUrlAsync(draft, cancellationToken);

        var validation = await _linkedInPublisher.ValidateCredentialsAsync(cancellationToken);
        if (!validation.Success)
        {
            var failedPublication = new Publication
            {
                PostDraftId = draft.Id,
                Platform = "LinkedIn",
                RequestPayload = string.Empty,
                ResponsePayload = string.Empty,
                Status = PublicationStatus.Failed,
                ErrorMessage = validation.ErrorMessage
            };

            await _publicationRepository.InsertAsync(failedPublication, cancellationToken);
            draft.Status = DraftStatus.Failed;
            draft.ApprovedBy = approvedBy;
            draft.ApprovedAt = DateTimeOffset.UtcNow;
            await _postDraftRepository.UpdateAsync(draft, cancellationToken);
            _logger.LogWarning("LinkedIn publish skipped for DraftId={DraftId}: {Reason}", draftId, validation.ErrorMessage);
            return false;
        }

        var publishResult = await _linkedInPublisher.PublishTextPostAsync(draft, cancellationToken);
        var publication = new Publication
        {
            PostDraftId = draft.Id,
            Platform = "LinkedIn",
            PlatformPostId = publishResult.PlatformPostId,
            PublishedAt = publishResult.Success ? DateTimeOffset.UtcNow : null,
            RequestPayload = publishResult.RequestPayload,
            ResponsePayload = publishResult.ResponsePayload,
            Status = publishResult.Success ? PublicationStatus.Published : PublicationStatus.Failed,
            ErrorMessage = publishResult.ErrorMessage
        };

        await _publicationRepository.InsertAsync(publication, cancellationToken);

        draft.Status = publishResult.Success ? DraftStatus.Published : DraftStatus.Failed;
        draft.ApprovedBy = approvedBy;
        draft.ApprovedAt = DateTimeOffset.UtcNow;
        await _postDraftRepository.UpdateAsync(draft, cancellationToken);
        return publishResult.Success;
    }

    private async Task<PostDraft> EnsureDraftIncludesOriginalArticleUrlAsync(PostDraft draft, CancellationToken cancellationToken)
    {
        var parsedDraft = LinkedInEditorialPostFormatter.Parse(draft.PostText);
        if (!string.IsNullOrWhiteSpace(parsedDraft.OriginalArticleUrl))
        {
            return draft;
        }

        var newsItem = await _newsItemRepository.GetByIdAsync(draft.NewsItemId, cancellationToken);
        if (newsItem is null)
        {
            return draft;
        }

        var editorialDraft = await ApplyEditorialMetadataAsync(newsItem, draft.PostText, cancellationToken);
        var rebuiltPostText = LinkedInEditorialPostFormatter.BuildPostText(editorialDraft);
        if (string.Equals(rebuiltPostText, draft.PostText, StringComparison.Ordinal))
        {
            return draft;
        }

        draft.TitleSuggestion = !string.IsNullOrWhiteSpace(draft.TitleSuggestion)
            ? draft.TitleSuggestion
            : editorialDraft.Headline;
        draft.PostText = rebuiltPostText;
        await _postDraftRepository.UpdateAsync(draft, cancellationToken);
        return draft;
    }

    public async Task<bool> ApproveDraftAsync(long draftId, string approvedBy, CancellationToken cancellationToken)
    {
        var draft = await _postDraftRepository.GetByIdAsync(draftId, cancellationToken);
        if (draft is null)
        {
            return false;
        }

        draft.Status = DraftStatus.Approved;
        draft.ApprovedAt = DateTimeOffset.UtcNow;
        draft.ApprovedBy = approvedBy;
        await _postDraftRepository.UpdateAsync(draft, cancellationToken);

        return GetPublishMode() != PublishMode.Automatic || await PublishDraftAsync(draftId, approvedBy, cancellationToken);
    }

    public async Task<bool> RejectDraftAsync(long draftId, string approvedBy, CancellationToken cancellationToken)
    {
        var draft = await _postDraftRepository.GetByIdAsync(draftId, cancellationToken);
        if (draft is null)
        {
            return false;
        }

        draft.Status = DraftStatus.Rejected;
        draft.ApprovedAt = DateTimeOffset.UtcNow;
        draft.ApprovedBy = approvedBy;
        await _postDraftRepository.UpdateAsync(draft, cancellationToken);
        return true;
    }

    public async Task<bool> DismissDraftAsync(long draftId, string approvedBy, CancellationToken cancellationToken)
    {
        var draft = await _postDraftRepository.GetByIdAsync(draftId, cancellationToken);
        if (draft is null)
        {
            return false;
        }

        draft.Status = DraftStatus.Dismissed;
        draft.ApprovedAt = DateTimeOffset.UtcNow;
        draft.ApprovedBy = approvedBy;
        await _postDraftRepository.UpdateAsync(draft, cancellationToken);
        return true;
    }

    public async Task<bool> ReopenDraftAsync(long draftId, string approvedBy, CancellationToken cancellationToken)
    {
        var draft = await _postDraftRepository.GetByIdAsync(draftId, cancellationToken);
        if (draft is null)
        {
            return false;
        }

        if (draft.Status is DraftStatus.Published or DraftStatus.Approved or DraftStatus.PendingApproval)
        {
            return false;
        }

        draft.Status = DraftStatus.PendingApproval;
        draft.ApprovedAt = null;
        draft.ApprovedBy = null;
        await _postDraftRepository.UpdateAsync(draft, cancellationToken);
        return true;
    }

    private PublishMode GetPublishMode()
    {
        return Enum.TryParse<PublishMode>(_options.PublishMode, true, out var mode) ? mode : PublishMode.Manual;
    }

    private async Task<bool> CurateNewsItemAsync(NewsItem newsItem, string initiatedBy, CancellationToken cancellationToken)
    {
        if (await _newsItemRepository.GetPublishedByCanonicalUrlAsync(newsItem.CanonicalUrl, cancellationToken) is not null)
        {
            await _newsItemRepository.UpdateStatusAsync(newsItem.Id, NewsItemStatus.Duplicate, cancellationToken);
            return false;
        }

        if (await _newsItemRepository.ExistsRecentSimilarAsync(newsItem.TitleHash, newsItem.ContentHash, _options.DuplicateLookbackDays, cancellationToken))
        {
            await _newsItemRepository.UpdateStatusAsync(newsItem.Id, NewsItemStatus.Duplicate, cancellationToken);
            return false;
        }

        var evaluation = await _aiCurationService.EvaluateNewsAsync(newsItem, cancellationToken);
        var curationResult = await CreateAndPersistCurationResultAsync(newsItem, evaluation, cancellationToken);

        if (!evaluation.IsRelevant || evaluation.RelevanceScore < _options.RelevanceThreshold)
        {
            await _newsItemRepository.UpdateStatusAsync(newsItem.Id, NewsItemStatus.Rejected, cancellationToken);
            return false;
        }

        await CreateDraftAsync(newsItem, curationResult, evaluation.LinkedInDraft, evaluation.LinkedInTitleSuggestion, initiatedBy, forcePendingApproval: false, cancellationToken);
        return true;
    }

    private async Task<CurationResult> CreateAndPersistCurationResultAsync(NewsItem newsItem, AiEvaluationResult evaluation, CancellationToken cancellationToken)
    {
        var curationResult = new CurationResult
        {
            NewsItemId = newsItem.Id,
            RelevanceScore = evaluation.RelevanceScore,
            ConfidenceScore = evaluation.ConfidenceScore,
            Category = evaluation.Category,
            WhyRelevant = evaluation.WhyRelevant,
            ShouldPublish = evaluation.IsRelevant,
            AiSummary = evaluation.Summary,
            KeyPointsJson = JsonSerializer.Serialize(evaluation.KeyPoints),
            PromptVersion = evaluation.PromptVersion,
            ModelName = evaluation.ModelName,
            PromptPayload = evaluation.PromptPayload,
            ResponsePayload = evaluation.ResponsePayload,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await _curationResultRepository.InsertAsync(curationResult, cancellationToken);
        return curationResult;
    }


    private async Task<long> CreateDraftAsync(
        NewsItem newsItem,
        CurationResult curationResult,
        string? providedDraft,
        string? providedTitleSuggestion,
        string initiatedBy,
        bool forcePendingApproval,
        CancellationToken cancellationToken)
    {
        var generatedPost = string.IsNullOrWhiteSpace(providedDraft)
            ? await _aiCurationService.GenerateLinkedInPostAsync(newsItem, curationResult, cancellationToken)
            : providedDraft;

        var editorialDraft = await ApplyEditorialMetadataAsync(newsItem, generatedPost, cancellationToken);
        generatedPost = LinkedInEditorialPostFormatter.BuildPostText(editorialDraft);
        var validation = await _aiCurationService.ValidatePostAsync(newsItem, generatedPost, cancellationToken);

        var draftStatus = DraftStatus.PendingApproval;
        if (!forcePendingApproval &&
            GetPublishMode() == PublishMode.Automatic &&
            validation.IsValid &&
            curationResult.ConfidenceScore >= _options.ConfidenceThreshold)
        {
            draftStatus = DraftStatus.Approved;
        }

        var draft = new PostDraft
        {
            NewsItemId = newsItem.Id,
            TitleSuggestion = !string.IsNullOrWhiteSpace(providedTitleSuggestion)
                ? providedTitleSuggestion
                : editorialDraft.Headline,
            PostText = generatedPost,
            Tone = _options.LinkedInTone,
            Status = draftStatus,
            ValidationErrorsJson = JsonSerializer.Serialize(validation.Errors),
            CreatedAt = DateTimeOffset.UtcNow
        };

        var draftId = await _postDraftRepository.InsertAsync(draft, cancellationToken);
        await _newsItemRepository.UpdateStatusAsync(newsItem.Id, NewsItemStatus.Selected, cancellationToken);

        if (draftStatus == DraftStatus.Approved)
        {
            await PublishDraftAsync(draftId, initiatedBy, cancellationToken);
        }

        return draftId;
    }

    private async Task<LinkedInEditorialDraft> ApplyEditorialMetadataAsync(NewsItem newsItem, string generatedPost, CancellationToken cancellationToken)
    {
        var parsedDraft = LinkedInEditorialPostFormatter.Parse(generatedPost);
        var source = await _sourceRepository.GetByIdAsync(newsItem.SourceId, cancellationToken);
        var sourceName = source?.Name ?? "Original source";

        return LinkedInEditorialRefiner.Refine(new LinkedInEditorialDraft
        {
            Headline = !string.IsNullOrWhiteSpace(parsedDraft.Headline)
                ? parsedDraft.Headline
                : newsItem.Title,
            Hook = !string.IsNullOrWhiteSpace(parsedDraft.Hook)
                ? parsedDraft.Hook
                : $"A noteworthy AI development: {newsItem.Title}",
            WhatHappened = !string.IsNullOrWhiteSpace(parsedDraft.WhatHappened)
                ? parsedDraft.WhatHappened
                : (newsItem.RawSummary ?? newsItem.Title),
            WhyItMatters = !string.IsNullOrWhiteSpace(parsedDraft.WhyItMatters)
                ? parsedDraft.WhyItMatters
                : "The story has practical implications for teams assessing where AI can improve execution, quality, or competitiveness.",
            StrategicTakeaway = !string.IsNullOrWhiteSpace(parsedDraft.StrategicTakeaway)
                ? parsedDraft.StrategicTakeaway
                : "The broader signal is that AI decisions are becoming more strategic and less experimental.",
            SourceLabel = sourceName,
            OriginalArticleUrl = !string.IsNullOrWhiteSpace(parsedDraft.OriginalArticleUrl)
                ? parsedDraft.OriginalArticleUrl
                : (newsItem.CanonicalUrl ?? newsItem.Url),
            Signature = string.IsNullOrWhiteSpace(_options.AttributionFooterLine)
                ? "Curated by AI News Curator."
                : _options.AttributionFooterLine
        });
    }

    private async Task<bool> RegenerateDraftInternalAsync(PostDraft draft, bool moveToPendingApproval, CancellationToken cancellationToken)
    {
        var newsItem = await _newsItemRepository.GetByIdAsync(draft.NewsItemId, cancellationToken);
        if (newsItem is null)
        {
            return false;
        }

        var evaluation = await _aiCurationService.EvaluateNewsAsync(newsItem, cancellationToken);
        await CreateAndPersistCurationResultAsync(newsItem, evaluation, cancellationToken);
        var generatedPost = evaluation.LinkedInDraft;
        var editorialDraft = await ApplyEditorialMetadataAsync(newsItem, generatedPost, cancellationToken);
        generatedPost = LinkedInEditorialPostFormatter.BuildPostText(editorialDraft);
        var validation = await _aiCurationService.ValidatePostAsync(newsItem, generatedPost, cancellationToken);

        draft.TitleSuggestion = !string.IsNullOrWhiteSpace(evaluation.LinkedInTitleSuggestion)
            ? evaluation.LinkedInTitleSuggestion
            : editorialDraft.Headline;
        draft.PostText = generatedPost;
        draft.Tone = _options.LinkedInTone;
        draft.ValidationErrorsJson = JsonSerializer.Serialize(validation.Errors);

        if (moveToPendingApproval ||
            (draft.Status == DraftStatus.Approved && !validation.IsValid))
        {
            draft.Status = DraftStatus.PendingApproval;
            draft.ApprovedAt = null;
            draft.ApprovedBy = null;
        }

        await _postDraftRepository.UpdateAsync(draft, cancellationToken);
        return true;
    }
}

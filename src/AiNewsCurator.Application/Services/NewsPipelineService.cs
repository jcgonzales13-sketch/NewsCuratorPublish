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

            var items = await collector.CollectAsync(source, cancellationToken);
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

    public async Task<bool> PublishDraftAsync(long draftId, string approvedBy, CancellationToken cancellationToken)
    {
        var draft = await _postDraftRepository.GetByIdAsync(draftId, cancellationToken);
        if (draft is null)
        {
            return false;
        }

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

        if (!evaluation.IsRelevant || evaluation.RelevanceScore < _options.RelevanceThreshold)
        {
            await _newsItemRepository.UpdateStatusAsync(newsItem.Id, NewsItemStatus.Rejected, cancellationToken);
            return false;
        }

        var generatedPost = string.IsNullOrWhiteSpace(evaluation.LinkedInDraft)
            ? await _aiCurationService.GenerateLinkedInPostAsync(newsItem, curationResult, cancellationToken)
            : evaluation.LinkedInDraft;
        var validation = await _aiCurationService.ValidatePostAsync(newsItem, generatedPost, cancellationToken);

        var draftStatus = DraftStatus.PendingApproval;
        if (GetPublishMode() == PublishMode.Automatic &&
            validation.IsValid &&
            evaluation.ConfidenceScore >= _options.ConfidenceThreshold)
        {
            draftStatus = DraftStatus.Approved;
        }

        var draft = new PostDraft
        {
            NewsItemId = newsItem.Id,
            TitleSuggestion = newsItem.Title,
            PostText = generatedPost,
            Tone = "Professional",
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

        return true;
    }
}

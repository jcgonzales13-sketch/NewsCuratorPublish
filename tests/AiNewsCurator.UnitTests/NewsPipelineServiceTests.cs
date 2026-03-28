using AiNewsCurator.Application.DTOs;
using AiNewsCurator.Application.Services;
using AiNewsCurator.Domain.Entities;
using AiNewsCurator.Domain.Enums;
using AiNewsCurator.Domain.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AiNewsCurator.UnitTests;

public sealed class NewsPipelineServiceTests
{
    [Fact]
    public async Task PublishDraft_Should_Fail_And_Record_Publication_When_LinkedIn_Validation_Fails()
    {
        var postDraftRepository = new FakePostDraftRepository
        {
            Draft = new PostDraft
            {
                Id = 10,
                NewsItemId = 20,
                PostText = "Legacy draft",
                Status = DraftStatus.Approved,
                ValidationErrorsJson = "[]"
            }
        };
        var publicationRepository = new FakePublicationRepository();
        var linkedInPublisher = new FakeLinkedInPublisher
        {
            ValidateResult = new OperationResult
            {
                Success = false,
                ErrorMessage = "missing credentials"
            }
        };

        var service = CreateService(
            postDraftRepository: postDraftRepository,
            publicationRepository: publicationRepository,
            linkedInPublisher: linkedInPublisher);

        var success = await service.PublishDraftAsync(10, "ops-ui", CancellationToken.None);

        Assert.False(success);
        Assert.NotNull(publicationRepository.InsertedPublication);
        Assert.Equal(PublicationStatus.Failed, publicationRepository.InsertedPublication.Status);
        Assert.Equal("missing credentials", publicationRepository.InsertedPublication.ErrorMessage);
        Assert.Equal(DraftStatus.Failed, postDraftRepository.UpdatedDraft?.Status);
    }

    [Fact]
    public async Task CreateManualDraft_Should_Use_Ai_Output_And_Save_Draft()
    {
        var newsItemRepository = new FakeNewsItemRepository
        {
            NewsItem = new NewsItem
            {
                Id = 20,
                SourceId = 1,
                Title = "OpenAI expands workflow execution",
                CanonicalUrl = "https://example.com/openai-workflows",
                Url = "https://example.com/openai-workflows",
                RawSummary = "OpenAI expanded workflow execution tooling.",
                TitleHash = "title-hash",
                ContentHash = "content-hash"
            }
        };
        var postDraftRepository = new FakePostDraftRepository();
        var aiService = new FakeAiCurationService
        {
            EvaluationResult = new AiEvaluationResult
            {
                IsRelevant = true,
                RelevanceScore = 0.91,
                ConfidenceScore = 0.88,
                Category = "Agents",
                WhyRelevant = "Relevant",
                Summary = "Summary",
                KeyPoints = ["Point 1"],
                LinkedInTitleSuggestion = "OpenAI expands workflow execution",
                LinkedInDraft = """
                OpenAI expands workflow execution

                The bigger story here is how quickly AI products are moving toward real execution inside everyday workflows.

                What happened:
                OpenAI expanded workflow execution tooling.

                Why it matters:
                This affects workflow execution and task completion.

                Strategic takeaway:
                The real shift is that AI is becoming an execution layer inside workflows.

                Source: OpenAI News
                """
            },
            ValidationResult = new PostValidationResult()
        };

        var service = CreateService(
            newsItemRepository: newsItemRepository,
            postDraftRepository: postDraftRepository,
            aiCurationService: aiService);

        var success = await service.CreateManualDraftAsync(20, "ops-ui", CancellationToken.None);

        Assert.True(success);
        Assert.NotNull(postDraftRepository.InsertedDraft);
        Assert.Equal("OpenAI expands workflow execution", postDraftRepository.InsertedDraft.TitleSuggestion);
        Assert.Contains("Original article: https://example.com/openai-workflows", postDraftRepository.InsertedDraft.PostText);
        Assert.Equal(DraftStatus.PendingApproval, postDraftRepository.InsertedDraft.Status);
        Assert.Equal(NewsItemStatus.Selected, newsItemRepository.LastUpdatedStatus);
    }

    [Fact]
    public async Task ApproveDraft_Should_AutoPublish_When_Mode_Is_Automatic()
    {
        var postDraftRepository = new FakePostDraftRepository
        {
            Draft = new PostDraft
            {
                Id = 10,
                NewsItemId = 20,
                PostText = """
                OpenAI expands workflow execution

                Hook

                What happened:
                OpenAI expanded workflow execution tooling.

                Why it matters:
                This affects workflow execution and task completion.

                Strategic takeaway:
                The real shift is that AI is becoming an execution layer inside workflows.

                Source: OpenAI News
                """
            }
        };
        var newsItemRepository = new FakeNewsItemRepository
        {
            NewsItem = new NewsItem
            {
                Id = 20,
                SourceId = 1,
                Title = "OpenAI expands workflow execution",
                CanonicalUrl = "https://example.com/openai-workflows",
                Url = "https://example.com/openai-workflows"
            }
        };
        var publicationRepository = new FakePublicationRepository();
        var linkedInPublisher = new FakeLinkedInPublisher
        {
            ValidateResult = new OperationResult { Success = true },
            PublishResult = new LinkedInPublishResult
            {
                Success = true,
                PlatformPostId = "linkedin-123",
                RequestPayload = "{\"text\":\"payload\"}",
                ResponsePayload = "{\"id\":\"linkedin-123\"}"
            }
        };

        var service = CreateService(
            publishMode: "Automatic",
            newsItemRepository: newsItemRepository,
            postDraftRepository: postDraftRepository,
            publicationRepository: publicationRepository,
            linkedInPublisher: linkedInPublisher);

        var success = await service.ApproveDraftAsync(10, "ops-ui", CancellationToken.None);

        Assert.True(success);
        Assert.NotNull(publicationRepository.InsertedPublication);
        Assert.Equal(PublicationStatus.Published, publicationRepository.InsertedPublication.Status);
        Assert.Equal(DraftStatus.Published, postDraftRepository.UpdatedDraft?.Status);
        Assert.Contains("Original article: https://example.com/openai-workflows", postDraftRepository.UpdatedDraft?.PostText);
    }

    private static NewsPipelineService CreateService(
        string publishMode = "Manual",
        IEnumerable<INewsCollector>? collectors = null,
        ISourceRepository? sourceRepository = null,
        FakeNewsItemRepository? newsItemRepository = null,
        ICurationResultRepository? curationResultRepository = null,
        FakePostDraftRepository? postDraftRepository = null,
        FakePublicationRepository? publicationRepository = null,
        IExecutionRunRepository? executionRunRepository = null,
        FakeAiCurationService? aiCurationService = null,
        FakeLinkedInPublisher? linkedInPublisher = null)
    {
        return new NewsPipelineService(
            collectors ?? [],
            sourceRepository ?? new FakeSourceRepository(),
            newsItemRepository ?? new FakeNewsItemRepository(),
            curationResultRepository ?? new FakeCurationResultRepository(),
            postDraftRepository ?? new FakePostDraftRepository(),
            publicationRepository ?? new FakePublicationRepository(),
            executionRunRepository ?? new FakeExecutionRunRepository(),
            aiCurationService ?? new FakeAiCurationService(),
            linkedInPublisher ?? new FakeLinkedInPublisher(),
            Options.Create(new AppOptions
            {
                PublishMode = publishMode,
                LinkedInTone = "Professional",
                ConfidenceThreshold = 0.8,
                RelevanceThreshold = 0.75,
                DuplicateLookbackDays = 7,
                NewsWindowHours = 24,
                MaxCandidatesForAi = 20,
                AttributionFooterLine = "Curated by AI News Curator."
            }),
            NullLogger<NewsPipelineService>.Instance);
    }

    private sealed class FakeSourceRepository : ISourceRepository
    {
        public Task<IReadOnlyList<Source>> GetAllAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Source>>([]);
        public Task<IReadOnlyList<Source>> GetActiveAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Source>>([]);
        public Task<Source?> GetByIdAsync(long id, CancellationToken cancellationToken) => Task.FromResult<Source?>(new Source { Id = id, Name = "OpenAI News" });
        public Task<long> InsertAsync(Source source, CancellationToken cancellationToken) => Task.FromResult(1L);
        public Task SeedDefaultsAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task UpdateAsync(Source source, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeNewsItemRepository : INewsItemRepository
    {
        public NewsItem? NewsItem { get; set; }
        public NewsItemStatus? LastUpdatedStatus { get; private set; }

        public Task<bool> ExistsRecentSimilarAsync(string titleHash, string contentHash, int lookbackDays, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<NewsItem?> GetByCanonicalUrlAsync(string canonicalUrl, CancellationToken cancellationToken) => Task.FromResult<NewsItem?>(null);
        public Task<NewsItem?> GetByIdAsync(long id, CancellationToken cancellationToken) => Task.FromResult(NewsItem);
        public Task<IReadOnlyList<NewsItem>> GetCandidatesForCurationAsync(int maxItems, DateTimeOffset publishedAfter, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<NewsItem>>([]);
        public Task<NewsItem?> GetPublishedByCanonicalUrlAsync(string canonicalUrl, CancellationToken cancellationToken) => Task.FromResult<NewsItem?>(null);
        public Task<IReadOnlyList<NewsItem>> GetRecentAsync(int maxItems, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<NewsItem>>([]);
        public Task<IReadOnlyList<NewsItem>> GetWithoutImageAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<NewsItem>>([]);
        public Task<long> InsertAsync(NewsItem newsItem, CancellationToken cancellationToken)
        {
            NewsItem = newsItem;
            return Task.FromResult(1L);
        }
        public Task<int> NormalizeStoredContentAsync(CancellationToken cancellationToken) => Task.FromResult(0);
        public Task UpdateImageAsync(long id, string imageUrl, string imageOrigin, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task UpdateStatusAsync(long id, NewsItemStatus status, CancellationToken cancellationToken)
        {
            LastUpdatedStatus = status;
            if (NewsItem is not null)
            {
                NewsItem.Status = status;
            }
            return Task.CompletedTask;
        }
    }

    private sealed class FakeCurationResultRepository : ICurationResultRepository
    {
        public CurationResult? LastInserted { get; private set; }
        public Task<CurationResult?> GetLatestByNewsItemIdAsync(long newsItemId, CancellationToken cancellationToken) => Task.FromResult<CurationResult?>(null);
        public Task<long> InsertAsync(CurationResult result, CancellationToken cancellationToken)
        {
            LastInserted = result;
            return Task.FromResult(1L);
        }
    }

    private sealed class FakePostDraftRepository : IPostDraftRepository
    {
        public PostDraft? Draft { get; set; }
        public PostDraft? InsertedDraft { get; private set; }
        public PostDraft? UpdatedDraft { get; private set; }

        public Task<IReadOnlyList<PostDraft>> GetAllEditableAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<PostDraft>>(Draft is null ? [] : [Draft]);
        public Task<PostDraft?> GetByIdAsync(long id, CancellationToken cancellationToken) => Task.FromResult(Draft);
        public Task<IReadOnlyList<PostDraft>> GetByNewsItemIdAsync(long newsItemId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<PostDraft>>(Draft is null ? [] : [Draft]);
        public Task<IReadOnlyList<PostDraft>> GetPendingApprovalAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<PostDraft>>(Draft is null ? [] : [Draft]);
        public Task<long> InsertAsync(PostDraft draft, CancellationToken cancellationToken)
        {
            draft.Id = 99;
            InsertedDraft = draft;
            Draft = draft;
            return Task.FromResult(99L);
        }
        public Task UpdateAsync(PostDraft draft, CancellationToken cancellationToken)
        {
            UpdatedDraft = draft;
            Draft = draft;
            return Task.CompletedTask;
        }
    }

    private sealed class FakePublicationRepository : IPublicationRepository
    {
        public Publication? InsertedPublication { get; private set; }
        public Task<IReadOnlyList<Publication>> GetByDraftIdAsync(long postDraftId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Publication>>([]);
        public Task<Publication?> GetLatestByDraftIdAsync(long postDraftId, CancellationToken cancellationToken) => Task.FromResult<Publication?>(null);
        public Task<long> InsertAsync(Publication publication, CancellationToken cancellationToken)
        {
            InsertedPublication = publication;
            return Task.FromResult(1L);
        }
    }

    private sealed class FakeExecutionRunRepository : IExecutionRunRepository
    {
        public Task<IReadOnlyList<ExecutionRun>> GetRecentAsync(int maxItems, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ExecutionRun>>([]);
        public Task<long> InsertAsync(ExecutionRun run, CancellationToken cancellationToken) => Task.FromResult(1L);
        public Task UpdateAsync(ExecutionRun run, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeAiCurationService : IAiCurationService
    {
        public AiEvaluationResult EvaluationResult { get; set; } = new();
        public PostValidationResult ValidationResult { get; set; } = new();

        public Task<AiEvaluationResult> EvaluateNewsAsync(NewsItem newsItem, CancellationToken cancellationToken) => Task.FromResult(EvaluationResult);
        public Task<string> GenerateLinkedInPostAsync(NewsItem newsItem, CurationResult curationResult, CancellationToken cancellationToken) => Task.FromResult(EvaluationResult.LinkedInDraft);
        public Task<PostValidationResult> ValidatePostAsync(NewsItem newsItem, string postText, CancellationToken cancellationToken) => Task.FromResult(ValidationResult);
    }

    private sealed class FakeLinkedInPublisher : ILinkedInPublisher
    {
        public OperationResult ValidateResult { get; set; } = new() { Success = true };
        public LinkedInPublishResult PublishResult { get; set; } = new() { Success = true };

        public Task<LinkedInPublishResult> PublishTextPostAsync(PostDraft draft, CancellationToken cancellationToken) => Task.FromResult(PublishResult);
        public Task<OperationResult> RefreshAccessAsync(CancellationToken cancellationToken) => Task.FromResult(new OperationResult { Success = true });
        public Task<OperationResult> ValidateCredentialsAsync(CancellationToken cancellationToken) => Task.FromResult(ValidateResult);
    }
}

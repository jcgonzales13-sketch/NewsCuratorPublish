using System.Text.Json;
using AiNewsCurator.Application.DTOs;
using AiNewsCurator.Application.Services;
using AiNewsCurator.Domain.Entities;
using AiNewsCurator.Domain.Enums;
using AiNewsCurator.Domain.Interfaces;
using AiNewsCurator.Infrastructure.Persistence;
using AiNewsCurator.Infrastructure.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AiNewsCurator.IntegrationTests;

public sealed class PipelineIntegrationTests : IAsyncLifetime
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"ainews-tests-{Guid.NewGuid():N}.db");
    private SqliteConnectionFactory _connectionFactory = default!;

    public async Task InitializeAsync()
    {
        var options = Options.Create(new AppOptions
        {
            DatabasePath = _databasePath
        });

        _connectionFactory = new SqliteConnectionFactory(options);
        var initializer = new SqliteDatabaseInitializer(_connectionFactory, NullLogger<SqliteDatabaseInitializer>.Instance);
        await initializer.InitializeAsync(CancellationToken.None);

        var sourceRepository = new SourceRepository(_connectionFactory);
        await sourceRepository.SeedDefaultsAsync(CancellationToken.None);
    }

    public Task DisposeAsync()
    {
        try
        {
            if (File.Exists(_databasePath))
            {
                File.Delete(_databasePath);
            }
        }
        catch (IOException)
        {
        }

        return Task.CompletedTask;
    }

    [Fact]
    public async Task Manual_Mode_Should_Create_Pending_Draft_And_Curation_Record()
    {
        var pipeline = CreatePipeline(
            publishMode: "Manual",
            collectedItems:
            [
                new CollectedNewsItem
                {
                    ExternalId = "item-1",
                    Title = "OpenAI launches new agentic AI workflow",
                    Url = "https://example.com/news/openai-agentic-workflow?utm_source=rss",
                    CanonicalUrl = "https://example.com/news/openai-agentic-workflow",
                    PublishedAt = DateTimeOffset.UtcNow,
                    Language = "en",
                    RawSummary = "A new workflow improves agentic orchestration for software teams.",
                    RawContent = "A new workflow improves agentic orchestration for software teams."
                }
            ],
            aiResult: new AiEvaluationResult
            {
                IsRelevant = true,
                RelevanceScore = 0.92,
                ConfidenceScore = 0.86,
                Category = "Agents",
                WhyRelevant = "Impacta times de tecnologia e produto.",
                Summary = "Resumo factual.",
                KeyPoints = ["Ponto 1", "Ponto 2"],
                LinkedInTitleSuggestion = "OpenAI pushes agent workflows into execution",
                LinkedInDraft = "Uma noticia recente sobre IA mostra avancos em agentes para software com impacto real em operacao e produtividade."
            });

        await pipeline.RunCollectAsync(new TriggerContext { TriggerType = TriggerType.Manual, InitiatedBy = "test" }, CancellationToken.None);
        await pipeline.RunCurateAsync(new TriggerContext { TriggerType = TriggerType.Manual, InitiatedBy = "test" }, CancellationToken.None);

        var draftCount = await ExecuteScalarAsync("SELECT COUNT(*) FROM PostDrafts WHERE Status = @Status", ("@Status", (int)DraftStatus.PendingApproval));
        var curationCount = await ExecuteScalarAsync("SELECT COUNT(*) FROM CurationResults");
        var selectedCount = await ExecuteScalarAsync("SELECT COUNT(*) FROM NewsItems WHERE Status = @Status", ("@Status", (int)NewsItemStatus.Selected));

        Assert.Equal(1, draftCount);
        Assert.Equal(1, curationCount);
        Assert.Equal(1, selectedCount);
    }

    [Fact]
    public async Task Automatic_Mode_Should_Deduplicate_And_Publish_Draft()
    {
        var collectedItems =
            new[]
            {
                new CollectedNewsItem
                {
                    ExternalId = "dup-1",
                    Title = "Google expands enterprise AI tooling",
                    Url = "https://example.com/news/google-ai-enterprise?ref=feed",
                    CanonicalUrl = "https://example.com/news/google-ai-enterprise",
                    PublishedAt = DateTimeOffset.UtcNow,
                    Language = "en",
                    RawSummary = "Enterprise AI tooling reaches more teams.",
                    RawContent = "Enterprise AI tooling reaches more teams."
                }
            };

        var pipeline = CreatePipeline(
            publishMode: "Automatic",
            collectedItems,
            aiResult: new AiEvaluationResult
            {
                IsRelevant = true,
                RelevanceScore = 0.95,
                ConfidenceScore = 0.91,
                Category = "AI",
                WhyRelevant = "Tem impacto de negocio.",
                Summary = "Resumo factual.",
                KeyPoints = ["Ponto 1"],
                LinkedInTitleSuggestion = "Google expands enterprise AI deployment",
                LinkedInDraft = "Uma noticia recente sobre IA destaca a expansao de tooling corporativo com impacto concreto para times de negocio e tecnologia."
            });

        await pipeline.RunCollectAsync(new TriggerContext { TriggerType = TriggerType.Manual, InitiatedBy = "test" }, CancellationToken.None);
        await pipeline.RunCollectAsync(new TriggerContext { TriggerType = TriggerType.Manual, InitiatedBy = "test" }, CancellationToken.None);
        await pipeline.RunCurateAsync(new TriggerContext { TriggerType = TriggerType.Manual, InitiatedBy = "test" }, CancellationToken.None);

        var newsCount = await ExecuteScalarAsync("SELECT COUNT(*) FROM NewsItems");
        var publicationCount = await ExecuteScalarAsync("SELECT COUNT(*) FROM Publications WHERE Status = @Status", ("@Status", (int)PublicationStatus.Published));
        var publishedDraftCount = await ExecuteScalarAsync("SELECT COUNT(*) FROM PostDrafts WHERE Status = @Status", ("@Status", (int)DraftStatus.Published));

        Assert.Equal(1, newsCount);
        Assert.Equal(1, publicationCount);
        Assert.Equal(1, publishedDraftCount);
    }

    [Fact]
    public async Task Reprocess_Should_Create_A_New_Draft_For_Unpublished_News()
    {
        var pipeline = CreatePipeline(
            publishMode: "Manual",
            collectedItems:
            [
                new CollectedNewsItem
                {
                    ExternalId = "item-reprocess",
                    Title = "Anthropic expands AI tooling for developers",
                    Url = "https://example.com/news/anthropic-developers",
                    CanonicalUrl = "https://example.com/news/anthropic-developers",
                    PublishedAt = DateTimeOffset.UtcNow,
                    Language = "en",
                    RawSummary = "The update broadens AI workflows for developers.",
                    RawContent = "The update broadens AI workflows for developers."
                }
            ],
            aiResult: new AiEvaluationResult
            {
                IsRelevant = true,
                RelevanceScore = 0.89,
                ConfidenceScore = 0.84,
                Category = "AI",
                WhyRelevant = "Impacta times de desenvolvimento.",
                Summary = "Resumo factual.",
                KeyPoints = ["Ponto 1"],
                LinkedInTitleSuggestion = "Anthropic broadens developer AI tooling",
                LinkedInDraft = "Uma noticia recente sobre IA mostra uma ampliacao relevante de tooling para desenvolvedores, com efeito pratico em fluxos de entrega."
            });

        await pipeline.RunCollectAsync(new TriggerContext { TriggerType = TriggerType.Manual, InitiatedBy = "test" }, CancellationToken.None);
        await pipeline.RunCurateAsync(new TriggerContext { TriggerType = TriggerType.Manual, InitiatedBy = "test" }, CancellationToken.None);

        var newsItemId = await ExecuteInt64ScalarAsync("SELECT Id FROM NewsItems ORDER BY Id DESC LIMIT 1", null);
        var reprocessed = await pipeline.ReprocessNewsItemAsync(newsItemId, "test-reprocess", CancellationToken.None);
        var draftCount = await ExecuteScalarAsync("SELECT COUNT(*) FROM PostDrafts WHERE NewsItemId = @NewsItemId", ("@NewsItemId", newsItemId));

        Assert.True(reprocessed);
        Assert.Equal(2, draftCount);
    }

    [Fact]
    public async Task Regenerate_Existing_Drafts_Should_Refresh_Old_Draft_Format()
    {
        var pipeline = CreatePipeline(
            publishMode: "Manual",
            collectedItems:
            [
                new CollectedNewsItem
                {
                    ExternalId = "item-regenerate",
                    Title = "Anthropic expands automation tooling",
                    Url = "https://example.com/news/anthropic-automation",
                    CanonicalUrl = "https://example.com/news/anthropic-automation",
                    PublishedAt = DateTimeOffset.UtcNow,
                    Language = "en",
                    RawSummary = "Anthropic expanded tooling so agents can act more directly in user workflows.",
                    RawContent = "Anthropic expanded tooling so agents can act more directly in user workflows."
                }
            ],
            aiResult: new AiEvaluationResult
            {
                IsRelevant = true,
                RelevanceScore = 0.91,
                ConfidenceScore = 0.87,
                Category = "Agents",
                WhyRelevant = "Impacta operacao e produto.",
                Summary = "Resumo factual.",
                KeyPoints = ["Ponto 1"],
                LinkedInTitleSuggestion = "Anthropic pushes AI closer to execution",
                LinkedInDraft = "Legacy summary draft without editorial sections but with AI context for teams."
            });

        await pipeline.RunCollectAsync(new TriggerContext { TriggerType = TriggerType.Manual, InitiatedBy = "test" }, CancellationToken.None);
        await pipeline.RunCurateAsync(new TriggerContext { TriggerType = TriggerType.Manual, InitiatedBy = "test" }, CancellationToken.None);

        var regenerated = await pipeline.RegenerateExistingDraftsAsync("test-normalize", CancellationToken.None);
        var postText = await ExecuteStringScalarAsync("SELECT PostText FROM PostDrafts ORDER BY Id DESC LIMIT 1", null);
        var title = await ExecuteStringScalarAsync("SELECT TitleSuggestion FROM PostDrafts ORDER BY Id DESC LIMIT 1", null);

        Assert.Equal(1, regenerated);
        Assert.Contains("What happened:", postText);
        Assert.Contains("Why it matters:", postText);
        Assert.Contains("Strategic takeaway:", postText);
        Assert.Equal("Anthropic pushes AI closer to execution", title);
    }

    [Fact]
    public async Task Dismiss_Draft_Should_Remove_It_From_Pending_Queue()
    {
        var pipeline = CreatePipeline(
            publishMode: "Manual",
            collectedItems:
            [
                new CollectedNewsItem
                {
                    ExternalId = "item-dismiss",
                    Title = "OpenAI expands enterprise orchestration tools",
                    Url = "https://example.com/news/openai-enterprise-orchestration",
                    CanonicalUrl = "https://example.com/news/openai-enterprise-orchestration",
                    PublishedAt = DateTimeOffset.UtcNow,
                    Language = "en",
                    RawSummary = "A broader orchestration release is now available for enterprise teams.",
                    RawContent = "A broader orchestration release is now available for enterprise teams."
                }
            ],
            aiResult: new AiEvaluationResult
            {
                IsRelevant = true,
                RelevanceScore = 0.9,
                ConfidenceScore = 0.86,
                Category = "Agents",
                WhyRelevant = "Impacta operacao e entrega.",
                Summary = "Resumo factual.",
                KeyPoints = ["Ponto 1"],
                LinkedInTitleSuggestion = "OpenAI expands orchestration for enterprises",
                LinkedInDraft = "Uma noticia recente sobre IA destaca um avanco em orquestracao com impacto pratico para times corporativos."
            });

        await pipeline.RunCollectAsync(new TriggerContext { TriggerType = TriggerType.Manual, InitiatedBy = "test" }, CancellationToken.None);
        await pipeline.RunCurateAsync(new TriggerContext { TriggerType = TriggerType.Manual, InitiatedBy = "test" }, CancellationToken.None);

        var draftId = await ExecuteInt64ScalarAsync("SELECT Id FROM PostDrafts ORDER BY Id DESC LIMIT 1", null);
        var dismissed = await pipeline.DismissDraftAsync(draftId, "test-dismiss", CancellationToken.None);
        var queueCount = await ExecuteScalarAsync(
            "SELECT COUNT(*) FROM PostDrafts WHERE Status IN (@PendingApproval, @Approved)",
            ("@PendingApproval", (int)DraftStatus.PendingApproval),
            ("@Approved", (int)DraftStatus.Approved));
        var dismissedCount = await ExecuteScalarAsync("SELECT COUNT(*) FROM PostDrafts WHERE Status = @Status", ("@Status", (int)DraftStatus.Dismissed));

        Assert.True(dismissed);
        Assert.Equal(0, queueCount);
        Assert.Equal(1, dismissedCount);
    }

    [Fact]
    public async Task Reopen_Draft_Should_Move_Dismissed_Item_Back_To_Review_Queue()
    {
        var pipeline = CreatePipeline(
            publishMode: "Manual",
            collectedItems:
            [
                new CollectedNewsItem
                {
                    ExternalId = "item-reopen",
                    Title = "Anthropic adds workflow automation controls",
                    Url = "https://example.com/news/anthropic-workflow-controls",
                    CanonicalUrl = "https://example.com/news/anthropic-workflow-controls",
                    PublishedAt = DateTimeOffset.UtcNow,
                    Language = "en",
                    RawSummary = "Anthropic now offers more control over workflow automation for teams.",
                    RawContent = "Anthropic now offers more control over workflow automation for teams."
                }
            ],
            aiResult: new AiEvaluationResult
            {
                IsRelevant = true,
                RelevanceScore = 0.88,
                ConfidenceScore = 0.83,
                Category = "Agents",
                WhyRelevant = "Impacta operacao e governanca.",
                Summary = "Resumo factual.",
                KeyPoints = ["Ponto 1"],
                LinkedInTitleSuggestion = "Anthropic expands workflow controls",
                LinkedInDraft = "Uma noticia recente sobre IA mostra mais controle operacional em automacoes com impacto para equipes."
            });

        await pipeline.RunCollectAsync(new TriggerContext { TriggerType = TriggerType.Manual, InitiatedBy = "test" }, CancellationToken.None);
        await pipeline.RunCurateAsync(new TriggerContext { TriggerType = TriggerType.Manual, InitiatedBy = "test" }, CancellationToken.None);

        var draftId = await ExecuteInt64ScalarAsync("SELECT Id FROM PostDrafts ORDER BY Id DESC LIMIT 1", null);
        var dismissed = await pipeline.DismissDraftAsync(draftId, "test-dismiss", CancellationToken.None);
        var reopened = await pipeline.ReopenDraftAsync(draftId, "test-reopen", CancellationToken.None);
        var status = await ExecuteScalarAsync("SELECT Status FROM PostDrafts WHERE Id = @Id", ("@Id", draftId));
        var queueCount = await ExecuteScalarAsync(
            "SELECT COUNT(*) FROM PostDrafts WHERE Status IN (@PendingApproval, @Approved)",
            ("@PendingApproval", (int)DraftStatus.PendingApproval),
            ("@Approved", (int)DraftStatus.Approved));

        Assert.True(dismissed);
        Assert.True(reopened);
        Assert.Equal((int)DraftStatus.PendingApproval, status);
        Assert.Equal(1, queueCount);
    }

    [Fact]
    public async Task Retry_Publish_Should_Allow_Failed_Draft_To_Be_Published_On_Second_Attempt()
    {
        var pipeline = CreatePipeline(
            publishMode: "Manual",
            collectedItems:
            [
                new CollectedNewsItem
                {
                    ExternalId = "item-retry-publish",
                    Title = "Google expands orchestration controls for enterprise AI",
                    Url = "https://example.com/news/google-orchestration-controls",
                    CanonicalUrl = "https://example.com/news/google-orchestration-controls",
                    PublishedAt = DateTimeOffset.UtcNow,
                    Language = "en",
                    RawSummary = "Enterprise teams now get more orchestration controls in AI workflows.",
                    RawContent = "Enterprise teams now get more orchestration controls in AI workflows."
                }
            ],
            aiResult: new AiEvaluationResult
            {
                IsRelevant = true,
                RelevanceScore = 0.9,
                ConfidenceScore = 0.86,
                Category = "AI",
                WhyRelevant = "Impacta operacao e governanca.",
                Summary = "Resumo factual.",
                KeyPoints = ["Ponto 1"],
                LinkedInTitleSuggestion = "Google expands enterprise orchestration controls",
                LinkedInDraft = "A new AI update expands orchestration controls for enterprise teams with practical operational impact."
            },
            linkedInPublisher: new FlakyLinkedInPublisher());

        await pipeline.RunCollectAsync(new TriggerContext { TriggerType = TriggerType.Manual, InitiatedBy = "test" }, CancellationToken.None);
        await pipeline.RunCurateAsync(new TriggerContext { TriggerType = TriggerType.Manual, InitiatedBy = "test" }, CancellationToken.None);

        var draftId = await ExecuteInt64ScalarAsync("SELECT Id FROM PostDrafts ORDER BY Id DESC LIMIT 1", null);
        var approved = await pipeline.ApproveDraftAsync(draftId, "test-approve", CancellationToken.None);
        var firstPublishAttempt = await pipeline.PublishDraftAsync(draftId, "test-first-publish", CancellationToken.None);
        var retryPublished = await pipeline.PublishDraftAsync(draftId, "test-retry", CancellationToken.None);
        var draftStatus = await ExecuteScalarAsync("SELECT Status FROM PostDrafts WHERE Id = @Id", ("@Id", draftId));
        var publicationCount = await ExecuteScalarAsync("SELECT COUNT(*) FROM Publications WHERE PostDraftId = @DraftId", ("@DraftId", draftId));

        Assert.True(approved);
        Assert.False(firstPublishAttempt);
        Assert.True(retryPublished);
        Assert.Equal((int)DraftStatus.Published, draftStatus);
        Assert.Equal(2, publicationCount);
    }

    [Fact]
    public async Task SourceRepository_Should_Insert_And_List_Custom_Source()
    {
        var repository = new SourceRepository(_connectionFactory);
        var source = new Source
        {
            Name = "Custom RSS",
            Type = SourceType.Rss,
            Url = "https://example.com/rss.xml",
            Language = "en",
            IsActive = true,
            Priority = 6,
            MaxItemsPerRun = 5,
            IncludeKeywordsJson = "[\"ai\"]",
            ExcludeKeywordsJson = "[]",
            TagsJson = "[\"custom\"]",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var id = await repository.InsertAsync(source, CancellationToken.None);
        var sources = await repository.GetAllAsync(CancellationToken.None);

        Assert.True(id > 0);
        Assert.Contains(sources, item => item.Id == id && item.Name == "Custom RSS");
    }

    [Fact]
    public async Task SourceRepository_Should_Update_And_Deactivate_Source()
    {
        var repository = new SourceRepository(_connectionFactory);
        var source = new Source
        {
            Name = "Mutable RSS",
            Type = SourceType.Rss,
            Url = "https://example.com/mutable.xml",
            Language = "en",
            IsActive = true,
            Priority = 4,
            MaxItemsPerRun = 5,
            IncludeKeywordsJson = "[]",
            ExcludeKeywordsJson = "[]",
            TagsJson = "[]",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var id = await repository.InsertAsync(source, CancellationToken.None);
        var persisted = await repository.GetByIdAsync(id, CancellationToken.None);

        Assert.NotNull(persisted);

        persisted!.Name = "Mutable RSS Updated";
        persisted.Priority = 9;
        persisted.IsActive = false;
        persisted.UpdatedAt = DateTimeOffset.UtcNow;
        await repository.UpdateAsync(persisted, CancellationToken.None);

        var updated = await repository.GetByIdAsync(id, CancellationToken.None);
        var activeSources = await repository.GetActiveAsync(CancellationToken.None);

        Assert.NotNull(updated);
        Assert.Equal("Mutable RSS Updated", updated!.Name);
        Assert.Equal(9, updated.Priority);
        Assert.False(updated.IsActive);
        Assert.DoesNotContain(activeSources, item => item.Id == id);
    }

    private NewsPipelineService CreatePipeline(
        string publishMode,
        IReadOnlyList<CollectedNewsItem> collectedItems,
        AiEvaluationResult aiResult,
        ILinkedInPublisher? linkedInPublisher = null)
    {
        var options = Options.Create(new AppOptions
        {
            DatabasePath = _databasePath,
            PublishMode = publishMode,
            RelevanceThreshold = 0.75,
            ConfidenceThreshold = 0.80,
            DuplicateLookbackDays = 14,
            MaxCandidatesForAi = 10,
            NewsWindowHours = 48
        });

        return new NewsPipelineService(
            [new FakeCollector(collectedItems)],
            new SourceRepository(_connectionFactory),
            new NewsItemRepository(_connectionFactory),
            new CurationResultRepository(_connectionFactory),
            new PostDraftRepository(_connectionFactory),
            new PublicationRepository(_connectionFactory),
            new ExecutionRunRepository(_connectionFactory),
            new FakeAiCurationService(aiResult),
            linkedInPublisher ?? new FakeLinkedInPublisher(),
            options,
            NullLogger<NewsPipelineService>.Instance);
    }

    private async Task<int> ExecuteScalarAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = await _connectionFactory.OpenAsync(CancellationToken.None);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var parameter in parameters)
        {
            command.Parameters.AddWithValue(parameter.Name, parameter.Value);
        }

        return Convert.ToInt32(await command.ExecuteScalarAsync(CancellationToken.None));
    }

    private async Task<long> ExecuteInt64ScalarAsync(string sql, (string Name, object Value)? parameter)
    {
        await using var connection = await _connectionFactory.OpenAsync(CancellationToken.None);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        if (parameter.HasValue)
        {
            command.Parameters.AddWithValue(parameter.Value.Name, parameter.Value.Value);
        }

        return Convert.ToInt64(await command.ExecuteScalarAsync(CancellationToken.None));
    }

    private async Task<string> ExecuteStringScalarAsync(string sql, (string Name, object Value)? parameter)
    {
        await using var connection = await _connectionFactory.OpenAsync(CancellationToken.None);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        if (parameter.HasValue)
        {
            command.Parameters.AddWithValue(parameter.Value.Name, parameter.Value.Value);
        }

        return Convert.ToString(await command.ExecuteScalarAsync(CancellationToken.None)) ?? string.Empty;
    }

    private sealed class FakeCollector : INewsCollector
    {
        private readonly IReadOnlyList<CollectedNewsItem> _items;

        public FakeCollector(IReadOnlyList<CollectedNewsItem> items)
        {
            _items = items;
        }

        public bool CanHandle(Source source) => true;

        public Task<IReadOnlyList<CollectedNewsItem>> CollectAsync(Source source, CancellationToken cancellationToken)
        {
            return Task.FromResult(_items);
        }
    }

    private sealed class FakeAiCurationService : IAiCurationService
    {
        private readonly AiEvaluationResult _result;

        public FakeAiCurationService(AiEvaluationResult result)
        {
            _result = result;
        }

        public Task<AiEvaluationResult> EvaluateNewsAsync(NewsItem newsItem, CancellationToken cancellationToken)
        {
            return Task.FromResult(_result);
        }

        public Task<string> GenerateLinkedInPostAsync(NewsItem newsItem, CurationResult curationResult, CancellationToken cancellationToken)
        {
            return Task.FromResult(_result.LinkedInDraft);
        }

        public Task<PostValidationResult> ValidatePostAsync(NewsItem newsItem, string postText, CancellationToken cancellationToken)
        {
            return Task.FromResult(new PostValidationResult());
        }
    }

    private sealed class FakeLinkedInPublisher : ILinkedInPublisher
    {
        public Task<LinkedInPublishResult> PublishTextPostAsync(PostDraft draft, CancellationToken cancellationToken)
        {
            return Task.FromResult(new LinkedInPublishResult
            {
                Success = true,
                PlatformPostId = "linkedin-post-1",
                RequestPayload = JsonSerializer.Serialize(new { draft.PostText }),
                ResponsePayload = JsonSerializer.Serialize(new { id = "linkedin-post-1" })
            });
        }

        public Task<OperationResult> ValidateCredentialsAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(OperationResult.Ok());
        }

        public Task<OperationResult> RefreshAccessAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(OperationResult.Ok());
        }
    }

    private sealed class FlakyLinkedInPublisher : ILinkedInPublisher
    {
        private int _attempts;

        public Task<LinkedInPublishResult> PublishTextPostAsync(PostDraft draft, CancellationToken cancellationToken)
        {
            _attempts++;
            if (_attempts == 1)
            {
                return Task.FromResult(new LinkedInPublishResult
                {
                    Success = false,
                    ErrorMessage = "Transient LinkedIn failure",
                    RequestPayload = JsonSerializer.Serialize(new { draft.PostText }),
                    ResponsePayload = JsonSerializer.Serialize(new { error = "temporary_failure" })
                });
            }

            return Task.FromResult(new LinkedInPublishResult
            {
                Success = true,
                PlatformPostId = "linkedin-post-retry-success",
                RequestPayload = JsonSerializer.Serialize(new { draft.PostText }),
                ResponsePayload = JsonSerializer.Serialize(new { id = "linkedin-post-retry-success" })
            });
        }

        public Task<OperationResult> ValidateCredentialsAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(OperationResult.Ok());
        }

        public Task<OperationResult> RefreshAccessAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(OperationResult.Ok());
        }
    }
}

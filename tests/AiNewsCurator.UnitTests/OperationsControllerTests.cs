using AiNewsCurator.Api.Controllers;
using AiNewsCurator.Api.Models.Operations;
using AiNewsCurator.Api.Operations;
using AiNewsCurator.Application.DTOs;
using AiNewsCurator.Application.Interfaces;
using AiNewsCurator.Application.Services;
using AiNewsCurator.Domain.Entities;
using AiNewsCurator.Domain.Enums;
using AiNewsCurator.Domain.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace AiNewsCurator.UnitTests;

public sealed class OperationsControllerTests
{
    [Fact]
    public void Login_Get_Should_Sanitize_ReturnUrl()
    {
        var controller = CreateController(isAuthenticated: false);

        var result = Assert.IsType<ViewResult>(controller.Login("https://example.com/evil"));
        var model = Assert.IsType<OperationsLoginViewModel>(result.Model);

        Assert.Equal("/ops", model.ReturnUrl);
    }

    [Theory]
    [InlineData("//evil.com")]
    [InlineData("/\\evil")]
    [InlineData("/ops\\drafts")]
    public void Login_Get_Should_Reject_Non_Local_ReturnUrl_Shapes(string returnUrl)
    {
        var controller = CreateController(isAuthenticated: false);

        var result = Assert.IsType<ViewResult>(controller.Login(returnUrl));
        var model = Assert.IsType<OperationsLoginViewModel>(result.Model);

        Assert.Equal("/ops", model.ReturnUrl);
    }

    [Fact]
    public void Login_Get_Should_Show_Session_Expired_Message()
    {
        var controller = CreateController(isAuthenticated: false);

        var result = Assert.IsType<ViewResult>(controller.Login("/ops", null, null, "session-expired"));
        var model = Assert.IsType<OperationsLoginViewModel>(result.Model);

        Assert.Equal("Your session expired. Please sign in again.", model.InfoMessage);
    }

    [Fact]
    public async Task LoginWithPassword_Should_Return_Error_When_Password_Is_Invalid()
    {
        var controller = CreateController(appOptions: new AppOptions { OpsAdminPassword = "secret123" });

        var result = Assert.IsType<ViewResult>(await controller.LoginWithPassword(new LoginOpsFormModel
        {
            Email = "OPS@Example.com ",
            Password = "wrong",
            ReturnUrl = "/ops"
        }));
        var model = Assert.IsType<OperationsLoginViewModel>(result.Model);

        Assert.Equal("ops@example.com", model.Email);
        Assert.Equal("Invalid email or password.", model.ErrorMessage);
    }

    [Fact]
    public async Task UpdateDraft_Should_Reject_Empty_Text()
    {
        var draftRepository = new FakePostDraftRepository
        {
            Draft = new PostDraft
            {
                Id = 10,
                NewsItemId = 20,
                Status = DraftStatus.PendingApproval,
                PostText = "Existing text",
                ValidationErrorsJson = "[]"
            }
        };

        var controller = CreateController(postDraftRepository: draftRepository);

        var result = Assert.IsType<RedirectResult>(await controller.UpdateDraft(
            10,
            new UpdateDraftFormModel
            {
                TitleSuggestion = "Updated title",
                PostText = "   "
            },
            "/ops",
            CancellationToken.None));

        Assert.Equal("/ops", result.Url);
        Assert.Equal("Draft text cannot be empty.", controller.TempData["FlashMessage"]);
        Assert.Equal("error", controller.TempData["FlashType"]);
    }

    [Fact]
    public async Task UpdateDraft_Should_Save_Validation_Warnings_And_Reset_Approved_Draft()
    {
        var draftRepository = new FakePostDraftRepository
        {
            Draft = new PostDraft
            {
                Id = 10,
                NewsItemId = 20,
                Status = DraftStatus.Approved,
                ApprovedAt = DateTimeOffset.UtcNow,
                ApprovedBy = "reviewer",
                PostText = "Existing text",
                ValidationErrorsJson = "[]"
            }
        };
        var newsRepository = new FakeNewsItemRepository
        {
            NewsItem = new NewsItem
            {
                Id = 20,
                Title = "Title"
            }
        };
        var aiService = new FakeAiCurationService
        {
            ValidationResult = new PostValidationResult
            {
            }
        };
        aiService.ValidationResult.Errors.Add("Needs revision");

        var controller = CreateController(
            postDraftRepository: draftRepository,
            newsItemRepository: newsRepository,
            aiCurationService: aiService);

        var result = Assert.IsType<RedirectResult>(await controller.UpdateDraft(
            10,
            new UpdateDraftFormModel
            {
                TitleSuggestion = "Updated title",
                PostText = "Updated post text"
            },
            "/ops",
            CancellationToken.None));

        Assert.Equal("/ops", result.Url);
        Assert.NotNull(draftRepository.UpdatedDraft);
        Assert.Equal(DraftStatus.PendingApproval, draftRepository.UpdatedDraft.Status);
        Assert.Null(draftRepository.UpdatedDraft.ApprovedAt);
        Assert.Null(draftRepository.UpdatedDraft.ApprovedBy);
        Assert.Contains("Needs revision", draftRepository.UpdatedDraft.ValidationErrorsJson);
        Assert.Equal("Draft updated, but it still has editorial review notes.", controller.TempData["FlashMessage"]);
    }

    [Fact]
    public async Task UpdateNewsImage_Should_Reject_Invalid_Url()
    {
        var newsRepository = new FakeNewsItemRepository
        {
            NewsItem = new NewsItem
            {
                Id = 7,
                Title = "News"
            }
        };
        var controller = CreateController(newsItemRepository: newsRepository);

        var result = Assert.IsType<RedirectResult>(await controller.UpdateNewsImage(
            7,
            new UpdateNewsImageFormModel { ImageUrl = "not-a-url" },
            "/ops",
            CancellationToken.None));

        Assert.Equal("/ops", result.Url);
        Assert.Equal("Enter a valid absolute image URL.", controller.TempData["FlashMessage"]);
        Assert.Equal("error", controller.TempData["FlashType"]);
    }

    [Fact]
    public async Task ValidateLinkedIn_Should_Set_Error_Flash_When_Validation_Fails()
    {
        var publisher = new FakeLinkedInPublisher
        {
            ValidateResult = new OperationResult
            {
                Success = false,
                ErrorMessage = "token expired"
            }
        };
        var controller = CreateController(linkedInPublisher: publisher);

        var result = Assert.IsType<RedirectResult>(await controller.ValidateLinkedIn("/ops", CancellationToken.None));

        Assert.Equal("/ops", result.Url);
        Assert.Equal("LinkedIn authorization expired or lost permission. Reconnect LinkedIn and validate the connection again.", controller.TempData["FlashMessage"]);
        Assert.Equal("error", controller.TempData["FlashType"]);
    }

    [Fact]
    public async Task RefreshLinkedIn_Should_Set_Success_Flash_When_Refresh_Succeeds()
    {
        var publisher = new FakeLinkedInPublisher
        {
            RefreshResult = new OperationResult
            {
                Success = true
            }
        };
        var controller = CreateController(linkedInPublisher: publisher);

        var result = Assert.IsType<RedirectResult>(await controller.RefreshLinkedIn("/ops", CancellationToken.None));

        Assert.Equal("/ops", result.Url);
        Assert.Equal("LinkedIn access token refreshed successfully.", controller.TempData["FlashMessage"]);
        Assert.Equal("success", controller.TempData["FlashType"]);
    }

    [Fact]
    public async Task RefreshContent_Should_Run_Complete_Content_Flow()
    {
        var pipeline = new FakeNewsPipelineService();
        var newsRepository = new FakeNewsItemRepository { NormalizeCount = 2 };
        var imageService = new FakeNewsImageEnrichmentService { BackfillCount = 3 };
        var controller = CreateController(
            pipelineService: pipeline,
            newsItemRepository: newsRepository,
            newsImageEnrichmentService: imageService);

        var result = Assert.IsType<RedirectResult>(await controller.RefreshContent("/ops", CancellationToken.None));

        Assert.Equal("/ops", result.Url);
        Assert.Equal(1, pipeline.CollectCalls);
        Assert.Equal(1, pipeline.CurateCalls);
        Assert.Equal(1, pipeline.RegenerateExistingCalls);
        Assert.Equal(1, newsRepository.NormalizeCalls);
        Assert.Equal(1, imageService.BackfillCalls);
        Assert.Contains("Refresh completed.", controller.TempData["FlashMessage"]?.ToString());
    }

    private static OperationsController CreateController(
        INewsPipelineService? pipelineService = null,
        FakePostDraftRepository? postDraftRepository = null,
        IExecutionRunRepository? executionRunRepository = null,
        ISourceRepository? sourceRepository = null,
        FakeNewsItemRepository? newsItemRepository = null,
        ICurationResultRepository? curationResultRepository = null,
        IPublicationRepository? publicationRepository = null,
        ILinkedInAuthService? linkedInAuthService = null,
        FakeLinkedInPublisher? linkedInPublisher = null,
        FakeAiCurationService? aiCurationService = null,
        INewsImageEnrichmentService? newsImageEnrichmentService = null,
        AppOptions? appOptions = null,
        bool isAuthenticated = true)
    {
        var controller = new OperationsController(
            pipelineService ?? new FakeNewsPipelineService(),
            postDraftRepository ?? new FakePostDraftRepository(),
            executionRunRepository ?? new FakeExecutionRunRepository(),
            sourceRepository ?? new FakeSourceRepository(),
            newsItemRepository ?? new FakeNewsItemRepository(),
            curationResultRepository ?? new FakeCurationResultRepository(),
            publicationRepository ?? new FakePublicationRepository(),
            linkedInAuthService ?? new FakeLinkedInAuthService(),
            linkedInPublisher ?? new FakeLinkedInPublisher(),
            aiCurationService ?? new FakeAiCurationService(),
            newsImageEnrichmentService ?? new FakeNewsImageEnrichmentService(),
            Options.Create(appOptions ?? new AppOptions { OpsAdminPassword = "secret123" }));

        var httpContext = new DefaultHttpContext();
        httpContext.User = isAuthenticated
            ? new ClaimsPrincipal(
                new ClaimsIdentity(
                    [new Claim(ClaimTypes.Email, "ops@example.com"), new Claim(OpsCookieAuthenticationDefaults.UserIdClaim, "1")],
                    OpsCookieAuthenticationDefaults.Scheme))
            : new ClaimsPrincipal(new ClaimsIdentity());
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
        controller.TempData = new TempDataDictionary(httpContext, new FakeTempDataProvider());
        return controller;
    }

    private sealed class FakeTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }

    private sealed class FakeNewsPipelineService : INewsPipelineService
    {
        public int CollectCalls { get; private set; }
        public int CurateCalls { get; private set; }
        public int RegenerateExistingCalls { get; private set; }

        public Task<bool> ApproveDraftAsync(long draftId, string approvedBy, CancellationToken cancellationToken) => Task.FromResult(true);
        public Task<bool> CreateManualDraftAsync(long newsItemId, string requestedBy, CancellationToken cancellationToken) => Task.FromResult(true);
        public Task<bool> DismissDraftAsync(long draftId, string approvedBy, CancellationToken cancellationToken) => Task.FromResult(true);
        public Task<bool> PublishDraftAsync(long draftId, string approvedBy, CancellationToken cancellationToken) => Task.FromResult(true);
        public Task<bool> RegenerateDraftAsync(long draftId, string requestedBy, CancellationToken cancellationToken) => Task.FromResult(true);
        public Task<bool> ReplaceDraftAsync(long draftId, string requestedBy, CancellationToken cancellationToken) => Task.FromResult(true);
        public Task<int> RegenerateExistingDraftsAsync(string requestedBy, CancellationToken cancellationToken)
        {
            RegenerateExistingCalls++;
            return Task.FromResult(4);
        }
        public Task<bool> RejectDraftAsync(long draftId, string approvedBy, CancellationToken cancellationToken) => Task.FromResult(true);
        public Task<bool> ReopenDraftAsync(long draftId, string approvedBy, CancellationToken cancellationToken) => Task.FromResult(true);
        public Task<bool> ReprocessNewsItemAsync(long newsItemId, string requestedBy, CancellationToken cancellationToken) => Task.FromResult(true);
        public Task<int> RunCollectAsync(TriggerContext triggerContext, CancellationToken cancellationToken)
        {
            CollectCalls++;
            return Task.FromResult(5);
        }
        public Task<int> RunCurateAsync(TriggerContext triggerContext, CancellationToken cancellationToken)
        {
            CurateCalls++;
            return Task.FromResult(6);
        }
        public Task<DailyRunResult> RunDailyAsync(TriggerContext triggerContext, CancellationToken cancellationToken) => Task.FromResult(new DailyRunResult());
    }

    private sealed class FakePostDraftRepository : IPostDraftRepository
    {
        public PostDraft? Draft { get; set; }
        public PostDraft? UpdatedDraft { get; private set; }

        public Task<IReadOnlyList<PostDraft>> GetAllEditableAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<PostDraft>>([]);
        public Task<PostDraft?> GetByIdAsync(long id, CancellationToken cancellationToken) => Task.FromResult(Draft);
        public Task<IReadOnlyList<PostDraft>> GetByNewsItemIdAsync(long newsItemId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<PostDraft>>([]);
        public Task<IReadOnlyList<PostDraft>> GetPendingApprovalAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<PostDraft>>([]);
        public Task<long> InsertAsync(PostDraft draft, CancellationToken cancellationToken) => Task.FromResult(1L);
        public Task UpdateAsync(PostDraft draft, CancellationToken cancellationToken)
        {
            UpdatedDraft = draft;
            Draft = draft;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeExecutionRunRepository : IExecutionRunRepository
    {
        public Task<IReadOnlyList<ExecutionRun>> GetRecentAsync(int maxItems, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ExecutionRun>>([]);
        public Task<long> InsertAsync(ExecutionRun run, CancellationToken cancellationToken) => Task.FromResult(1L);
        public Task UpdateAsync(ExecutionRun run, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeSourceRepository : ISourceRepository
    {
        public Task<IReadOnlyList<Source>> GetActiveAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Source>>([]);
        public Task<IReadOnlyList<Source>> GetAllAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Source>>([]);
        public Task<Source?> GetByIdAsync(long id, CancellationToken cancellationToken) => Task.FromResult<Source?>(null);
        public Task<long> InsertAsync(Source source, CancellationToken cancellationToken) => Task.FromResult(1L);
        public Task SeedDefaultsAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task UpdateAsync(Source source, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeNewsItemRepository : INewsItemRepository
    {
        public NewsItem? NewsItem { get; set; }
        public int NormalizeCount { get; set; }
        public int NormalizeCalls { get; private set; }

        public Task<bool> ExistsRecentSimilarAsync(string titleHash, string contentHash, int lookbackDays, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<NewsItem?> GetByCanonicalUrlAsync(string canonicalUrl, CancellationToken cancellationToken) => Task.FromResult<NewsItem?>(null);
        public Task<NewsItem?> GetByIdAsync(long id, CancellationToken cancellationToken) => Task.FromResult(NewsItem);
        public Task<IReadOnlyList<NewsItem>> GetCandidatesForCurationAsync(int maxItems, DateTimeOffset publishedAfter, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<NewsItem>>([]);
        public Task<NewsItem?> GetPublishedByCanonicalUrlAsync(string canonicalUrl, CancellationToken cancellationToken) => Task.FromResult<NewsItem?>(null);
        public Task<IReadOnlyList<NewsItem>> GetRecentAsync(int maxItems, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<NewsItem>>([]);
        public Task<IReadOnlyList<NewsItem>> GetWithoutImageAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<NewsItem>>([]);
        public Task<long> InsertAsync(NewsItem newsItem, CancellationToken cancellationToken) => Task.FromResult(1L);
        public Task<int> NormalizeStoredContentAsync(CancellationToken cancellationToken)
        {
            NormalizeCalls++;
            return Task.FromResult(NormalizeCount);
        }
        public Task UpdateImageAsync(long id, string imageUrl, string imageOrigin, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task UpdateStatusAsync(long id, NewsItemStatus status, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeCurationResultRepository : ICurationResultRepository
    {
        public Task<CurationResult?> GetLatestByNewsItemIdAsync(long newsItemId, CancellationToken cancellationToken) => Task.FromResult<CurationResult?>(null);
        public Task<long> InsertAsync(CurationResult result, CancellationToken cancellationToken) => Task.FromResult(1L);
    }

    private sealed class FakePublicationRepository : IPublicationRepository
    {
        public Task<IReadOnlyList<Publication>> GetByDraftIdAsync(long postDraftId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Publication>>([]);
        public Task<Publication?> GetLatestByDraftIdAsync(long postDraftId, CancellationToken cancellationToken) => Task.FromResult<Publication?>(null);
        public Task<long> InsertAsync(Publication publication, CancellationToken cancellationToken) => Task.FromResult(1L);
    }

    private sealed class FakeLinkedInAuthService : ILinkedInAuthService
    {
        public Task<string> CreateAuthorizationUrlAsync(CancellationToken cancellationToken) => Task.FromResult("https://example.com/linkedin");
        public Task<LinkedInAuthCallbackResult> HandleCallbackAsync(string code, string state, string? error, string? errorDescription, CancellationToken cancellationToken) => Task.FromResult(new LinkedInAuthCallbackResult { Success = true });
        public Task<LinkedInAuthState> GetStatusAsync(CancellationToken cancellationToken) => Task.FromResult(new LinkedInAuthState());
    }

    private sealed class FakeLinkedInPublisher : ILinkedInPublisher
    {
        public OperationResult ValidateResult { get; set; } = new() { Success = true };
        public OperationResult RefreshResult { get; set; } = new() { Success = true };

        public Task<LinkedInPublishResult> PublishTextPostAsync(PostDraft draft, CancellationToken cancellationToken) => Task.FromResult(new LinkedInPublishResult { Success = true });
        public Task<OperationResult> RefreshAccessAsync(CancellationToken cancellationToken) => Task.FromResult(RefreshResult);
        public Task<OperationResult> ValidateCredentialsAsync(CancellationToken cancellationToken) => Task.FromResult(ValidateResult);
    }

    private sealed class FakeAiCurationService : IAiCurationService
    {
        public PostValidationResult ValidationResult { get; set; } = new();

        public Task<AiEvaluationResult> EvaluateNewsAsync(NewsItem newsItem, CancellationToken cancellationToken) => Task.FromResult(new AiEvaluationResult());
        public Task<string> GenerateLinkedInPostAsync(NewsItem newsItem, CurationResult curationResult, CancellationToken cancellationToken) => Task.FromResult(string.Empty);
        public Task<PostValidationResult> ValidatePostAsync(NewsItem newsItem, string postText, CancellationToken cancellationToken) => Task.FromResult(ValidationResult);
    }

    private sealed class FakeNewsImageEnrichmentService : INewsImageEnrichmentService
    {
        public int BackfillCount { get; set; }
        public int BackfillCalls { get; private set; }
        public Task<int> BackfillMissingImagesAsync(CancellationToken cancellationToken)
        {
            BackfillCalls++;
            return Task.FromResult(BackfillCount);
        }
    }
}

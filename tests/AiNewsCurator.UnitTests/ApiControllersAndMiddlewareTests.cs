using System.Text.Json;
using AiNewsCurator.Api.Controllers;
using AiNewsCurator.Api.Contracts;
using AiNewsCurator.Api.Middleware;
using AiNewsCurator.Api.Operations;
using AiNewsCurator.Application.DTOs;
using AiNewsCurator.Application.Interfaces;
using AiNewsCurator.Domain.Entities;
using AiNewsCurator.Domain.Enums;
using AiNewsCurator.Domain.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AiNewsCurator.UnitTests;

public sealed class ApiControllersAndMiddlewareTests
{
    [Fact]
    public void HealthController_Get_Should_Return_Healthy_Status()
    {
        var controller = new HealthController();

        var result = Assert.IsType<OkObjectResult>(controller.Get());
        var payload = JsonSerializer.Serialize(result.Value);

        Assert.Contains("\"status\":\"Healthy\"", payload, StringComparison.Ordinal);
        Assert.Contains("\"timestamp\":", payload, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LinkedInAuthController_Callback_Should_Redirect_To_Ops_And_Forward_Callback_Data()
    {
        var authService = new FakeLinkedInAuthService();
        var controller = new LinkedInAuthController(authService, new FakeLinkedInPublisher());

        var result = await controller.Callback("auth-code", "state-1", null, null, CancellationToken.None);

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal("/ops", redirect.Url);
        Assert.Equal("auth-code", authService.LastCode);
        Assert.Equal("state-1", authService.LastState);
    }

    [Fact]
    public async Task LinkedInAuthController_Validate_Should_Return_BadRequest_When_Validation_Fails()
    {
        var controller = new LinkedInAuthController(
            new FakeLinkedInAuthService(),
            new FakeLinkedInPublisher
            {
                ValidateResult = new OperationResult
                {
                    Success = false,
                    ErrorMessage = "invalid token"
                }
            });

        var result = await controller.Validate(CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var payload = Assert.IsType<OperationResult>(badRequest.Value);
        Assert.False(payload.Success);
        Assert.Equal("invalid token", payload.ErrorMessage);
    }

    [Fact]
    public async Task InternalApiKeyMiddleware_Should_Reject_Invalid_Internal_Request()
    {
        var invoked = false;
        var context = new DefaultHttpContext();
        context.Request.Path = "/internal/run/daily";
        context.Response.Body = new MemoryStream();

        var middleware = new InternalApiKeyMiddleware(
            _ =>
            {
                invoked = true;
                return Task.CompletedTask;
            },
            Options.Create(new AppOptions { InternalApiKey = "secret" }));

        await middleware.InvokeAsync(context);
        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();

        Assert.False(invoked);
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        Assert.Contains("Invalid API key.", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OperationsAccessMiddleware_Should_Redirect_Unauthenticated_Ops_Request_To_Login()
    {
        var invoked = false;
        var context = new DefaultHttpContext();
        context.Request.Path = "/ops";
        context.Request.QueryString = new QueryString("?drafts=review");
        context.RequestServices = new ServiceCollection()
            .AddSingleton<IAuthenticationService>(new FakeAuthenticationService())
            .BuildServiceProvider();

        var middleware = new OperationsAccessMiddleware(
            _ =>
            {
                invoked = true;
                return Task.CompletedTask;
            });

        await middleware.InvokeAsync(context);

        Assert.False(invoked);
        Assert.Equal(StatusCodes.Status302Found, context.Response.StatusCode);
        Assert.Equal("/ops/login?reason=session-expired&returnUrl=%2Fops%3Fdrafts%3Dreview", context.Response.Headers.Location.ToString());
    }

    [Fact]
    public async Task OperationsAccessMiddleware_Should_Use_SeeOther_For_Unauthenticated_Post_Request()
    {
        var invoked = false;
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = "/ops/actions/daily";
        context.RequestServices = new ServiceCollection()
            .AddSingleton<IAuthenticationService>(new FakeAuthenticationService())
            .BuildServiceProvider();

        var middleware = new OperationsAccessMiddleware(_ =>
        {
            invoked = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        Assert.False(invoked);
        Assert.Equal(StatusCodes.Status303SeeOther, context.Response.StatusCode);
        Assert.Equal("/ops/login?reason=session-expired&returnUrl=%2Fops%2Factions%2Fdaily", context.Response.Headers.Location.ToString());
    }

    [Fact]
    public async Task OperationsAccessMiddleware_Should_Allow_Authenticated_Ops_Request()
    {
        var invoked = false;
        var context = new DefaultHttpContext();
        context.Request.Path = "/ops";
        context.RequestServices = new ServiceCollection()
            .AddSingleton<IAuthenticationService>(new FakeAuthenticationService(BuildPrincipal()))
            .BuildServiceProvider();

        var middleware = new OperationsAccessMiddleware(_ =>
        {
            invoked = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        Assert.True(invoked);
        Assert.True(context.User.Identity?.IsAuthenticated);
        Assert.Equal("ops@example.com", context.User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value);
    }

    [Fact]
    public async Task InternalRunsController_Sources_Should_Filter_By_Resolved_Profile()
    {
        var controller = new InternalRunsController(
            new FakeNewsPipelineService(),
            new FakePostDraftRepository(),
            new FakeExecutionRunRepository(),
            new FakeNewsItemRepository(),
            new FakeCurationResultRepository(),
            new FakeSourceRepository(
                new Source
                {
                    Id = 1,
                    Name = "OpenAI News",
                    Url = "https://openai.com/news/rss.xml",
                    TagsJson = "[\"ai\"]",
                    IncludeKeywordsJson = "[\"ai\"]"
                },
                new Source
                {
                    Id = 2,
                    Name = ".NET Blog",
                    Url = "https://devblogs.microsoft.com/dotnet/feed/",
                    TagsJson = "[\"dotnet\"]",
                    IncludeKeywordsJson = "[\"c#\"]"
                }),
            new FakeNewsImageEnrichmentService());

        var result = await controller.Sources("dotnet", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsAssignableFrom<IReadOnlyList<SourceResponse>>(ok.Value);
        var item = Assert.Single(response);
        Assert.Equal(2, item.Source.Id);
        Assert.Equal("dotnet", item.EditorialProfile);
    }

    private sealed class FakeLinkedInAuthService : ILinkedInAuthService
    {
        public string LastCode { get; private set; } = string.Empty;
        public string LastState { get; private set; } = string.Empty;

        public Task<string> CreateAuthorizationUrlAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult("https://example.com/linkedin-auth");
        }

        public Task<LinkedInAuthCallbackResult> HandleCallbackAsync(string code, string state, string? error, string? errorDescription, CancellationToken cancellationToken)
        {
            LastCode = code;
            LastState = state;
            return Task.FromResult(new LinkedInAuthCallbackResult { Success = true });
        }

        public Task<LinkedInAuthState> GetStatusAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(new LinkedInAuthState());
        }
    }

    private static System.Security.Claims.ClaimsPrincipal BuildPrincipal()
    {
        return new(
            new System.Security.Claims.ClaimsIdentity(
                [
                    new(System.Security.Claims.ClaimTypes.Email, "ops@example.com"),
                    new(OpsCookieAuthenticationDefaults.UserIdClaim, "1")
                ],
                OpsCookieAuthenticationDefaults.Scheme));
    }

    private sealed class FakeAuthenticationService : IAuthenticationService
    {
        private readonly System.Security.Claims.ClaimsPrincipal? _principal;

        public FakeAuthenticationService(System.Security.Claims.ClaimsPrincipal? principal = null)
        {
            _principal = principal;
        }

        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme)
        {
            if (_principal is null)
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            return Task.FromResult(
                AuthenticateResult.Success(
                    new AuthenticationTicket(_principal, OpsCookieAuthenticationDefaults.Scheme)));
        }

        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
        public Task SignInAsync(HttpContext context, string? scheme, System.Security.Claims.ClaimsPrincipal principal, AuthenticationProperties? properties) => Task.CompletedTask;
        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
    }

    private sealed class FakeLinkedInPublisher : ILinkedInPublisher
    {
        public OperationResult ValidateResult { get; set; } = new() { Success = true };

        public Task<LinkedInPublishResult> PublishTextPostAsync(PostDraft draft, CancellationToken cancellationToken)
        {
            return Task.FromResult(new LinkedInPublishResult { Success = true });
        }

        public Task<OperationResult> RefreshAccessAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(new OperationResult { Success = true });
        }

        public Task<OperationResult> ValidateCredentialsAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(ValidateResult);
        }
    }

    private sealed class FakeNewsPipelineService : INewsPipelineService
    {
        public Task<bool> ApproveDraftAsync(long draftId, string approvedBy, CancellationToken cancellationToken) => Task.FromResult(true);
        public Task<bool> CreateManualDraftAsync(long newsItemId, string requestedBy, CancellationToken cancellationToken) => Task.FromResult(true);
        public Task<bool> DismissDraftAsync(long draftId, string approvedBy, CancellationToken cancellationToken) => Task.FromResult(true);
        public Task<bool> PublishDraftAsync(long draftId, string approvedBy, CancellationToken cancellationToken) => Task.FromResult(true);
        public Task<bool> RegenerateDraftAsync(long draftId, string requestedBy, CancellationToken cancellationToken) => Task.FromResult(true);
        public Task<bool> ReplaceDraftAsync(long draftId, string requestedBy, CancellationToken cancellationToken) => Task.FromResult(true);
        public Task<int> RegenerateExistingDraftsAsync(string requestedBy, CancellationToken cancellationToken) => Task.FromResult(0);
        public Task<bool> RejectDraftAsync(long draftId, string approvedBy, CancellationToken cancellationToken) => Task.FromResult(true);
        public Task<bool> ReopenDraftAsync(long draftId, string approvedBy, CancellationToken cancellationToken) => Task.FromResult(true);
        public Task<bool> ReprocessNewsItemAsync(long newsItemId, string requestedBy, CancellationToken cancellationToken) => Task.FromResult(true);
        public Task<int> RunCollectAsync(TriggerContext triggerContext, CancellationToken cancellationToken) => Task.FromResult(0);
        public Task<int> RunCurateAsync(TriggerContext triggerContext, CancellationToken cancellationToken) => Task.FromResult(0);
        public Task<DailyRunResult> RunDailyAsync(TriggerContext triggerContext, CancellationToken cancellationToken) => Task.FromResult(new DailyRunResult());
    }

    private sealed class FakePostDraftRepository : IPostDraftRepository
    {
        public Task<IReadOnlyList<PostDraft>> GetAllEditableAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<PostDraft>>([]);
        public Task<PostDraft?> GetByIdAsync(long id, CancellationToken cancellationToken) => Task.FromResult<PostDraft?>(null);
        public Task<IReadOnlyList<PostDraft>> GetByNewsItemIdAsync(long newsItemId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<PostDraft>>([]);
        public Task<IReadOnlyList<PostDraft>> GetPendingApprovalAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<PostDraft>>([]);
        public Task<long> InsertAsync(PostDraft draft, CancellationToken cancellationToken) => Task.FromResult(1L);
        public Task UpdateAsync(PostDraft draft, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeExecutionRunRepository : IExecutionRunRepository
    {
        public Task<IReadOnlyList<ExecutionRun>> GetRecentAsync(int maxItems, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ExecutionRun>>([]);
        public Task<long> InsertAsync(ExecutionRun run, CancellationToken cancellationToken) => Task.FromResult(1L);
        public Task UpdateAsync(ExecutionRun run, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeNewsItemRepository : INewsItemRepository
    {
        public Task<bool> ExistsRecentSimilarAsync(string titleHash, string contentHash, int lookbackDays, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<NewsItem?> GetByCanonicalUrlAsync(string canonicalUrl, CancellationToken cancellationToken) => Task.FromResult<NewsItem?>(null);
        public Task<NewsItem?> GetByIdAsync(long id, CancellationToken cancellationToken) => Task.FromResult<NewsItem?>(null);
        public Task<IReadOnlyList<NewsItem>> GetCandidatesForCurationAsync(int maxItems, DateTimeOffset publishedAfter, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<NewsItem>>([]);
        public Task<NewsItem?> GetPublishedByCanonicalUrlAsync(string canonicalUrl, CancellationToken cancellationToken) => Task.FromResult<NewsItem?>(null);
        public Task<IReadOnlyList<NewsItem>> GetRecentAsync(int maxItems, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<NewsItem>>([]);
        public Task<IReadOnlyList<NewsItem>> GetWithoutImageAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<NewsItem>>([]);
        public Task<long> InsertAsync(NewsItem newsItem, CancellationToken cancellationToken) => Task.FromResult(1L);
        public Task<int> NormalizeStoredContentAsync(CancellationToken cancellationToken) => Task.FromResult(0);
        public Task UpdateImageAsync(long id, string imageUrl, string imageOrigin, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task UpdateStatusAsync(long id, NewsItemStatus status, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeCurationResultRepository : ICurationResultRepository
    {
        public Task<CurationResult?> GetLatestByNewsItemIdAsync(long newsItemId, CancellationToken cancellationToken) => Task.FromResult<CurationResult?>(null);
        public Task<long> InsertAsync(CurationResult result, CancellationToken cancellationToken) => Task.FromResult(1L);
    }

    private sealed class FakeSourceRepository(params Source[] sources) : ISourceRepository
    {
        private readonly IReadOnlyList<Source> _sources = sources;

        public Task<IReadOnlyList<Source>> GetActiveAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Source>>(_sources.Where(source => source.IsActive).ToList());
        public Task<IReadOnlyList<Source>> GetAllAsync(CancellationToken cancellationToken) => Task.FromResult(_sources);
        public Task<Source?> GetByIdAsync(long id, CancellationToken cancellationToken) => Task.FromResult(_sources.FirstOrDefault(source => source.Id == id));
        public Task<long> InsertAsync(Source source, CancellationToken cancellationToken) => Task.FromResult(1L);
        public Task SeedDefaultsAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task UpdateAsync(Source source, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeNewsImageEnrichmentService : INewsImageEnrichmentService
    {
        public Task<int> BackfillMissingImagesAsync(CancellationToken cancellationToken) => Task.FromResult(0);
    }
}

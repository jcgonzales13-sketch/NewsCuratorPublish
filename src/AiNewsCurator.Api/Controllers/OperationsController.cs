using AiNewsCurator.Api.Models.Operations;
using AiNewsCurator.Api.Operations;
using AiNewsCurator.Application.DTOs;
using AiNewsCurator.Application.Interfaces;
using AiNewsCurator.Domain.Enums;
using AiNewsCurator.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AiNewsCurator.Api.Controllers;

public sealed class OperationsController : Controller
{
    private readonly INewsPipelineService _pipelineService;
    private readonly IPostDraftRepository _postDraftRepository;
    private readonly IExecutionRunRepository _executionRunRepository;
    private readonly ISourceRepository _sourceRepository;
    private readonly INewsItemRepository _newsItemRepository;
    private readonly ICurationResultRepository _curationResultRepository;
    private readonly ILinkedInAuthService _linkedInAuthService;
    private readonly AppOptions _options;

    public OperationsController(
        INewsPipelineService pipelineService,
        IPostDraftRepository postDraftRepository,
        IExecutionRunRepository executionRunRepository,
        ISourceRepository sourceRepository,
        INewsItemRepository newsItemRepository,
        ICurationResultRepository curationResultRepository,
        ILinkedInAuthService linkedInAuthService,
        IOptions<AppOptions> options)
    {
        _pipelineService = pipelineService;
        _postDraftRepository = postDraftRepository;
        _executionRunRepository = executionRunRepository;
        _sourceRepository = sourceRepository;
        _newsItemRepository = newsItemRepository;
        _curationResultRepository = curationResultRepository;
        _linkedInAuthService = linkedInAuthService;
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
            model.ErrorMessage = "API key invalida.";
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
    public IActionResult Logout()
    {
        Response.Cookies.Delete(OperationsAuthCookie.CookieName);
        return RedirectToAction(nameof(Login));
    }

    [HttpGet("/ops")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var drafts = await _postDraftRepository.GetPendingApprovalAsync(cancellationToken);
        var runs = await _executionRunRepository.GetRecentAsync(10, cancellationToken);
        var sources = await _sourceRepository.GetAllAsync(cancellationToken);
        var news = await _newsItemRepository.GetRecentAsync(10, cancellationToken);
        var linkedInStatus = await _linkedInAuthService.GetStatusAsync(cancellationToken);

        var newsItems = new List<OperationsNewsItemViewModel>(news.Count);
        foreach (var item in news)
        {
            var latestCuration = await _curationResultRepository.GetLatestByNewsItemIdAsync(item.Id, cancellationToken);
            newsItems.Add(new OperationsNewsItemViewModel
            {
                NewsItem = item,
                LatestCuration = latestCuration
            });
        }

        return View(new OperationsDashboardViewModel
        {
            Drafts = drafts,
            Runs = runs,
            Sources = sources.OrderByDescending(source => source.IsActive).ThenByDescending(source => source.Priority).ToList(),
            NewsItems = newsItems,
            LinkedInStatus = linkedInStatus,
            FlashMessage = TempData["FlashMessage"] as string,
            FlashIsError = string.Equals(TempData["FlashType"] as string, "error", StringComparison.Ordinal),
            CreateSource = new CreateSourceFormModel()
        });
    }

    [HttpPost("/ops/actions/daily")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RunDaily(CancellationToken cancellationToken)
    {
        var result = await _pipelineService.RunDailyAsync(BuildTriggerContext("ops-daily"), cancellationToken);
        SetFlash($"Rotina diaria concluida. Coletadas: {result.ItemsCollected}. Curadas: {result.ItemsCurated}. Publicadas: {result.ItemsPublished}.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("/ops/actions/collect")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RunCollect(CancellationToken cancellationToken)
    {
        var itemsCollected = await _pipelineService.RunCollectAsync(BuildTriggerContext("ops-collect"), cancellationToken);
        SetFlash($"Coleta concluida com {itemsCollected} itens.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("/ops/actions/curate")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RunCurate(CancellationToken cancellationToken)
    {
        var itemsCurated = await _pipelineService.RunCurateAsync(BuildTriggerContext("ops-curate"), cancellationToken);
        SetFlash($"Curadoria concluida com {itemsCurated} drafts candidatos.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("/ops/drafts/{id:long}/approve")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveDraft(long id, CancellationToken cancellationToken)
    {
        var success = await _pipelineService.ApproveDraftAsync(id, "ops-ui", cancellationToken);
        SetFlash(success ? "Draft aprovado." : "Nao foi possivel aprovar o draft.", !success);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("/ops/drafts/{id:long}/reject")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectDraft(long id, CancellationToken cancellationToken)
    {
        var success = await _pipelineService.RejectDraftAsync(id, "ops-ui", cancellationToken);
        SetFlash(success ? "Draft rejeitado." : "Nao foi possivel rejeitar o draft.", !success);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("/ops/drafts/{id:long}/publish")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PublishDraft(long id, CancellationToken cancellationToken)
    {
        var success = await _pipelineService.PublishDraftAsync(id, "ops-ui", cancellationToken);
        SetFlash(success ? "Draft publicado no LinkedIn." : "Nao foi possivel publicar o draft.", !success);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("/ops/sources")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateSource(CreateSourceFormModel model, CancellationToken cancellationToken)
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
            SetFlash(error ?? "Nao foi possivel criar a fonte.", true);
            return RedirectToAction(nameof(Index));
        }

        source!.CreatedAt = DateTimeOffset.UtcNow;
        source.UpdatedAt = DateTimeOffset.UtcNow;
        await _sourceRepository.InsertAsync(source, cancellationToken);
        SetFlash("Fonte criada com sucesso.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("/ops/sources/{id:long}/toggle")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleSource(long id, CancellationToken cancellationToken)
    {
        var source = await _sourceRepository.GetByIdAsync(id, cancellationToken);
        if (source is null)
        {
            SetFlash("Fonte nao encontrada.", true);
            return RedirectToAction(nameof(Index));
        }

        source.IsActive = !source.IsActive;
        source.UpdatedAt = DateTimeOffset.UtcNow;
        await _sourceRepository.UpdateAsync(source, cancellationToken);
        SetFlash(source.IsActive ? "Fonte ativada." : "Fonte desativada.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("/ops/linkedin/connect")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConnectLinkedIn(CancellationToken cancellationToken)
    {
        var authorizationUrl = await _linkedInAuthService.CreateAuthorizationUrlAsync(cancellationToken);
        return Redirect(authorizationUrl);
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

    private static string SanitizeReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl) || !Uri.TryCreate(returnUrl, UriKind.Relative, out _))
        {
            return "/ops";
        }

        return returnUrl;
    }
}

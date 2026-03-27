using AiNewsCurator.Application.DTOs;
using AiNewsCurator.Application.Interfaces;
using AiNewsCurator.Api.Contracts;
using AiNewsCurator.Api.Operations;
using AiNewsCurator.Domain.Entities;
using AiNewsCurator.Domain.Enums;
using AiNewsCurator.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AiNewsCurator.Api.Controllers;

[ApiController]
[Route("internal")]
public sealed class InternalRunsController : ControllerBase
{
    private readonly INewsPipelineService _pipelineService;
    private readonly IPostDraftRepository _postDraftRepository;
    private readonly IExecutionRunRepository _executionRunRepository;
    private readonly INewsItemRepository _newsItemRepository;
    private readonly ICurationResultRepository _curationResultRepository;
    private readonly ISourceRepository _sourceRepository;
    private readonly INewsImageEnrichmentService _newsImageEnrichmentService;

    public InternalRunsController(
        INewsPipelineService pipelineService,
        IPostDraftRepository postDraftRepository,
        IExecutionRunRepository executionRunRepository,
        INewsItemRepository newsItemRepository,
        ICurationResultRepository curationResultRepository,
        ISourceRepository sourceRepository,
        INewsImageEnrichmentService newsImageEnrichmentService)
    {
        _pipelineService = pipelineService;
        _postDraftRepository = postDraftRepository;
        _executionRunRepository = executionRunRepository;
        _newsItemRepository = newsItemRepository;
        _curationResultRepository = curationResultRepository;
        _sourceRepository = sourceRepository;
        _newsImageEnrichmentService = newsImageEnrichmentService;
    }

    [HttpPost("run/daily")]
    public async Task<IActionResult> RunDaily(CancellationToken cancellationToken)
    {
        var result = await _pipelineService.RunDailyAsync(new TriggerContext
        {
            TriggerType = TriggerType.Manual,
            InitiatedBy = "api"
        }, cancellationToken);

        return Ok(result);
    }

    [HttpPost("run/collect")]
    public async Task<IActionResult> RunCollect(CancellationToken cancellationToken)
    {
        var count = await _pipelineService.RunCollectAsync(new TriggerContext
        {
            TriggerType = TriggerType.Manual,
            InitiatedBy = "api"
        }, cancellationToken);

        return Ok(new { itemsCollected = count });
    }

    [HttpPost("run/curate")]
    public async Task<IActionResult> RunCurate(CancellationToken cancellationToken)
    {
        var count = await _pipelineService.RunCurateAsync(new TriggerContext
        {
            TriggerType = TriggerType.Manual,
            InitiatedBy = "api"
        }, cancellationToken);

        return Ok(new { itemsCurated = count });
    }

    [HttpPost("run/normalize-news")]
    public async Task<IActionResult> NormalizeNews(CancellationToken cancellationToken)
    {
        var itemsNormalized = await _newsItemRepository.NormalizeStoredContentAsync(cancellationToken);
        var imagesEnriched = await _newsImageEnrichmentService.BackfillMissingImagesAsync(cancellationToken);
        return Ok(new { itemsNormalized, imagesEnriched });
    }

    [HttpPost("news/{id:long}/reprocess")]
    public async Task<IActionResult> ReprocessNews(long id, CancellationToken cancellationToken)
    {
        var success = await _pipelineService.ReprocessNewsItemAsync(id, "manual-reprocess", cancellationToken);
        return success ? Ok(new { reprocessed = true }) : BadRequest(new { reprocessed = false });
    }

    [HttpPost("news/{id:long}/draft")]
    public async Task<IActionResult> CreateManualDraft(long id, CancellationToken cancellationToken)
    {
        var success = await _pipelineService.CreateManualDraftAsync(id, "manual-draft", cancellationToken);
        return success ? Ok(new { draftCreated = true }) : BadRequest(new { draftCreated = false });
    }

    [HttpPost("run/publish/{draftId:long}")]
    public async Task<IActionResult> PublishDraft(long draftId, CancellationToken cancellationToken)
    {
        var success = await _pipelineService.PublishDraftAsync(draftId, "manual-publish", cancellationToken);
        return success ? Ok(new { published = true }) : NotFound();
    }

    [HttpGet("drafts")]
    public async Task<IActionResult> Drafts(CancellationToken cancellationToken)
    {
        var drafts = await _postDraftRepository.GetPendingApprovalAsync(cancellationToken);
        return Ok(drafts);
    }

    [HttpPost("drafts/{id:long}/approve")]
    public async Task<IActionResult> Approve(long id, CancellationToken cancellationToken)
    {
        var success = await _pipelineService.ApproveDraftAsync(id, "reviewer", cancellationToken);
        return success ? Ok(new { approved = true }) : NotFound();
    }

    [HttpPost("drafts/{id:long}/reject")]
    public async Task<IActionResult> Reject(long id, CancellationToken cancellationToken)
    {
        var success = await _pipelineService.RejectDraftAsync(id, "reviewer", cancellationToken);
        return success ? Ok(new { rejected = true }) : NotFound();
    }

    [HttpPost("drafts/{id:long}/dismiss")]
    public async Task<IActionResult> Dismiss(long id, CancellationToken cancellationToken)
    {
        var success = await _pipelineService.DismissDraftAsync(id, "reviewer", cancellationToken);
        return success ? Ok(new { dismissed = true }) : NotFound();
    }

    [HttpPost("drafts/{id:long}/reopen")]
    public async Task<IActionResult> Reopen(long id, CancellationToken cancellationToken)
    {
        var success = await _pipelineService.ReopenDraftAsync(id, "reviewer", cancellationToken);
        return success ? Ok(new { reopened = true }) : NotFound();
    }

    [HttpPost("drafts/{id:long}/regenerate")]
    public async Task<IActionResult> Regenerate(long id, CancellationToken cancellationToken)
    {
        var success = await _pipelineService.RegenerateDraftAsync(id, "reviewer", cancellationToken);
        return success ? Ok(new { regenerated = true }) : NotFound();
    }

    [HttpGet("runs")]
    public async Task<IActionResult> Runs(CancellationToken cancellationToken)
    {
        var runs = await _executionRunRepository.GetRecentAsync(20, cancellationToken);
        return Ok(runs);
    }

    [HttpGet("news")]
    public async Task<IActionResult> News(CancellationToken cancellationToken)
    {
        var items = await _newsItemRepository.GetRecentAsync(50, cancellationToken);
        var response = new List<object>(items.Count);

        foreach (var item in items)
        {
            var curation = await _curationResultRepository.GetLatestByNewsItemIdAsync(item.Id, cancellationToken);
            response.Add(new
            {
                newsItem = item,
                latestCuration = curation
            });
        }

        return Ok(response);
    }

    [HttpGet("news/{id:long}")]
    public async Task<IActionResult> NewsById(long id, CancellationToken cancellationToken)
    {
        var item = await _newsItemRepository.GetByIdAsync(id, cancellationToken);
        if (item is null)
        {
            return NotFound();
        }

        var curation = await _curationResultRepository.GetLatestByNewsItemIdAsync(item.Id, cancellationToken);
        return Ok(new
        {
            newsItem = item,
            latestCuration = curation
        });
    }

    [HttpGet("sources")]
    public async Task<IActionResult> Sources([FromQuery] string? profile = null, CancellationToken cancellationToken = default)
    {
        var sources = await _sourceRepository.GetAllAsync(cancellationToken);
        var profileFilter = NormalizeEditorialProfileFilter(profile);
        var response = sources
            .Select(BuildSourceResponse)
            .Where(source => MatchesEditorialProfileFilter(source.EditorialProfile, profileFilter))
            .ToList();
        return Ok(response);
    }

    [HttpPost("sources")]
    public async Task<IActionResult> CreateSource([FromBody] CreateSourceRequest request, CancellationToken cancellationToken)
    {
        var sourceKeywords = SourceInputMapper.ApplyEditorialProfile(
            request.EditorialProfile,
            request.IncludeKeywords,
            request.ExcludeKeywords,
            request.Tags);

        if (!SourceInputMapper.TryBuildSource(
                request.Name,
                request.Type,
                request.Url,
                request.Language,
                request.IsActive,
                request.Priority,
                request.MaxItemsPerRun,
                sourceKeywords.IncludeKeywords,
                sourceKeywords.ExcludeKeywords,
                sourceKeywords.Tags,
                out var source,
                out var error))
        {
            return BadRequest(new { error });
        }

        source!.CreatedAt = DateTimeOffset.UtcNow;
        source.UpdatedAt = DateTimeOffset.UtcNow;
        var sourceId = await _sourceRepository.InsertAsync(source, cancellationToken);
        source.Id = sourceId;
        return CreatedAtAction(nameof(Sources), new { id = sourceId }, BuildSourceResponse(source));
    }

    [HttpPut("sources/{id:long}")]
    public async Task<IActionResult> UpdateSource(long id, [FromBody] UpdateSourceRequest request, CancellationToken cancellationToken)
    {
        var existing = await _sourceRepository.GetByIdAsync(id, cancellationToken);
        if (existing is null)
        {
            return NotFound();
        }

        var sourceKeywords = SourceInputMapper.ApplyEditorialProfile(
            request.EditorialProfile,
            request.IncludeKeywords,
            request.ExcludeKeywords,
            request.Tags);

        if (!SourceInputMapper.TryBuildSource(
                request.Name,
                request.Type,
                request.Url,
                request.Language,
                request.IsActive,
                request.Priority,
                request.MaxItemsPerRun,
                sourceKeywords.IncludeKeywords,
                sourceKeywords.ExcludeKeywords,
                sourceKeywords.Tags,
                out var updated,
                out var error))
        {
            return BadRequest(new { error });
        }

        updated!.Id = existing.Id;
        updated.CreatedAt = existing.CreatedAt;
        updated.UpdatedAt = DateTimeOffset.UtcNow;
        await _sourceRepository.UpdateAsync(updated, cancellationToken);
        return Ok(BuildSourceResponse(updated));
    }

    [HttpPost("sources/{id:long}/deactivate")]
    public async Task<IActionResult> DeactivateSource(long id, CancellationToken cancellationToken)
    {
        var existing = await _sourceRepository.GetByIdAsync(id, cancellationToken);
        if (existing is null)
        {
            return NotFound();
        }

        existing.IsActive = false;
        existing.UpdatedAt = DateTimeOffset.UtcNow;
        await _sourceRepository.UpdateAsync(existing, cancellationToken);
        return Ok(BuildSourceResponse(existing));
    }

    private static SourceResponse BuildSourceResponse(Source source)
    {
        return new SourceResponse
        {
            Source = source,
            EditorialProfile = SourceInputMapper.ResolveEditorialProfile(source),
            EditorialProfileLabel = SourceInputMapper.ResolveEditorialProfileLabel(source)
        };
    }

    private static string NormalizeEditorialProfileFilter(string? profile)
    {
        return profile?.Trim().ToLowerInvariant() switch
        {
            "ai" => "ai",
            "dotnet" => "dotnet",
            _ => "all"
        };
    }

    private static bool MatchesEditorialProfileFilter(string resolvedProfile, string filter)
    {
        return filter switch
        {
            "ai" => string.Equals(resolvedProfile, "ai", StringComparison.OrdinalIgnoreCase),
            "dotnet" => string.Equals(resolvedProfile, "dotnet", StringComparison.OrdinalIgnoreCase),
            _ => true
        };
    }
}

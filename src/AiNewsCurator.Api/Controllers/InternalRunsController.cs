using AiNewsCurator.Application.DTOs;
using AiNewsCurator.Application.Interfaces;
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

    public InternalRunsController(
        INewsPipelineService pipelineService,
        IPostDraftRepository postDraftRepository,
        IExecutionRunRepository executionRunRepository)
    {
        _pipelineService = pipelineService;
        _postDraftRepository = postDraftRepository;
        _executionRunRepository = executionRunRepository;
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

    [HttpGet("runs")]
    public async Task<IActionResult> Runs(CancellationToken cancellationToken)
    {
        var runs = await _executionRunRepository.GetRecentAsync(20, cancellationToken);
        return Ok(runs);
    }
}

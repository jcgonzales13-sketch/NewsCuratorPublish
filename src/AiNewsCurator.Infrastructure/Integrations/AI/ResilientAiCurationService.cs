using AiNewsCurator.Application.DTOs;
using AiNewsCurator.Domain.Entities;
using AiNewsCurator.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AiNewsCurator.Infrastructure.Integrations.AI;

public sealed class ResilientAiCurationService : IAiCurationService
{
    private readonly OpenAiResponsesAiCurationService _openAiService;
    private readonly HeuristicAiCurationService _heuristicService;
    private readonly AppOptions _options;
    private readonly ILogger<ResilientAiCurationService> _logger;

    public ResilientAiCurationService(
        OpenAiResponsesAiCurationService openAiService,
        HeuristicAiCurationService heuristicService,
        IOptions<AppOptions> options,
        ILogger<ResilientAiCurationService> logger)
    {
        _openAiService = openAiService;
        _heuristicService = heuristicService;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<AiEvaluationResult> EvaluateNewsAsync(NewsItem newsItem, CancellationToken cancellationToken)
    {
        if (!UseOpenAi())
        {
            return await _heuristicService.EvaluateNewsAsync(newsItem, cancellationToken);
        }

        try
        {
            return await _openAiService.EvaluateNewsAsync(newsItem, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OpenAI evaluation failed. Falling back to heuristic evaluation.");
            return await _heuristicService.EvaluateNewsAsync(newsItem, cancellationToken);
        }
    }

    public async Task<string> GenerateLinkedInPostAsync(NewsItem newsItem, CurationResult curationResult, CancellationToken cancellationToken)
    {
        if (!UseOpenAi())
        {
            return await _heuristicService.GenerateLinkedInPostAsync(newsItem, curationResult, cancellationToken);
        }

        try
        {
            return await _openAiService.GenerateLinkedInPostAsync(newsItem, curationResult, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OpenAI draft generation failed. Falling back to heuristic draft generation.");
            return await _heuristicService.GenerateLinkedInPostAsync(newsItem, curationResult, cancellationToken);
        }
    }

    public async Task<PostValidationResult> ValidatePostAsync(NewsItem newsItem, string postText, CancellationToken cancellationToken)
    {
        return await _heuristicService.ValidatePostAsync(newsItem, postText, cancellationToken);
    }

    private bool UseOpenAi()
    {
        return string.Equals(_options.AiProvider, "OpenAI", StringComparison.OrdinalIgnoreCase) &&
               !string.IsNullOrWhiteSpace(_options.AiApiKey);
    }
}

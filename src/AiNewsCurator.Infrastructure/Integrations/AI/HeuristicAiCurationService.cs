using System.Text.Json;
using AiNewsCurator.Application.Services;
using AiNewsCurator.Domain.Entities;
using AiNewsCurator.Domain.Interfaces;

namespace AiNewsCurator.Infrastructure.Integrations.AI;

public sealed class HeuristicAiCurationService : IAiCurationService
{
    private static readonly string[] PriorityKeywords =
    [
        "ai", "artificial intelligence", "inteligencia artificial", "llm", "model",
        "agent", "openai", "anthropic", "google", "microsoft", "nvidia", "regulation"
    ];

    public Task<AiEvaluationResult> EvaluateNewsAsync(NewsItem newsItem, CancellationToken cancellationToken)
    {
        var corpus = $"{newsItem.Title} {newsItem.RawSummary} {newsItem.RawContent}".ToLowerInvariant();
        var hits = PriorityKeywords.Count(corpus.Contains);
        var relevance = Math.Clamp(0.55 + (hits * 0.04), 0.0, 0.98);
        var confidence = Math.Clamp(0.65 + (hits * 0.02), 0.0, 0.95);
        var keyPoints = new[]
        {
            $"The headline development is: {newsItem.Title}.",
            "The topic is directly tied to AI and has practical relevance for professionals.",
            "It is worth watching how this evolves for technology, product, and business teams."
        };

        var payload = JsonSerializer.Serialize(new
        {
            newsItem.Title,
            newsItem.CanonicalUrl,
            newsItem.RawSummary
        });

        return Task.FromResult(new AiEvaluationResult
        {
            IsRelevant = relevance >= 0.75,
            RelevanceScore = relevance,
            ConfidenceScore = confidence,
            Category = DetectCategory(corpus),
            WhyRelevant = "This story is relevant to technology and business professionals because it signals a meaningful AI development with practical implications.",
            Summary = newsItem.RawSummary ?? newsItem.Title,
            KeyPoints = keyPoints,
            LinkedInDraft = BuildDraft(newsItem),
            PromptVersion = "heuristic-v1",
            ModelName = "local-heuristic",
            PromptPayload = payload,
            ResponsePayload = JsonSerializer.Serialize(new { relevance, confidence, keyPoints })
        });
    }

    public Task<string> GenerateLinkedInPostAsync(NewsItem newsItem, CurationResult curationResult, CancellationToken cancellationToken)
    {
        return Task.FromResult(BuildDraft(newsItem));
    }

    public Task<PostValidationResult> ValidatePostAsync(NewsItem newsItem, string postText, CancellationToken cancellationToken)
    {
        return Task.FromResult(PostQualityValidator.Validate(postText));
    }

    private static string DetectCategory(string corpus)
    {
        if (corpus.Contains("regulation"))
        {
            return "Regulation";
        }

        if (corpus.Contains("agent"))
        {
            return "Agents";
        }

        if (corpus.Contains("model") || corpus.Contains("llm"))
        {
            return "LLM";
        }

        return "AI";
    }

    private static string BuildDraft(NewsItem newsItem)
    {
        var baseSummary = newsItem.RawSummary ?? newsItem.Title;
        return
            $"One AI story that stood out today is {newsItem.Title}. " +
            $"At a practical level, the key takeaway is this: {baseSummary}. " +
            "What makes this relevant is not just the announcement itself, but the broader signal it sends for product strategy, operational priorities, and competitive positioning. " +
            "This is the kind of development worth tracking closely over the next few weeks.";
    }
}

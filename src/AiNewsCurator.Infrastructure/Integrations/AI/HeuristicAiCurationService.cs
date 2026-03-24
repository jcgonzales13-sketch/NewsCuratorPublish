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
            LinkedInTitleSuggestion = BuildEditorialDraft(newsItem).Headline,
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
        return LinkedInEditorialPostFormatter.BuildPostText(BuildEditorialDraft(newsItem));
    }

    private static LinkedInEditorialDraft BuildEditorialDraft(NewsItem newsItem)
    {
        var title = LinkedInEditorialPostFormatter.SanitizeSentence(newsItem.Title, 90);
        var summary = LinkedInEditorialPostFormatter.SanitizeSentence(newsItem.RawSummary ?? newsItem.Title, 220);
        var draft = new LinkedInEditorialDraft
        {
            Headline = title,
            Hook = "The bigger story here is how quickly AI products are moving toward real execution inside everyday workflows.",
            HookType = "market_signal",
            WhatHappened = summary,
            WhyItMatters = "This moves AI closer to workflow execution instead of just assistance. That matters because teams can judge task completion and operational automation in real environments.",
            StrategicTakeaway = "The real shift is that AI is becoming an execution layer inside workflows, not just a conversational layer on top of them.",
            SourceLabel = "Original reporting",
            Signature = "Curated by AI News Curator."
        };

        return LinkedInEditorialRefiner.Refine(draft);
    }
}

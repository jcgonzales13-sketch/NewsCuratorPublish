using System.Text.Json;
using AiNewsCurator.Application.Services;
using AiNewsCurator.Domain.Entities;
using AiNewsCurator.Domain.Interfaces;

namespace AiNewsCurator.Infrastructure.Integrations.AI;

public sealed class HeuristicAiCurationService : IAiCurationService
{
    private readonly ISourceRepository _sourceRepository;

    public HeuristicAiCurationService(ISourceRepository sourceRepository)
    {
        _sourceRepository = sourceRepository;
    }

    public async Task<AiEvaluationResult> EvaluateNewsAsync(NewsItem newsItem, CancellationToken cancellationToken)
    {
        var source = await _sourceRepository.GetByIdAsync(newsItem.SourceId, cancellationToken);
        var profile = EditorialProfileResolver.Resolve(source, newsItem);
        var corpus = $"{newsItem.Title} {newsItem.RawSummary} {newsItem.RawContent}".ToLowerInvariant();
        var hits = profile.HeuristicKeywords.Count(corpus.Contains);
        var relevanceBase = profile.Name == "dotnet" ? 0.63 : 0.55;
        var confidenceBase = profile.Name == "dotnet" ? 0.69 : 0.65;
        var relevance = Math.Clamp(relevanceBase + (hits * 0.04), 0.0, 0.98);
        var confidence = Math.Clamp(confidenceBase + (hits * 0.02), 0.0, 0.95);
        var keyPoints = new[]
        {
            $"The headline development is: {newsItem.Title}.",
            $"The topic is relevant to {profile.Audience}.",
            "It is worth watching how this evolves for engineering, product, and operational teams."
        };

        var payload = JsonSerializer.Serialize(new
        {
            newsItem.Title,
            newsItem.CanonicalUrl,
            newsItem.RawSummary,
            Profile = profile.Name,
            Source = source?.Name
        });

        return new AiEvaluationResult
        {
            IsRelevant = relevance >= 0.75,
            RelevanceScore = relevance,
            ConfidenceScore = confidence + (profile.Name == "dotnet" ? 0.02 : 0.0),
            Category = DetectCategory(corpus),
            WhyRelevant = profile.HeuristicWhyRelevant,
            Summary = newsItem.RawSummary ?? newsItem.Title,
            KeyPoints = keyPoints,
            LinkedInTitleSuggestion = BuildEditorialDraft(newsItem, profile).Headline,
            LinkedInDraft = BuildDraft(newsItem, profile),
            PromptVersion = $"heuristic-{profile.Name}-v1",
            ModelName = "local-heuristic",
            PromptPayload = payload,
            ResponsePayload = JsonSerializer.Serialize(new { relevance, confidence, profile = profile.Name, keyPoints })
        };
    }

    public Task<string> GenerateLinkedInPostAsync(NewsItem newsItem, CurationResult curationResult, CancellationToken cancellationToken)
    {
        return GenerateDraftInternalAsync(newsItem, cancellationToken);
    }

    public Task<PostValidationResult> ValidatePostAsync(NewsItem newsItem, string postText, CancellationToken cancellationToken)
    {
        return Task.FromResult(PostQualityValidator.Validate(postText));
    }

    private Task<string> GenerateDraftInternalAsync(NewsItem newsItem, CancellationToken cancellationToken)
    {
        return BuildDraftAsync(newsItem, cancellationToken);
    }

    private async Task<string> BuildDraftAsync(NewsItem newsItem, CancellationToken cancellationToken)
    {
        var source = await _sourceRepository.GetByIdAsync(newsItem.SourceId, cancellationToken);
        var profile = EditorialProfileResolver.Resolve(source, newsItem);
        return BuildDraft(newsItem, profile);
    }

    private static string DetectCategory(string corpus)
    {
        if (corpus.Contains(".net") || corpus.Contains("dotnet") || corpus.Contains("c#") || corpus.Contains("asp.net"))
        {
            return "Developer Tools";
        }

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

    private static string BuildDraft(NewsItem newsItem, EditorialProfile profile)
    {
        return LinkedInEditorialPostFormatter.BuildPostText(BuildEditorialDraft(newsItem, profile));
    }

    private static LinkedInEditorialDraft BuildEditorialDraft(NewsItem newsItem, EditorialProfile profile)
    {
        var title = LinkedInEditorialPostFormatter.SanitizeSentence(newsItem.Title, 90);
        var summary = LinkedInEditorialPostFormatter.SanitizeSentence(newsItem.RawSummary ?? newsItem.Title, 220);
        var draft = new LinkedInEditorialDraft
        {
            Headline = title,
            Hook = profile.HeuristicHook,
            HookType = profile.Name == "dotnet" ? "workflow_change" : "market_signal",
            WhatHappened = summary,
            WhyItMatters = profile.HeuristicWhyItMatters,
            StrategicTakeaway = profile.HeuristicTakeaway,
            SourceLabel = profile.SourceLabel,
            OriginalArticleUrl = newsItem.CanonicalUrl,
            Signature = "Curated by AI News Curator."
        };

        return LinkedInEditorialRefiner.Refine(draft);
    }
}

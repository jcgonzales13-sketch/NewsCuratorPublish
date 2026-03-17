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
            $"A noticia destaca: {newsItem.Title}.",
            "O tema tem relacao direta com IA e impacto profissional.",
            "Vale acompanhar o desdobramento para tecnologia e negocios."
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
            WhyRelevant = "Impacta profissionais de tecnologia e negocios com um fato recente sobre IA.",
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
            $"Uma noticia recente sobre IA chamou minha atencao: {newsItem.Title}. " +
            $"Em termos praticos, o ponto central e este: {baseSummary}. " +
            "O que torna esse movimento relevante para empresas e profissionais e a combinacao entre impacto de negocio, velocidade de adocao e necessidade de leitura critica. " +
            "Vale observar como esse tema evolui nas proximas semanas e o que ele sinaliza para produto, operacao e estrategia.";
    }
}

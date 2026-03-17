namespace AiNewsCurator.Domain.Entities;

public sealed class AiEvaluationResult
{
    public bool IsRelevant { get; init; }
    public double RelevanceScore { get; init; }
    public double ConfidenceScore { get; init; }
    public string Category { get; init; } = string.Empty;
    public string WhyRelevant { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public IReadOnlyList<string> KeyPoints { get; init; } = Array.Empty<string>();
    public string LinkedInDraft { get; init; } = string.Empty;
    public string PromptVersion { get; init; } = string.Empty;
    public string ModelName { get; init; } = string.Empty;
    public string PromptPayload { get; init; } = string.Empty;
    public string ResponsePayload { get; init; } = string.Empty;
}

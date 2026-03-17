namespace AiNewsCurator.Domain.Entities;

public sealed class CurationResult
{
    public long Id { get; set; }
    public long NewsItemId { get; set; }
    public double RelevanceScore { get; set; }
    public double ConfidenceScore { get; set; }
    public string Category { get; set; } = string.Empty;
    public string WhyRelevant { get; set; } = string.Empty;
    public bool ShouldPublish { get; set; }
    public string AiSummary { get; set; } = string.Empty;
    public string KeyPointsJson { get; set; } = "[]";
    public string PromptVersion { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public string PromptPayload { get; set; } = string.Empty;
    public string ResponsePayload { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}

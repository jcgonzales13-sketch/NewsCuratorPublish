namespace AiNewsCurator.Infrastructure.Integrations.AI;

internal sealed class AiStructuredOutput
{
    public bool IsRelevant { get; set; }
    public double RelevanceScore { get; set; }
    public double ConfidenceScore { get; set; }
    public string Category { get; set; } = string.Empty;
    public string WhyRelevant { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public List<string> KeyPoints { get; set; } = [];
    public string LinkedInDraft { get; set; } = string.Empty;
}

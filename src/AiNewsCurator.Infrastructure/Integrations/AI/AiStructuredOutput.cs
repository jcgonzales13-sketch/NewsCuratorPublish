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
    public string Headline { get; set; } = string.Empty;
    public string Hook { get; set; } = string.Empty;
    public string HookType { get; set; } = string.Empty;
    public string WhatHappened { get; set; } = string.Empty;
    public string WhyItMatters { get; set; } = string.Empty;
    public string StrategicTakeaway { get; set; } = string.Empty;
    public string SourceLabel { get; set; } = string.Empty;
    public string Signature { get; set; } = string.Empty;
}

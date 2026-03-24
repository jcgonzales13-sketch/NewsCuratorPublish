namespace AiNewsCurator.Application.Services;

public sealed class LinkedInDraftQualityAnalysis
{
    public string ReadabilityScore { get; init; } = "Good";
    public double RedundancyScore { get; init; }
    public double AbstractionScore { get; init; }
    public double TextDensityScore { get; init; }
    public bool HookRepeatsHeadline { get; init; }
    public bool TakeawayTooGeneric { get; init; }
    public bool WhyItMattersTooAbstract { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = [];
}

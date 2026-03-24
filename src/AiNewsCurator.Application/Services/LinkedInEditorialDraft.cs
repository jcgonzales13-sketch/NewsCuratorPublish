namespace AiNewsCurator.Application.Services;

public sealed record LinkedInEditorialDraft
{
    public string Headline { get; init; } = string.Empty;
    public string Hook { get; init; } = string.Empty;
    public string? HookType { get; init; }
    public string WhatHappened { get; init; } = string.Empty;
    public string WhyItMatters { get; init; } = string.Empty;
    public string StrategicTakeaway { get; init; } = string.Empty;
    public string SourceLabel { get; init; } = string.Empty;
    public string Signature { get; init; } = string.Empty;
    public LinkedInDraftQualityAnalysis? QualityAnalysis { get; init; }

    public string ToFeedText()
    {
        return string.Join(
            Environment.NewLine + Environment.NewLine,
            new[] { Headline, Hook, WhatHappened, WhyItMatters, StrategicTakeaway, $"Source: {SourceLabel}", Signature }
                .Where(value => !string.IsNullOrWhiteSpace(value)));
    }
}

using AiNewsCurator.Application.Services;

namespace AiNewsCurator.UnitTests;

public sealed class LinkedInEditorialQualityAnalyzerTests
{
    [Fact]
    public void Analyze_Should_Flag_Generic_And_Abstract_Draft()
    {
        var draft = new LinkedInEditorialDraft
        {
            Headline = "AI changes enterprise workflows",
            Hook = "This is worth watching as AI changes enterprise workflows.",
            WhatHappened = "A vendor announced new AI features for enterprises.",
            WhyItMatters = "This has meaningful impact and strategic implications for future relevance.",
            StrategicTakeaway = "This highlights innovation across the industry.",
            SourceLabel = "OpenAI News"
        };

        var analysis = LinkedInEditorialQualityAnalyzer.Analyze(draft);

        Assert.Equal("Needs refinement", analysis.ReadabilityScore);
        Assert.Contains("Hook repeats headline", analysis.Warnings);
        Assert.Contains("Opening sounds templated", analysis.Warnings);
        Assert.Contains("Why it matters is too abstract", analysis.Warnings);
        Assert.Contains("Takeaway is too generic", analysis.Warnings);
    }

    [Fact]
    public void Analyze_Should_Reward_Concrete_Draft()
    {
        var draft = new LinkedInEditorialDraft
        {
            Headline = ".NET tooling gets faster for release teams",
            Hook = "The bigger story here is how quickly developer workflows are moving toward lower execution friction.",
            WhatHappened = "Microsoft shipped .NET tooling updates that reduce release bottlenecks for engineering teams.",
            WhyItMatters = "This affects developer workflows and task completion because release teams can move with less handoff friction.",
            StrategicTakeaway = "The bigger signal is that product advantage is moving toward faster execution inside developer workflows.",
            SourceLabel = ".NET Blog"
        };

        var analysis = LinkedInEditorialQualityAnalyzer.Analyze(draft);

        Assert.Equal("Excellent", analysis.ReadabilityScore);
        Assert.Empty(analysis.Warnings);
        Assert.False(analysis.HookRepeatsHeadline);
        Assert.True(analysis.RedundancyScore < 0.52);
    }
}

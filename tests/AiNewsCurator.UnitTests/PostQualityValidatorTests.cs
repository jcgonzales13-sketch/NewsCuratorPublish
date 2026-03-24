using AiNewsCurator.Application.Services;

namespace AiNewsCurator.UnitTests;

public sealed class PostQualityValidatorTests
{
    [Fact]
    public void Should_Reject_Post_With_Forbidden_Phrase()
    {
        var text = "Essa IA vai mudar tudo para sempre e todo mundo precisa usar isso agora.";

        var result = PostQualityValidator.Validate(text);

        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void Should_Accept_Well_Formed_Post()
    {
        var text =
            """
            Anthropic expands Claude's ability to act directly on a user's computer

            This is a meaningful step in the shift from AI assistants to AI operators.

            What happened:
            Anthropic updated Claude so its tools can take actions directly on a user's machine. The release covers actions such as browsing, opening files, and supporting work steps.

            Why it matters:
            This lowers the gap between user intent and task completion. Teams can evaluate workflow execution and user trust in real environments.

            Strategic takeaway:
            The real shift is that AI is becoming an execution layer inside workflows, not just a conversational layer on top of them.

            Source: TechCrunch

            Curated by AI News Curator.
            """;

        var result = PostQualityValidator.Validate(text);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Should_Flag_Hook_That_Repeats_Headline()
    {
        var text =
            """
            Anthropic expands Claude's ability to act directly on a user's computer

            A practical AI signal worth tracking: Anthropic expands Claude's ability to act directly on a user's computer

            What happened:
            Anthropic updated Claude so its tools can take actions directly on a user's machine.

            Why it matters:
            This has strategic implications for the market.

            Strategic takeaway:
            This is an important development for the industry.

            Source: The Verge

            Curated by AI News Curator.
            """;

        var result = PostQualityValidator.Validate(text);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("Hook should complement the headline", StringComparison.Ordinal));
    }
}

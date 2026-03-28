using AiNewsCurator.Application.Services;

namespace AiNewsCurator.UnitTests;

public sealed class LinkedInEditorialPostFormatterTests
{
    [Fact]
    public void BuildPostText_Should_Append_Original_Article_Link_When_Available()
    {
        var draft = new LinkedInEditorialDraft
        {
            Headline = "Microsoft ships a new .NET update",
            Hook = "This is a meaningful step in the shift from AI assistants to AI operators.",
            WhatHappened = "Microsoft shipped a new .NET update with tooling improvements.",
            WhyItMatters = "This affects developer workflows and release readiness.",
            StrategicTakeaway = "The bigger signal is that tooling speed is becoming a product advantage.",
            SourceLabel = ".NET Blog",
            Hashtags = "#DotNet #CSharp #DeveloperTools",
            OriginalArticleUrl = "https://devblogs.microsoft.com/dotnet/example-post/",
            Signature = "Curated by AI News Curator."
        };

        var postText = LinkedInEditorialPostFormatter.BuildPostText(draft);

        Assert.Contains("Hashtags: #DotNet #CSharp #DeveloperTools", postText, StringComparison.Ordinal);
        Assert.Contains("Original article: https://devblogs.microsoft.com/dotnet/example-post/", postText, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_Should_Read_Original_Article_Link_When_Present()
    {
        var postText =
            """
            Microsoft ships a new .NET update

            This is a meaningful step in the shift from AI assistants to AI operators.

            What happened:
            Microsoft shipped a new .NET update with tooling improvements.

            Why it matters:
            This affects developer workflows and release readiness.

            Strategic takeaway:
            The bigger signal is that tooling speed is becoming a product advantage.

            Source: .NET Blog

            Hashtags: #DotNet #CSharp #DeveloperTools

            Original article: https://devblogs.microsoft.com/dotnet/example-post/

            Curated by AI News Curator.
            """;

        var parsed = LinkedInEditorialPostFormatter.Parse(postText);

        Assert.Equal("#DotNet #CSharp #DeveloperTools", parsed.Hashtags);
        Assert.Equal("https://devblogs.microsoft.com/dotnet/example-post/", parsed.OriginalArticleUrl);
        Assert.Equal("Curated by AI News Curator.", parsed.Signature);
    }
}

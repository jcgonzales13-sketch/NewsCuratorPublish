using System.Text.Json;
using AiNewsCurator.Api.Operations;
using AiNewsCurator.Domain.Entities;

namespace AiNewsCurator.UnitTests;

public sealed class SourceInputMapperTests
{
    [Fact]
    public void TryBuildSource_Should_Create_Source_With_Normalized_Fields()
    {
        var success = SourceInputMapper.TryBuildSource(
            "  .NET Blog  ",
            "Rss",
            "https://devblogs.microsoft.com/dotnet/feed/",
            "",
            true,
            8,
            10,
            ["dotnet"],
            [],
            ["official"],
            out var source,
            out var error);

        Assert.True(success);
        Assert.Null(error);
        Assert.NotNull(source);
        Assert.Equal(".NET Blog", source.Name);
        Assert.Equal("en", source.Language);

        var includeKeywords = JsonSerializer.Deserialize<string[]>(source.IncludeKeywordsJson);
        Assert.Equal(["dotnet"], includeKeywords);
    }

    [Fact]
    public void ApplyEditorialProfile_Should_Merge_DotNet_Hints_Without_Duplicates()
    {
        var result = SourceInputMapper.ApplyEditorialProfile(
            "dotnet",
            ["blazor", "dotnet"],
            [],
            ["official", "dotnet"]);

        Assert.Contains("blazor", result.IncludeKeywords, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(".net", result.IncludeKeywords, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("sdk", result.IncludeKeywords, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(result.IncludeKeywords.Length, result.IncludeKeywords.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Contains("csharp", result.Tags, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolveEditorialProfile_Should_Detect_DotNet_From_Source_Metadata()
    {
        var source = new Source
        {
            Name = "Visual Studio Magazine",
            Url = "https://visualstudiomagazine.com/RSS-Feeds/News.aspx",
            TagsJson = "[\"visualstudio\",\"news\"]",
            IncludeKeywordsJson = "[\"asp.net\",\"c#\"]"
        };

        Assert.Equal("dotnet", SourceInputMapper.ResolveEditorialProfile(source));
        Assert.Equal(".NET / C#", SourceInputMapper.ResolveEditorialProfileLabel(source));
    }

    [Fact]
    public void ParseCsv_Should_Split_And_Trim_Values()
    {
        var values = SourceInputMapper.ParseCsv(" ai, dotnet , csharp ,, ");

        Assert.Equal(["ai", "dotnet", "csharp"], values);
    }
}

using AiNewsCurator.Domain.Rules;

namespace AiNewsCurator.UnitTests;

public sealed class UrlNormalizationTests
{
    [Fact]
    public void Should_Remove_Query_And_Fragment()
    {
        var normalized = UrlNormalization.Normalize("https://example.com/news/item?utm_source=x#section");

        Assert.Equal("https://example.com/news/item", normalized);
    }
}

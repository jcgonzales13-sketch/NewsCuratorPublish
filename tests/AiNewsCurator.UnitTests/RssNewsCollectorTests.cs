using System.Net;
using System.ServiceModel.Syndication;
using System.Text;
using AiNewsCurator.Domain.Entities;
using AiNewsCurator.Domain.Enums;
using AiNewsCurator.Infrastructure.Integrations.NewsCollectors;
using Microsoft.Extensions.Logging.Abstractions;

namespace AiNewsCurator.UnitTests;

public sealed class RssNewsCollectorTests
{
    [Fact]
    public async Task CollectAsync_Should_Strip_Html_From_Summary_And_Keep_Image_Url()
    {
        const string rss =
            """
            <?xml version="1.0" encoding="utf-8"?>
            <rss version="2.0">
              <channel>
                <title>Test Feed</title>
                <item>
                  <guid>item-1</guid>
                  <title>InfoQ .NET update</title>
                  <link>https://example.com/news/infoq-dotnet-update</link>
                  <description><![CDATA[<img src="https://cdn.example.com/image.jpg" /> InfoQ published a .NET update for developers.]]></description>
                  <pubDate>Wed, 25 Mar 2026 12:00:00 GMT</pubDate>
                </item>
              </channel>
            </rss>
            """;

        var collector = new RssNewsCollector(new StubHttpClientFactory(rss), NullLogger<RssNewsCollector>.Instance);
        var source = new Source
        {
            Id = 1,
            Name = "InfoQ .NET",
            Type = SourceType.Rss,
            Url = "https://example.com/feed.xml",
            Language = "en",
            IsActive = true,
            Priority = 1,
            MaxItemsPerRun = 10
        };

        var items = await collector.CollectAsync(source, CancellationToken.None);

        var item = Assert.Single(items);
        Assert.Equal("https://cdn.example.com/image.jpg", item.ImageUrl);
        Assert.DoesNotContain("<img", item.RawSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("InfoQ published a .NET update for developers.", item.RawSummary);
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly string _rss;

        public StubHttpClientFactory(string rss)
        {
            _rss = rss;
        }

        public HttpClient CreateClient(string name)
        {
            return new HttpClient(new StubHttpMessageHandler(_rss), disposeHandler: true);
        }
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly string _rss;

        public StubHttpMessageHandler(string rss)
        {
            _rss = rss;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_rss, Encoding.UTF8, "application/rss+xml")
            });
        }
    }
}

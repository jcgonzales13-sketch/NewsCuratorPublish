using System.ServiceModel.Syndication;
using System.Text.RegularExpressions;
using System.Net;
using System.Xml;
using AiNewsCurator.Domain.Entities;
using AiNewsCurator.Domain.Enums;
using AiNewsCurator.Domain.Interfaces;
using AiNewsCurator.Domain.Rules;
using Microsoft.Extensions.Logging;

namespace AiNewsCurator.Infrastructure.Integrations.NewsCollectors;

public sealed class RssNewsCollector : INewsCollector
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<RssNewsCollector> _logger;

    public RssNewsCollector(IHttpClientFactory httpClientFactory, ILogger<RssNewsCollector> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public bool CanHandle(Source source) => source.Type == SourceType.Rss;

    public async Task<IReadOnlyList<CollectedNewsItem>> CollectAsync(Source source, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient("rss");
        using var response = await client.GetAsync(source.Url, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var xmlReader = XmlReader.Create(stream, new XmlReaderSettings { Async = true });
        var feed = SyndicationFeed.Load(xmlReader);
        if (feed is null)
        {
            return Array.Empty<CollectedNewsItem>();
        }

        var items = feed.Items
            .Take(source.MaxItemsPerRun)
            .Select(item =>
            {
                var link = item.Links.FirstOrDefault()?.Uri?.ToString() ?? string.Empty;
                var title = DecodeHtml(item.Title?.Text ?? string.Empty);
                var summary = DecodeHtml(item.Summary?.Text ?? item.Title?.Text ?? string.Empty);
                var imageUrl = TryGetImageUrl(item, summary);

                return new CollectedNewsItem
                {
                    ExternalId = item.Id,
                    Title = title,
                    Url = link,
                    CanonicalUrl = UrlNormalization.Normalize(link),
                    ImageUrl = imageUrl,
                    ImageOrigin = string.IsNullOrWhiteSpace(imageUrl) ? null : "RSS",
                    Author = item.Authors.FirstOrDefault()?.Name,
                    PublishedAt = item.PublishDate != DateTimeOffset.MinValue ? item.PublishDate : DateTimeOffset.UtcNow,
                    Language = source.Language,
                    RawSummary = summary,
                    RawContent = summary
                };
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.Title) && !string.IsNullOrWhiteSpace(item.Url))
            .ToList();

        _logger.LogInformation("Collected {Count} RSS items from source {SourceName}", items.Count, source.Name);
        return items;
    }

    private static string DecodeHtml(string value)
    {
        return WebUtility.HtmlDecode(value).Trim();
    }

    private static string? TryGetImageUrl(SyndicationItem item, string? summary)
    {
        var enclosure = item.Links.FirstOrDefault(link =>
            string.Equals(link.RelationshipType, "enclosure", StringComparison.OrdinalIgnoreCase) &&
            link.Uri is not null &&
            (link.MediaType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ?? false));

        if (enclosure?.Uri is not null)
        {
            return enclosure.Uri.ToString();
        }

        if (string.IsNullOrWhiteSpace(summary))
        {
            return null;
        }

        var match = Regex.Match(summary, "<img[^>]+src=[\"'](?<src>[^\"']+)[\"']", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups["src"].Value : null;
    }
}

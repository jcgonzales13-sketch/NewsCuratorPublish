using System.Net;
using System.Text.RegularExpressions;
using AiNewsCurator.Domain.Entities;
using AiNewsCurator.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace AiNewsCurator.Infrastructure.Integrations.NewsCollectors;

public sealed class NewsImageEnrichmentService : INewsImageEnrichmentService
{
    private static readonly Regex MetaImageRegex = new(
        "<meta[^>]+(?:property|name)=[\"'](?:og:image|twitter:image)[\"'][^>]+content=[\"'](?<url>[^\"']+)[\"'][^>]*>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex LinkImageRegex = new(
        "<link[^>]+rel=[\"']image_src[\"'][^>]+href=[\"'](?<url>[^\"']+)[\"'][^>]*>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ImgRegex = new(
        "<img[^>]+src=[\"'](?<url>[^\"']+)[\"']",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly INewsItemRepository _newsItemRepository;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<NewsImageEnrichmentService> _logger;

    public NewsImageEnrichmentService(
        INewsItemRepository newsItemRepository,
        IHttpClientFactory httpClientFactory,
        ILogger<NewsImageEnrichmentService> logger)
    {
        _newsItemRepository = newsItemRepository;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<int> BackfillMissingImagesAsync(CancellationToken cancellationToken)
    {
        var items = await _newsItemRepository.GetWithoutImageAsync(cancellationToken);
        var updatedCount = 0;

        foreach (var item in items)
        {
            var imageUrl = ExtractFromStoredContent(item) ?? await TryFetchFromPageAsync(item, cancellationToken);
            if (string.IsNullOrWhiteSpace(imageUrl))
            {
                continue;
            }

            await _newsItemRepository.UpdateImageAsync(item.Id, imageUrl, "Enriched", cancellationToken);
            updatedCount++;
        }

        return updatedCount;
    }

    private string? ExtractFromStoredContent(NewsItem item)
    {
        return TryResolveImageUrl(item.CanonicalUrl, item.RawSummary) ??
               TryResolveImageUrl(item.CanonicalUrl, item.RawContent);
    }

    private async Task<string?> TryFetchFromPageAsync(NewsItem item, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient("rss");
        var pageUrl = !string.IsNullOrWhiteSpace(item.CanonicalUrl) ? item.CanonicalUrl : item.Url;
        if (string.IsNullOrWhiteSpace(pageUrl))
        {
            return null;
        }

        try
        {
            using var response = await client.GetAsync(pageUrl, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var html = await response.Content.ReadAsStringAsync(cancellationToken);
            return TryResolveImageUrl(pageUrl, html);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Unable to enrich image for NewsItemId={NewsItemId} Url={Url}", item.Id, pageUrl);
            return null;
        }
    }

    private static string? TryResolveImageUrl(string pageUrl, string? htmlOrFragment)
    {
        if (string.IsNullOrWhiteSpace(htmlOrFragment))
        {
            return null;
        }

        var candidate =
            ExtractFirstMatch(MetaImageRegex, htmlOrFragment) ??
            ExtractFirstMatch(LinkImageRegex, htmlOrFragment) ??
            ExtractFirstMatch(ImgRegex, htmlOrFragment);

        if (string.IsNullOrWhiteSpace(candidate))
        {
            return null;
        }

        candidate = WebUtility.HtmlDecode(candidate).Trim();
        if (!Uri.TryCreate(new Uri(pageUrl), candidate, out var absoluteUri))
        {
            return null;
        }

        var scheme = absoluteUri.Scheme;
        return string.Equals(scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            ? absoluteUri.ToString()
            : null;
    }

    private static string? ExtractFirstMatch(Regex regex, string content)
    {
        var match = regex.Match(content);
        return match.Success ? match.Groups["url"].Value : null;
    }
}

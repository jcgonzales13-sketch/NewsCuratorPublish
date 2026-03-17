namespace AiNewsCurator.Domain.Rules;

public static class UrlNormalization
{
    public static string Normalize(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return url.Trim();
        }

        var builder = new UriBuilder(uri)
        {
            Query = string.Empty,
            Fragment = string.Empty
        };

        var normalized = builder.Uri.GetLeftPart(UriPartial.Path).TrimEnd('/');
        return normalized.ToLowerInvariant();
    }
}

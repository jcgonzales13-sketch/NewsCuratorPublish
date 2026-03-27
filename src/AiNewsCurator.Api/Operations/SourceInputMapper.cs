using AiNewsCurator.Domain.Entities;
using AiNewsCurator.Domain.Enums;
using System.Text.Json;

namespace AiNewsCurator.Api.Operations;

public static class SourceInputMapper
{
    private static readonly string[] AiTags = ["ai"];
    private static readonly string[] AiIncludeKeywords = ["ai", "artificial intelligence", "llm", "model", "agent"];
    private static readonly string[] DotNetTags = ["dotnet", "csharp"];
    private static readonly string[] DotNetIncludeKeywords = [".net", "dotnet", "c#", "asp.net core", "runtime", "sdk"];

    public static bool TryBuildSource(
        string name,
        string type,
        string url,
        string language,
        bool isActive,
        int priority,
        int maxItemsPerRun,
        string[] includeKeywords,
        string[] excludeKeywords,
        string[] tags,
        out Source? source,
        out string? error)
    {
        source = null;
        error = null;

        if (!Enum.TryParse<SourceType>(type, true, out var sourceType))
        {
            error = "Invalid source type.";
            return false;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out _))
        {
            error = "Invalid source URL.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            error = "Source name is required.";
            return false;
        }

        source = new Source
        {
            Name = name.Trim(),
            Type = sourceType,
            Url = url.Trim(),
            Language = string.IsNullOrWhiteSpace(language) ? "en" : language.Trim(),
            IsActive = isActive,
            Priority = priority,
            MaxItemsPerRun = maxItemsPerRun,
            IncludeKeywordsJson = JsonSerializer.Serialize(includeKeywords),
            ExcludeKeywordsJson = JsonSerializer.Serialize(excludeKeywords),
            TagsJson = JsonSerializer.Serialize(tags)
        };

        return true;
    }

    public static (string[] IncludeKeywords, string[] ExcludeKeywords, string[] Tags) ApplyEditorialProfile(
        string? editorialProfile,
        string[] includeKeywords,
        string[] excludeKeywords,
        string[] tags)
    {
        return NormalizeEditorialProfile(editorialProfile) switch
        {
            "dotnet" => (
                MergeUnique(includeKeywords, DotNetIncludeKeywords),
                excludeKeywords,
                MergeUnique(tags, DotNetTags)),
            "ai" => (
                MergeUnique(includeKeywords, AiIncludeKeywords),
                excludeKeywords,
                MergeUnique(tags, AiTags)),
            _ => (includeKeywords, excludeKeywords, tags)
        };
    }

    public static string[] ParseCsv(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    }

    public static string ResolveEditorialProfile(Source source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return IsDotNetProfile(source.TagsJson, source.IncludeKeywordsJson, source.Name, source.Url)
            ? "dotnet"
            : "ai";
    }

    public static string ResolveEditorialProfileLabel(Source source)
    {
        return ResolveEditorialProfile(source) switch
        {
            "dotnet" => ".NET / C#",
            _ => "General AI"
        };
    }

    private static string NormalizeEditorialProfile(string? editorialProfile)
    {
        return editorialProfile?.Trim().ToLowerInvariant() switch
        {
            "ai" => "ai",
            "dotnet" => "dotnet",
            _ => "auto"
        };
    }

    private static bool IsDotNetProfile(string? tagsJson, string? includeKeywordsJson, string? name, string? url)
    {
        var fingerprint = string.Join(
            " ",
            name ?? string.Empty,
            url ?? string.Empty,
            tagsJson ?? string.Empty,
            includeKeywordsJson ?? string.Empty);

        return fingerprint.Contains(".net", StringComparison.OrdinalIgnoreCase) ||
               fingerprint.Contains("dotnet", StringComparison.OrdinalIgnoreCase) ||
               fingerprint.Contains("csharp", StringComparison.OrdinalIgnoreCase) ||
               fingerprint.Contains("c#", StringComparison.OrdinalIgnoreCase) ||
               fingerprint.Contains("asp.net", StringComparison.OrdinalIgnoreCase) ||
               fingerprint.Contains("blazor", StringComparison.OrdinalIgnoreCase) ||
               fingerprint.Contains("visual studio", StringComparison.OrdinalIgnoreCase) ||
               fingerprint.Contains("rider", StringComparison.OrdinalIgnoreCase) ||
               fingerprint.Contains("resharper", StringComparison.OrdinalIgnoreCase) ||
               fingerprint.Contains("roslyn", StringComparison.OrdinalIgnoreCase);
    }

    private static string[] MergeUnique(IEnumerable<string> existing, IEnumerable<string> additions)
    {
        return existing
            .Concat(additions)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}

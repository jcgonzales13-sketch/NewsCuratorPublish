using AiNewsCurator.Domain.Entities;
using AiNewsCurator.Domain.Enums;
using System.Text.Json;

namespace AiNewsCurator.Api.Operations;

public static class SourceInputMapper
{
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

    public static string[] ParseCsv(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    }
}

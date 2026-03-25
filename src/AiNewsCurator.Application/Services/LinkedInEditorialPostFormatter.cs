using System.Text.RegularExpressions;

namespace AiNewsCurator.Application.Services;

public static partial class LinkedInEditorialPostFormatter
{
    public static string BuildPostText(LinkedInEditorialDraft draft)
    {
        var refinedDraft = draft.QualityAnalysis is null
            ? LinkedInEditorialRefiner.Refine(draft)
            : draft;

        return string.Join(
            Environment.NewLine + Environment.NewLine,
            new[]
            {
                refinedDraft.Headline.Trim(),
                refinedDraft.Hook.Trim(),
                $"What happened:{Environment.NewLine}{refinedDraft.WhatHappened.Trim()}",
                $"Why it matters:{Environment.NewLine}{refinedDraft.WhyItMatters.Trim()}",
                $"Strategic takeaway:{Environment.NewLine}{refinedDraft.StrategicTakeaway.Trim()}",
                $"Source: {refinedDraft.SourceLabel.Trim()}",
                string.IsNullOrWhiteSpace(refinedDraft.OriginalArticleUrl) ? string.Empty : $"Original article: {refinedDraft.OriginalArticleUrl.Trim()}",
                refinedDraft.Signature.Trim()
            }.Where(section => !string.IsNullOrWhiteSpace(section)));
    }

    public static LinkedInEditorialDraft Parse(string postText)
    {
        var normalized = postText.Replace("\r\n", "\n").Trim();
        var sections = normalized.Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (sections.Length < 6)
        {
            return new LinkedInEditorialDraft
            {
                Hook = sections.FirstOrDefault() ?? string.Empty,
                WhatHappened = string.Join(Environment.NewLine + Environment.NewLine, sections.Skip(1))
            };
        }

        return new LinkedInEditorialDraft
        {
            Headline = sections.ElementAtOrDefault(0) ?? string.Empty,
            Hook = sections.ElementAtOrDefault(1) ?? string.Empty,
            WhatHappened = StripSectionLabel(sections.ElementAtOrDefault(2), "What happened:"),
            WhyItMatters = StripSectionLabel(sections.ElementAtOrDefault(3), "Why it matters:"),
            StrategicTakeaway = StripSectionLabel(sections.ElementAtOrDefault(4), "Strategic takeaway:"),
            SourceLabel = StripSectionLabel(sections.ElementAtOrDefault(5), "Source:"),
            OriginalArticleUrl = ExtractOriginalArticleUrl(sections),
            Signature = ExtractSignature(sections)
        };
    }

    public static string SanitizeSentence(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = MultiWhitespaceRegex().Replace(value.Trim(), " ");
        return normalized.Length <= maxLength
            ? normalized
            : normalized[..maxLength].TrimEnd(' ', '.', ',', ';', ':') + ".";
    }

    private static string StripSectionLabel(string? value, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.StartsWith(label, StringComparison.OrdinalIgnoreCase)
            ? value[label.Length..].Trim()
            : value.Trim();
    }

    private static string ExtractOriginalArticleUrl(string[] sections)
    {
        var originalArticleSection = sections.ElementAtOrDefault(6);
        if (string.IsNullOrWhiteSpace(originalArticleSection) ||
            !originalArticleSection.StartsWith("Original article:", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        return StripSectionLabel(originalArticleSection, "Original article:");
    }

    private static string ExtractSignature(string[] sections)
    {
        var originalArticleSection = sections.ElementAtOrDefault(6);
        if (!string.IsNullOrWhiteSpace(originalArticleSection) &&
            originalArticleSection.StartsWith("Original article:", StringComparison.OrdinalIgnoreCase))
        {
            return sections.ElementAtOrDefault(7) ?? string.Empty;
        }

        return originalArticleSection ?? string.Empty;
    }

    [GeneratedRegex("\\s+")]
    private static partial Regex MultiWhitespaceRegex();
}

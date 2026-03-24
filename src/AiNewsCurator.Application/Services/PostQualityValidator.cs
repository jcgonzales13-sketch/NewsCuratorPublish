using AiNewsCurator.Domain.Entities;

namespace AiNewsCurator.Application.Services;

public static class PostQualityValidator
{
    private static readonly string[] ForbiddenPhrases =
    [
        "vai mudar tudo para sempre",
        "maior revolucao da historia",
        "todo mundo precisa usar isso agora",
        "sem duvida"
    ];

    public static PostValidationResult Validate(string text)
    {
        var result = new PostValidationResult();
        var trimmed = text.Trim();

        if (string.IsNullOrWhiteSpace(trimmed))
        {
            result.Errors.Add("Post text cannot be empty.");
            return result;
        }

        if (trimmed.Length < 120)
        {
            result.Errors.Add("Post text is too short.");
        }

        if (trimmed.Length > 1300)
        {
            result.Errors.Add("Post text is too long for the desired LinkedIn style.");
        }

        if (!trimmed.Contains("IA", StringComparison.OrdinalIgnoreCase) &&
            !trimmed.Contains("AI", StringComparison.OrdinalIgnoreCase) &&
            !trimmed.Contains("inteligencia artificial", StringComparison.OrdinalIgnoreCase) &&
            !trimmed.Contains("artificial intelligence", StringComparison.OrdinalIgnoreCase))
        {
            result.Errors.Add("Post text must mention AI context.");
        }

        if (trimmed.Contains("{{", StringComparison.Ordinal) || trimmed.Contains("}}", StringComparison.Ordinal))
        {
            result.Errors.Add("Post text contains unresolved placeholders.");
        }

        var editorialDraft = LinkedInEditorialPostFormatter.Parse(trimmed);
        var quality = LinkedInEditorialQualityAnalyzer.Analyze(editorialDraft);
        if (quality.HookRepeatsHeadline)
        {
            result.Errors.Add("Hook should complement the headline instead of repeating it.");
        }

        if (quality.WhyItMattersTooAbstract)
        {
            result.Errors.Add("Why it matters should be more concrete.");
        }

        if (quality.TakeawayTooGeneric)
        {
            result.Errors.Add("Strategic takeaway should be sharper.");
        }

        foreach (var phrase in ForbiddenPhrases)
        {
            if (trimmed.Contains(phrase, StringComparison.OrdinalIgnoreCase))
            {
                result.Errors.Add($"Post text contains forbidden phrase: {phrase}.");
            }
        }

        foreach (var warning in quality.Warnings.Where(warning => warning is "Opening sounds templated"))
        {
            result.Errors.Add(warning + ".");
        }

        return result;
    }
}

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
            !trimmed.Contains("inteligencia artificial", StringComparison.OrdinalIgnoreCase))
        {
            result.Errors.Add("Post text must mention AI context.");
        }

        if (trimmed.Contains("{{", StringComparison.Ordinal) || trimmed.Contains("}}", StringComparison.Ordinal))
        {
            result.Errors.Add("Post text contains unresolved placeholders.");
        }

        foreach (var phrase in ForbiddenPhrases)
        {
            if (trimmed.Contains(phrase, StringComparison.OrdinalIgnoreCase))
            {
                result.Errors.Add($"Post text contains forbidden phrase: {phrase}.");
            }
        }

        return result;
    }
}

namespace AiNewsCurator.Application.Services;

public static class LinkedInEditorialRefiner
{
    private static readonly (string Type, string Text)[] HookTemplates =
    [
        ("strategic_shift", "This is a meaningful step in the shift from AI assistants to AI operators."),
        ("market_signal", "The bigger story here is how quickly AI products are moving toward real execution inside everyday workflows."),
        ("product_implication", "Small product changes like this can quickly change how users judge usefulness, trust, and speed."),
        ("workflow_change", "The key shift is that AI is moving closer to execution, not just recommendation."),
        ("ecosystem_signal", "This is another sign that ecosystem advantage is increasingly shaped by lower friction in the user experience.")
    ];

    public static LinkedInEditorialDraft Refine(LinkedInEditorialDraft draft)
    {
        var refined = new LinkedInEditorialDraft
        {
            Headline = LinkedInEditorialPostFormatter.SanitizeSentence(draft.Headline, 100),
            Hook = LinkedInEditorialPostFormatter.SanitizeSentence(draft.Hook, 180),
            HookType = draft.HookType,
            WhatHappened = LinkedInEditorialPostFormatter.SanitizeSentence(ShortenToTwoSentences(draft.WhatHappened), 280),
            WhyItMatters = LinkedInEditorialPostFormatter.SanitizeSentence(draft.WhyItMatters, 320),
            StrategicTakeaway = LinkedInEditorialPostFormatter.SanitizeSentence(ShortenToTwoSentences(draft.StrategicTakeaway), 220),
            SourceLabel = LinkedInEditorialPostFormatter.SanitizeSentence(draft.SourceLabel, 80),
            Hashtags = NormalizeHashtags(draft.Hashtags),
            OriginalArticleUrl = draft.OriginalArticleUrl?.Trim() ?? string.Empty,
            Signature = LinkedInEditorialPostFormatter.SanitizeSentence(draft.Signature, 80)
        };

        refined = refined with
        {
            Hook = SelectHook(refined),
            WhyItMatters = ImproveWhyItMatters(refined),
            StrategicTakeaway = ImproveTakeaway(refined)
        };

        return refined with
        {
            QualityAnalysis = LinkedInEditorialQualityAnalyzer.Analyze(refined)
        };
    }

    private static string SelectHook(LinkedInEditorialDraft draft)
    {
        var candidates = new List<(string Type, string Text)>
        {
            (draft.HookType ?? "selected", draft.Hook)
        };
        candidates.AddRange(HookTemplates);

        var winner = candidates
            .Select(candidate =>
            {
                var candidateDraft = draft with { Hook = candidate.Text, HookType = candidate.Type };
                var quality = LinkedInEditorialQualityAnalyzer.Analyze(candidateDraft);
                var score = 100d
                            - (quality.RedundancyScore * 45d)
                            - (quality.AbstractionScore * 25d)
                            - (quality.TextDensityScore * 20d)
                            - (quality.HookRepeatsHeadline ? 25d : 0d)
                            - (LinkedInEditorialQualityAnalyzer.ContainsGenericPhrase(candidate.Text) ? 20d : 0d)
                            - Math.Max(0, candidate.Text.Length - 180) * 0.1;
                return (candidate.Type, candidate.Text, Score: score);
            })
            .OrderByDescending(item => item.Score)
            .First();

        return winner.Text;
    }

    private static string ImproveWhyItMatters(LinkedInEditorialDraft draft)
    {
        if (!LinkedInEditorialQualityAnalyzer.ContainsAbstractLanguage(draft.WhyItMatters) &&
            LinkedInEditorialQualityAnalyzer.HasConcreteSignal(draft.WhyItMatters))
        {
            return draft.WhyItMatters;
        }

        var lowerContext = $"{draft.Headline} {draft.WhatHappened}".ToLowerInvariant();
        if (lowerContext.Contains("computer") || lowerContext.Contains("browser") || lowerContext.Contains("file"))
        {
            return "This lowers the gap between user intent and task completion. For product and operations teams, that makes workflow execution easier to evaluate in real tools.";
        }

        if (lowerContext.Contains("developer") || lowerContext.Contains("code"))
        {
            return "This changes developer workflows by moving AI closer to execution instead of just suggestion. Teams can evaluate faster task completion and less handoff friction.";
        }

        return "This moves AI closer to workflow execution instead of just assistance. That matters now because teams can judge product adoption and operational automation in real environments.";
    }

    private static string ImproveTakeaway(LinkedInEditorialDraft draft)
    {
        if (!LinkedInEditorialQualityAnalyzer.ContainsAbstractLanguage(draft.StrategicTakeaway) &&
            (draft.StrategicTakeaway.StartsWith("The bigger signal is", StringComparison.OrdinalIgnoreCase) ||
             draft.StrategicTakeaway.StartsWith("What matters more than the feature itself is", StringComparison.OrdinalIgnoreCase) ||
             draft.StrategicTakeaway.StartsWith("The real shift is", StringComparison.OrdinalIgnoreCase) ||
             draft.StrategicTakeaway.StartsWith("The competitive implication is", StringComparison.OrdinalIgnoreCase)))
        {
            return draft.StrategicTakeaway;
        }

        var lowerContext = $"{draft.Headline} {draft.WhatHappened} {draft.WhyItMatters}".ToLowerInvariant();
        if (lowerContext.Contains("computer") || lowerContext.Contains("workflow") || lowerContext.Contains("operator"))
        {
            return "The real shift is that AI is becoming an execution layer inside workflows, not just a conversational layer on top of them.";
        }

        if (lowerContext.Contains("developer") || lowerContext.Contains("code"))
        {
            return "The bigger signal is that product advantage is moving toward faster execution inside developer workflows, not just better model output.";
        }

        return "What matters more than the feature itself is how it reduces friction between intent and execution in everyday software work.";
    }

    private static string ShortenToTwoSentences(string text)
    {
        var sentences = text
            .Split(['.', '!', '?'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(sentence => !string.IsNullOrWhiteSpace(sentence))
            .Take(2)
            .ToArray();

        return sentences.Length == 0
            ? text
            : string.Join(". ", sentences) + ".";
    }

    private static string NormalizeHashtags(string? hashtags)
    {
        if (string.IsNullOrWhiteSpace(hashtags))
        {
            return string.Empty;
        }

        var normalized = hashtags
            .Split([' ', ',', ';', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries)
            .Select(tag => tag.Trim())
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.StartsWith('#') ? tag : $"#{tag}")
            .Select(tag => new string(tag.Where(character => char.IsLetterOrDigit(character) || character == '#').ToArray()))
            .Where(tag => tag.Length > 1)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToArray();

        return string.Join(' ', normalized);
    }
}

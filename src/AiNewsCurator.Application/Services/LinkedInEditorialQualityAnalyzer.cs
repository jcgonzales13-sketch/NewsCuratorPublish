using System.Text.RegularExpressions;

namespace AiNewsCurator.Application.Services;

public static partial class LinkedInEditorialQualityAnalyzer
{
    private static readonly string[] GenericPhrases =
    [
        "a practical ai signal worth tracking",
        "one story that stood out today",
        "at a practical level",
        "what makes this relevant is",
        "this is worth watching",
        "this may have implications",
        "this is an important development",
        "this highlights innovation",
        "this signals change in the industry",
        "it will be interesting to see"
    ];

    private static readonly string[] AbstractPhrases =
    [
        "measurable value",
        "strategic implications",
        "future relevance",
        "important development",
        "meaningful impact",
        "industry evolution",
        "emerging opportunity",
        "practical relevance",
        "worth evaluating"
    ];

    private static readonly string[] ConcreteSignals =
    [
        "workflow execution",
        "file sharing friction",
        "task completion",
        "product adoption",
        "developer workflows",
        "ecosystem loyalty",
        "operational automation",
        "user trust",
        "decision velocity",
        "real environments",
        "user intent",
        "task handoff"
    ];

    private static readonly HashSet<string> StopWords =
    [
        "the", "a", "an", "and", "or", "to", "of", "for", "in", "on", "with", "from", "into", "inside",
        "your", "their", "this", "that", "these", "those", "is", "are", "be", "being", "by", "how", "as",
        "at", "it", "its", "than", "more", "closer", "toward", "towards", "can"
    ];

    public static LinkedInDraftQualityAnalysis Analyze(LinkedInEditorialDraft draft)
    {
        var warnings = new List<string>();
        var headlineHookOverlap = CalculateOverlapScore(draft.Headline, draft.Hook);
        var hookTakeawayOverlap = CalculateOverlapScore(draft.Hook, draft.StrategicTakeaway);
        var factsOverlap = CalculateOverlapScore(draft.WhatHappened, draft.WhyItMatters);
        var redundancyScore = Math.Round((headlineHookOverlap + hookTakeawayOverlap + factsOverlap) / 3d, 2);

        var hookRepeatsHeadline = headlineHookOverlap >= 0.52 || StartsWithGenericLeadIn(draft.Hook, draft.Headline);
        if (hookRepeatsHeadline)
        {
            warnings.Add("Hook repeats headline");
        }

        if (ContainsGenericPhrase(draft.Hook))
        {
            warnings.Add("Opening sounds templated");
        }

        var whyTooAbstract = ContainsAbstractLanguage(draft.WhyItMatters) || !HasConcreteSignal(draft.WhyItMatters);
        if (whyTooAbstract)
        {
            warnings.Add("Why it matters is too abstract");
        }

        var takeawayTooGeneric = ContainsAbstractLanguage(draft.StrategicTakeaway) ||
                                 !StartsWithPreferredTakeawayPattern(draft.StrategicTakeaway);
        if (takeawayTooGeneric)
        {
            warnings.Add("Takeaway is too generic");
        }

        var textDensityScore = CalculateTextDensityScore(draft);
        if (textDensityScore >= 0.55)
        {
            warnings.Add("Text density is high");
        }

        if (draft.WhatHappened.Length > 280)
        {
            warnings.Add("What happened is too long");
        }

        if (draft.WhyItMatters.Length > 320)
        {
            warnings.Add("Why it matters is too long");
        }

        if (draft.StrategicTakeaway.Length > 220)
        {
            warnings.Add("Strategic takeaway is too long");
        }

        var readabilityScore = warnings.Count switch
        {
            0 => "Excellent",
            <= 2 => "Good",
            _ => "Needs refinement"
        };

        return new LinkedInDraftQualityAnalysis
        {
            ReadabilityScore = readabilityScore,
            RedundancyScore = redundancyScore,
            AbstractionScore = Math.Round(CalculateAbstractionScore(draft), 2),
            TextDensityScore = Math.Round(textDensityScore, 2),
            HookRepeatsHeadline = hookRepeatsHeadline,
            TakeawayTooGeneric = takeawayTooGeneric,
            WhyItMattersTooAbstract = whyTooAbstract,
            Warnings = warnings
        };
    }

    public static bool ContainsGenericPhrase(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var normalized = text.ToLowerInvariant();
        return GenericPhrases.Any(normalized.Contains);
    }

    public static bool ContainsAbstractLanguage(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var normalized = text.ToLowerInvariant();
        return AbstractPhrases.Any(normalized.Contains);
    }

    public static bool HasConcreteSignal(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var normalized = text.ToLowerInvariant();
        return ConcreteSignals.Any(normalized.Contains) ||
               (normalized.Contains("workflow") && (normalized.Contains("execute") || normalized.Contains("execution"))) ||
               (normalized.Contains("user") && (normalized.Contains("task") || normalized.Contains("trust"))) ||
               (normalized.Contains("product") && normalized.Contains("adoption"));
    }

    private static bool StartsWithGenericLeadIn(string hook, string headline)
    {
        if (string.IsNullOrWhiteSpace(hook) || string.IsNullOrWhiteSpace(headline))
        {
            return false;
        }

        var normalizedHook = NormalizeForComparison(hook);
        var normalizedHeadline = NormalizeForComparison(headline);
        return normalizedHook.EndsWith(normalizedHeadline, StringComparison.Ordinal) && normalizedHook != normalizedHeadline;
    }

    private static bool StartsWithPreferredTakeawayPattern(string takeaway)
    {
        return takeaway.StartsWith("The bigger signal is", StringComparison.OrdinalIgnoreCase) ||
               takeaway.StartsWith("What matters more than the feature itself is", StringComparison.OrdinalIgnoreCase) ||
               takeaway.StartsWith("The real shift is", StringComparison.OrdinalIgnoreCase) ||
               takeaway.StartsWith("The competitive implication is", StringComparison.OrdinalIgnoreCase);
    }

    private static double CalculateTextDensityScore(LinkedInEditorialDraft draft)
    {
        var sections = new[] { draft.Hook, draft.WhatHappened, draft.WhyItMatters, draft.StrategicTakeaway }
            .Where(section => !string.IsNullOrWhiteSpace(section))
            .ToArray();
        var totalCharacters = sections.Sum(section => section.Length);
        var averageSentenceLength = sections
            .SelectMany(SplitSentences)
            .Where(sentence => !string.IsNullOrWhiteSpace(sentence))
            .Select(sentence => Tokenize(sentence).Count)
            .DefaultIfEmpty()
            .Average();
        var whitespaceRatio = totalCharacters == 0
            ? 0
            : draft.ToFeedText().Count(char.IsWhiteSpace) / (double)draft.ToFeedText().Length;

        var density = 0d;
        if (totalCharacters > 780) density += 0.3;
        if (averageSentenceLength > 22) density += 0.3;
        if (whitespaceRatio < 0.1) density += 0.2;
        if (sections.Any(section => section.Length > 260)) density += 0.2;

        return Math.Clamp(density, 0, 1);
    }

    private static double CalculateAbstractionScore(LinkedInEditorialDraft draft)
    {
        var sections = $"{draft.Hook} {draft.WhyItMatters} {draft.StrategicTakeaway}";
        var normalized = sections.ToLowerInvariant();
        var abstractHits = AbstractPhrases.Count(normalized.Contains);
        var genericHits = GenericPhrases.Count(normalized.Contains);
        var concreteBonus = HasConcreteSignal(draft.WhyItMatters) ? -0.2 : 0;
        return Math.Clamp((abstractHits * 0.18) + (genericHits * 0.22) + concreteBonus + 0.2, 0, 1);
    }

    private static double CalculateOverlapScore(string left, string right)
    {
        var leftTokens = Tokenize(left).Where(token => token.Length > 2 && !StopWords.Contains(token)).Distinct().ToArray();
        var rightTokens = Tokenize(right).Where(token => token.Length > 2 && !StopWords.Contains(token)).Distinct().ToArray();

        if (leftTokens.Length == 0 || rightTokens.Length == 0)
        {
            return 0;
        }

        var overlap = leftTokens.Intersect(rightTokens).Count();
        return overlap / (double)Math.Min(leftTokens.Length, rightTokens.Length);
    }

    private static IEnumerable<string> SplitSentences(string text)
    {
        return SentenceSplitRegex().Split(text);
    }

    private static IReadOnlyList<string> Tokenize(string text)
    {
        return WordRegex().Matches(text.ToLowerInvariant()).Select(match => match.Value).ToArray();
    }

    private static string NormalizeForComparison(string text)
    {
        return string.Join(" ", Tokenize(text));
    }

    [GeneratedRegex("[A-Za-z0-9']+")]
    private static partial Regex WordRegex();

    [GeneratedRegex(@"(?<=[.!?])\s+")]
    private static partial Regex SentenceSplitRegex();
}

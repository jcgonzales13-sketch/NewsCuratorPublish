using AiNewsCurator.Domain.Entities;

namespace AiNewsCurator.Infrastructure.Integrations.AI;

internal static class OpenAiPromptFactory
{
    public static object[] BuildEvaluationInput(NewsItem newsItem, string tone)
    {
        return
        [
            new
            {
                role = "system",
                content = "You are an editorial AI assistant for LinkedIn publishing. Always respond in English, maintain a credible publication-quality tone, and never invent facts."
            },
            new
            {
                role = "user",
                content =
                    $"Evaluate the following news item and produce structured JSON.\n" +
                    $"Title: {newsItem.Title}\n" +
                    $"Canonical URL: {newsItem.CanonicalUrl}\n" +
                    $"Raw summary: {newsItem.RawSummary}\n" +
                    $"Raw content: {newsItem.RawContent}\n\n" +
                    $"Preferred tone: {tone}\n\n" +
                    "Rules:\n" +
                    "- Write in English.\n" +
                    "- Do not invent facts.\n" +
                    "- Be factual, clear, and publication-ready.\n" +
                    "- Assess relevance, confidence, category, and key points.\n" +
                    "- Also produce a structured LinkedIn draft with these exact sections: headline, hook, whatHappened, whyItMatters, strategicTakeaway, sourceLabel, signature.\n" +
                    "- Internally generate multiple candidates before choosing the final result: 3 headlines, 3 hooks from different hook types, 2 whatHappened options, 2 whyItMatters options, and 3 strategic takeaways.\n" +
                    "- Return only the selected final draft, plus hookType.\n" +
                    "- Allowed hookType values: strategic_shift, market_signal, product_implication, workflow_change, ecosystem_signal.\n" +
                    "- The post must feel curated, concise, strategic, and trustworthy.\n" +
                    "- headline: short, sharp, and publication-ready.\n" +
                    "- hook: a strong opening sentence that complements the headline and does not restate it.\n" +
                    "- whatHappened: 1 to 2 concise factual sentences.\n" +
                    "- whatHappened should stay under 280 characters when possible.\n" +
                    "- whyItMatters: 1 to 2 concise sentences focused on practical relevance, and include at least one concrete consequence such as workflow execution, task completion, developer workflows, user trust, or operational automation.\n" +
                    "- strategicTakeaway: exactly 1 or 2 short sentences with a sharper interpretation or market signal.\n" +
                    "- sourceLabel: clean publication/source attribution without a URL.\n" +
                    "- signature: short and subtle.\n" +
                    "- Avoid repeating the headline in the hook.\n" +
                    "- Avoid generic phrases such as: A practical AI signal worth tracking, One story that stood out today, At a practical level, What makes this relevant is, This is worth watching, This may have implications, This is an important development, This highlights innovation, This signals change in the industry, It will be interesting to see.\n" +
                    "- Avoid abstract filler such as measurable value, strategic implications, future relevance, meaningful impact, industry evolution, or emerging opportunity.\n" +
                    "- Avoid clickbait, hype, and exaggerated claims.\n" +
                    "- Even if the story should not be published, still return the full JSON."
            }
        ];
    }

    public static object BuildStructuredTextFormat()
    {
        return new
        {
            format = new
            {
                type = "json_schema",
                name = "ai_news_curation_result",
                strict = true,
                schema = new
                {
                    type = "object",
                    additionalProperties = false,
                    properties = new
                    {
                        isRelevant = new { type = "boolean" },
                        relevanceScore = new { type = "number" },
                        confidenceScore = new { type = "number" },
                        category = new { type = "string" },
                        whyRelevant = new { type = "string" },
                        summary = new { type = "string" },
                        keyPoints = new
                        {
                            type = "array",
                            items = new { type = "string" }
                        },
                        headline = new { type = "string" },
                        hook = new { type = "string" },
                        hookType = new { type = "string" },
                        whatHappened = new { type = "string" },
                        whyItMatters = new { type = "string" },
                        strategicTakeaway = new { type = "string" },
                        sourceLabel = new { type = "string" },
                        signature = new { type = "string" }
                    },
                    required = new[]
                    {
                        "isRelevant",
                        "relevanceScore",
                        "confidenceScore",
                        "category",
                        "whyRelevant",
                        "summary",
                        "keyPoints",
                        "headline",
                        "hook",
                        "hookType",
                        "whatHappened",
                        "whyItMatters",
                        "strategicTakeaway",
                        "sourceLabel",
                        "signature"
                    }
                }
            }
        };
    }
}

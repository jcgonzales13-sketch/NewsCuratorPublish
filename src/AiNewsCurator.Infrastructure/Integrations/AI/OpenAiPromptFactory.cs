using AiNewsCurator.Domain.Entities;

namespace AiNewsCurator.Infrastructure.Integrations.AI;

internal static class OpenAiPromptFactory
{
    public static object[] BuildEvaluationInput(NewsItem newsItem)
    {
        return
        [
            new
            {
                role = "system",
                content = "You are an AI news curator for LinkedIn. Always respond in English, maintain a professional tone, and never invent facts."
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
                    "Rules:\n" +
                    "- Write in English.\n" +
                    "- Do not invent facts.\n" +
                    "- Be factual, clear, and professional.\n" +
                    "- Assess relevance, confidence, category, and key points.\n" +
                    "- Also produce a polished LinkedIn draft with context, business impact, and a light CTA.\n" +
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
                        linkedinDraft = new { type = "string" }
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
                        "linkedinDraft"
                    }
                }
            }
        };
    }
}

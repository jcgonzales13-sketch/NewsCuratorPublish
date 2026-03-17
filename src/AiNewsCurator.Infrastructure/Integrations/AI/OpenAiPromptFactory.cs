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
                content = "You are an AI news curator for LinkedIn. Always respond in Brazilian Portuguese and never invent facts."
            },
            new
            {
                role = "user",
                content =
                    $"Avalie a noticia a seguir e gere um JSON estruturado.\n" +
                    $"Titulo: {newsItem.Title}\n" +
                    $"URL canonica: {newsItem.CanonicalUrl}\n" +
                    $"Resumo bruto: {newsItem.RawSummary}\n" +
                    $"Conteudo bruto: {newsItem.RawContent}\n\n" +
                    "Regras:\n" +
                    "- Trabalhe em portugues do Brasil.\n" +
                    "- Nao invente fatos.\n" +
                    "- Seja factual e profissional.\n" +
                    "- Classifique relevancia, confianca, categoria e pontos principais.\n" +
                    "- Escreva tambem um rascunho de post para LinkedIn com contexto, impacto e CTA leve.\n" +
                    "- Se a noticia nao merecer publicacao, ainda assim devolva o JSON completo."
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

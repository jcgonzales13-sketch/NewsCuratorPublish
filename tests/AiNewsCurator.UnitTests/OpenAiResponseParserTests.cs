using AiNewsCurator.Infrastructure.Integrations.AI;

namespace AiNewsCurator.UnitTests;

public sealed class OpenAiResponseParserTests
{
    [Fact]
    public void Should_Extract_Output_Text_From_Responses_Api_Payload()
    {
        const string payload =
            """
            {
              "output": [
                {
                  "content": [
                    {
                      "type": "output_text",
                      "text": "{\"isRelevant\":true,\"relevanceScore\":0.9,\"confidenceScore\":0.88,\"category\":\"LLM\",\"whyRelevant\":\"impacta profissionais\",\"summary\":\"resumo\",\"keyPoints\":[\"p1\"],\"linkedinDraft\":\"draft\"}"
                    }
                  ]
                }
              ]
            }
            """;

        var text = OpenAiResponseParser.ExtractOutputText(payload);

        Assert.Contains("\"isRelevant\":true", text);
        Assert.Contains("\"linkedinDraft\":\"draft\"", text);
    }
}

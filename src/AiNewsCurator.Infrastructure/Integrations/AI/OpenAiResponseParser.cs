using System.Text.Json;

namespace AiNewsCurator.Infrastructure.Integrations.AI;

public static class OpenAiResponseParser
{
    public static string ExtractOutputText(string rawResponse)
    {
        using var document = JsonDocument.Parse(rawResponse);
        if (!document.RootElement.TryGetProperty("output", out var outputElement) ||
            outputElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("OpenAI response did not contain an output array.");
        }

        foreach (var item in outputElement.EnumerateArray())
        {
            if (!item.TryGetProperty("content", out var contentElement) ||
                contentElement.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var content in contentElement.EnumerateArray())
            {
                if (content.TryGetProperty("type", out var typeElement) &&
                    string.Equals(typeElement.GetString(), "output_text", StringComparison.Ordinal) &&
                    content.TryGetProperty("text", out var textElement))
                {
                    var text = textElement.GetString();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        return text;
                    }
                }
            }
        }

        throw new InvalidOperationException("OpenAI response did not contain output_text content.");
    }
}

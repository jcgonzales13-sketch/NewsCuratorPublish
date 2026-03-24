using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AiNewsCurator.Application.DTOs;
using AiNewsCurator.Application.Services;
using AiNewsCurator.Domain.Entities;
using AiNewsCurator.Domain.Interfaces;
using Microsoft.Extensions.Options;

namespace AiNewsCurator.Infrastructure.Integrations.AI;

public sealed class OpenAiResponsesAiCurationService : IAiCurationService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AppOptions _options;

    public OpenAiResponsesAiCurationService(IHttpClientFactory httpClientFactory, IOptions<AppOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
    }

    public async Task<AiEvaluationResult> EvaluateNewsAsync(NewsItem newsItem, CancellationToken cancellationToken)
    {
        var requestPayload = new
        {
            model = _options.AiModelName,
            input = OpenAiPromptFactory.BuildEvaluationInput(newsItem, _options.LinkedInTone),
            text = OpenAiPromptFactory.BuildStructuredTextFormat()
        };

        var rawResponse = await SendRequestAsync(requestPayload, cancellationToken);
        var outputText = OpenAiResponseParser.ExtractOutputText(rawResponse);
        var structured = JsonSerializer.Deserialize<AiStructuredOutput>(outputText, JsonOptions()) ??
                         throw new InvalidOperationException("Unable to parse structured OpenAI output.");
        var editorialDraft = new LinkedInEditorialDraft
        {
            Headline = LinkedInEditorialPostFormatter.SanitizeSentence(structured.Headline, 90),
            Hook = LinkedInEditorialPostFormatter.SanitizeSentence(structured.Hook, 180),
            HookType = LinkedInEditorialPostFormatter.SanitizeSentence(structured.HookType, 40),
            WhatHappened = LinkedInEditorialPostFormatter.SanitizeSentence(structured.WhatHappened, 280),
            WhyItMatters = LinkedInEditorialPostFormatter.SanitizeSentence(structured.WhyItMatters, 280),
            StrategicTakeaway = LinkedInEditorialPostFormatter.SanitizeSentence(structured.StrategicTakeaway, 180),
            SourceLabel = LinkedInEditorialPostFormatter.SanitizeSentence(structured.SourceLabel, 80),
            Signature = LinkedInEditorialPostFormatter.SanitizeSentence(structured.Signature, 80)
        };
        editorialDraft = LinkedInEditorialRefiner.Refine(editorialDraft);

        return new AiEvaluationResult
        {
            IsRelevant = structured.IsRelevant,
            RelevanceScore = structured.RelevanceScore,
            ConfidenceScore = structured.ConfidenceScore,
            Category = structured.Category,
            WhyRelevant = structured.WhyRelevant,
            Summary = structured.Summary,
            KeyPoints = structured.KeyPoints,
            LinkedInTitleSuggestion = editorialDraft.Headline,
            LinkedInDraft = LinkedInEditorialPostFormatter.BuildPostText(editorialDraft),
            PromptVersion = "openai-responses-v1",
            ModelName = _options.AiModelName,
            PromptPayload = JsonSerializer.Serialize(requestPayload),
            ResponsePayload = rawResponse
        };
    }

    public async Task<string> GenerateLinkedInPostAsync(NewsItem newsItem, CurationResult curationResult, CancellationToken cancellationToken)
    {
        var evaluation = await EvaluateNewsAsync(newsItem, cancellationToken);
        return evaluation.LinkedInDraft;
    }

    public Task<PostValidationResult> ValidatePostAsync(NewsItem newsItem, string postText, CancellationToken cancellationToken)
    {
        return Task.FromResult(PostQualityValidator.Validate(postText));
    }

    private async Task<string> SendRequestAsync(object payload, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.AiApiKey))
        {
            throw new InvalidOperationException("AI_API_KEY is not configured.");
        }

        var client = _httpClientFactory.CreateClient("openai");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.AiApiKey);
        var requestJson = JsonSerializer.Serialize(payload);
        using var response = await client.PostAsync(
            "/v1/responses",
            new StringContent(requestJson, Encoding.UTF8, "application/json"),
            cancellationToken);

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"OpenAI request failed with status {(int)response.StatusCode}: {body}");
        }

        return body;
    }

    private static JsonSerializerOptions JsonOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }
}

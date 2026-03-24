using System.Text.Json;
using AiNewsCurator.Application.Services;
using AiNewsCurator.Domain.Entities;

namespace AiNewsCurator.Api.Models.Operations;

public sealed class OperationsDraftViewModel
{
    public required PostDraft Draft { get; init; }
    public NewsItem? NewsItem { get; init; }
    public string? SourceName { get; init; }
    public Publication? LatestPublication { get; init; }
    public string? PostImageUrl => NewsItem?.ImageUrl;
    public string? PostImageOrigin => NewsItem?.ImageOrigin;
    public bool HasPostImage => !string.IsNullOrWhiteSpace(PostImageUrl);
    public LinkedInEditorialDraft EditorialDraft => LinkedInEditorialRefiner.Refine(LinkedInEditorialPostFormatter.Parse(Draft.PostText));
    public LinkedInDraftQualityAnalysis QualityAnalysis => EditorialDraft.QualityAnalysis ?? LinkedInEditorialQualityAnalyzer.Analyze(EditorialDraft);
    public IReadOnlyList<string> ValidationErrors =>
        JsonSerializer.Deserialize<List<string>>(Draft.ValidationErrorsJson) ?? [];
    public bool IsReadyToPublish => ValidationErrors.Count == 0 && QualityAnalysis.ReadabilityScore != "Needs refinement";
    public int CharacterCount => Draft.PostText.Length;
    public string ReadinessLabel => IsReadyToPublish ? "Ready to publish" : "Needs review";
    public string SourceUrl => NewsItem?.CanonicalUrl ?? NewsItem?.Url ?? string.Empty;

    public bool PublishedWithImage =>
        LatestPublication?.RequestPayload?.Contains("\"shareMediaCategory\":\"IMAGE\"", StringComparison.OrdinalIgnoreCase) == true;
}

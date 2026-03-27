using System.Text.Json;
using AiNewsCurator.Application.Services;
using AiNewsCurator.Domain.Entities;
using AiNewsCurator.Domain.Enums;

namespace AiNewsCurator.Api.Models.Operations;

public sealed class OperationsDraftViewModel
{
    public required PostDraft Draft { get; init; }
    public UpdateDraftFormModel EditForm => new()
    {
        TitleSuggestion = Draft.TitleSuggestion,
        PostText = Draft.PostText
    };
    public UpdateNewsImageFormModel ImageForm => new()
    {
        ImageUrl = NewsItem?.ImageUrl ?? string.Empty
    };
    public NewsItem? NewsItem { get; init; }
    public CurationResult? LatestCuration { get; init; }
    public string? SourceName { get; init; }
    public Publication? LatestPublication { get; init; }
    public IReadOnlyList<Publication> PublicationHistory { get; init; } = [];
    public string? PostImageUrl => NewsItem?.ImageUrl;
    public string? PostImageOrigin => NewsItem?.ImageOrigin;
    public bool HasPostImage => !string.IsNullOrWhiteSpace(PostImageUrl);
    public LinkedInEditorialDraft EditorialDraft => LinkedInEditorialRefiner.Refine(LinkedInEditorialPostFormatter.Parse(Draft.PostText));
    public LinkedInDraftQualityAnalysis QualityAnalysis => EditorialDraft.QualityAnalysis ?? LinkedInEditorialQualityAnalyzer.Analyze(EditorialDraft);
    public IReadOnlyList<string> ValidationErrors =>
        JsonSerializer.Deserialize<List<string>>(Draft.ValidationErrorsJson) ?? [];
    public bool IsReadyToPublish => ValidationErrors.Count == 0 && QualityAnalysis.ReadabilityScore != "Needs refinement";
    public bool CanRetryPublish => Draft.Status == AiNewsCurator.Domain.Enums.DraftStatus.Failed;
    public bool HasFailedPublication => LatestPublication?.Status == PublicationStatus.Failed;
    public int CharacterCount => Draft.PostText.Length;
    public string ReadinessLabel => IsReadyToPublish ? "Ready to publish" : "Needs review";
    public string SourceUrl => NewsItem?.CanonicalUrl ?? NewsItem?.Url ?? string.Empty;
    public string EditorialProfileLabel => BuildEditorialProfileLabel(LatestCuration?.PromptVersion);
    public string LatestPublicationFailureSummary => BuildLatestPublicationFailureSummary();
    public string? LatestPublicationTechnicalDetails => BuildLatestPublicationTechnicalDetails();
    public string LatestPublicationFailureCategory => BuildLatestPublicationFailureCategory();
    public string LatestPublicationFailureGuidance => BuildLatestPublicationFailureGuidance();
    public string? LatestPublicationSentText => BuildLatestPublicationSentText();
    public string? LatestPublicationRequestPayload => NormalizePayload(LatestPublication?.RequestPayload);
    public string? LatestPublicationResponsePayload => NormalizePayload(LatestPublication?.ResponsePayload);
    public bool HasPublicationAudit =>
        !string.IsNullOrWhiteSpace(LatestPublicationSentText) ||
        !string.IsNullOrWhiteSpace(LatestPublicationRequestPayload) ||
        !string.IsNullOrWhiteSpace(LatestPublicationResponsePayload);
    public bool HasPublicationHistory => PublicationHistory.Count > 0;

    public bool PublishedWithImage =>
        LatestPublication?.RequestPayload?.Contains("\"shareMediaCategory\":\"IMAGE\"", StringComparison.OrdinalIgnoreCase) == true;

    private string BuildLatestPublicationFailureSummary()
    {
        if (!HasFailedPublication)
        {
            return string.Empty;
        }

        var primaryMessage = LatestPublication?.ErrorMessage;
        if (!string.IsNullOrWhiteSpace(primaryMessage))
        {
            return primaryMessage.Trim();
        }

        var responsePayload = LatestPublication?.ResponsePayload;
        if (string.IsNullOrWhiteSpace(responsePayload))
        {
            return "LinkedIn returned an unknown error.";
        }

        try
        {
            using var document = JsonDocument.Parse(responsePayload);
            var root = document.RootElement;
            if (TryReadProperty(root, "message", out var message) ||
                TryReadProperty(root, "error_description", out message) ||
                TryReadProperty(root, "error", out message))
            {
                return message;
            }
        }
        catch (JsonException)
        {
        }

        return responsePayload.Length <= 180
            ? responsePayload
            : $"{responsePayload[..180]}...";
    }

    private string? BuildLatestPublicationTechnicalDetails()
    {
        if (!HasFailedPublication || string.IsNullOrWhiteSpace(LatestPublication?.ResponsePayload))
        {
            return null;
        }

        var responsePayload = LatestPublication.ResponsePayload.Trim();
        return string.Equals(responsePayload, LatestPublicationFailureSummary, StringComparison.Ordinal)
            ? null
            : responsePayload;
    }

    private string BuildLatestPublicationFailureCategory()
    {
        if (!HasFailedPublication)
        {
            return string.Empty;
        }

        var fingerprint = BuildFailureFingerprint();
        if (ContainsAny(fingerprint, "401", "403", "unauthorized", "forbidden", "credential", "access token", "refresh token", "oauth", "not configured"))
        {
            return "Connection issue";
        }

        if (ContainsAny(fingerprint, "429", "rate limit", "throttle", "quota"))
        {
            return "Rate limit";
        }

        if (ContainsAny(fingerprint, "400", "422", "invalid", "validation", "duplicate"))
        {
            return "Content or request issue";
        }

        if (ContainsAny(fingerprint, "500", "502", "503", "504", "timeout", "temporary", "transient", "unavailable"))
        {
            return "Likely retryable";
        }

        return "Needs review";
    }

    private string BuildLatestPublicationFailureGuidance()
    {
        if (!HasFailedPublication)
        {
            return string.Empty;
        }

        return LatestPublicationFailureCategory switch
        {
            "Connection issue" => "Validate or refresh LinkedIn credentials before retrying.",
            "Rate limit" => "Wait a bit before retrying to avoid another immediate failure.",
            "Content or request issue" => "Review and edit the draft before retrying publication.",
            "Likely retryable" => "This looks transient. Retrying the publication should be safe.",
            _ => "Review the technical details before retrying publication."
        };
    }

    private string BuildFailureFingerprint()
    {
        return string.Join(
            "\n",
            LatestPublication?.ErrorMessage ?? string.Empty,
            LatestPublication?.ResponsePayload ?? string.Empty).ToLowerInvariant();
    }

    private string? BuildLatestPublicationSentText()
    {
        var requestPayload = LatestPublication?.RequestPayload;
        if (string.IsNullOrWhiteSpace(requestPayload))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(requestPayload);
            var root = document.RootElement;
            if (root.TryGetProperty("specificContent", out var specificContent) &&
                specificContent.TryGetProperty("com.linkedin.ugc.ShareContent", out var shareContent) &&
                shareContent.TryGetProperty("shareCommentary", out var shareCommentary) &&
                shareCommentary.TryGetProperty("text", out var textElement) &&
                textElement.ValueKind == JsonValueKind.String)
            {
                var text = textElement.GetString()?.Trim();
                return string.IsNullOrWhiteSpace(text) ? null : text;
            }
        }
        catch (JsonException)
        {
        }

        return null;
    }

    private static bool TryReadProperty(JsonElement root, string propertyName, out string value)
    {
        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty(propertyName, out var property) &&
            property.ValueKind == JsonValueKind.String)
        {
            value = property.GetString()?.Trim() ?? string.Empty;
            return !string.IsNullOrWhiteSpace(value);
        }

        value = string.Empty;
        return false;
    }

    private static bool ContainsAny(string value, params string[] fragments)
    {
        return fragments.Any(fragment => value.Contains(fragment, StringComparison.Ordinal));
    }

    private static string BuildEditorialProfileLabel(string? promptVersion)
    {
        if (string.IsNullOrWhiteSpace(promptVersion))
        {
            return "General AI";
        }

        if (promptVersion.Contains("dotnet", StringComparison.OrdinalIgnoreCase))
        {
            return ".NET / C#";
        }

        return "General AI";
    }

    private static string? NormalizePayload(string? payload)
    {
        return string.IsNullOrWhiteSpace(payload) ? null : payload.Trim();
    }
}

using AiNewsCurator.Domain.Entities;

namespace AiNewsCurator.Api.Models.Operations;

public sealed class OperationsNewsItemViewModel
{
    public required NewsItem NewsItem { get; init; }
    public UpdateNewsImageFormModel ImageForm => new()
    {
        ImageUrl = NewsItem.ImageUrl ?? string.Empty
    };
    public CurationResult? LatestCuration { get; init; }
    public string? SourceName { get; init; }
    public bool IsPublishedToLinkedIn { get; init; }
    public DateTimeOffset? LinkedInPublishedAt { get; init; }
    public string? LinkedInPostId { get; init; }
    public string EditorialProfileLabel => BuildEditorialProfileLabel(LatestCuration?.PromptVersion);

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
}

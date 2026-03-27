using System.Text.Json;
using AiNewsCurator.Api.Operations;
using AiNewsCurator.Domain.Entities;

namespace AiNewsCurator.Api.Models.Operations;

public sealed class OperationsSourceViewModel
{
    public required Source Source { get; init; }
    public string EditorialProfileLabel => SourceInputMapper.ResolveEditorialProfileLabel(Source);

    public UpdateSourceFormModel EditForm => new()
    {
        EditorialProfile = SourceInputMapper.ResolveEditorialProfile(Source),
        Name = Source.Name,
        Type = Source.Type.ToString(),
        Url = Source.Url,
        Language = Source.Language,
        IsActive = Source.IsActive,
        Priority = Source.Priority,
        MaxItemsPerRun = Source.MaxItemsPerRun,
        IncludeKeywords = ToCsv(Source.IncludeKeywordsJson),
        ExcludeKeywords = ToCsv(Source.ExcludeKeywordsJson),
        Tags = ToCsv(Source.TagsJson)
    };

    private static string ToCsv(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return string.Empty;
        }

        var items = JsonSerializer.Deserialize<string[]>(json) ?? [];
        return string.Join(", ", items);
    }
}

using AiNewsCurator.Domain.Enums;

namespace AiNewsCurator.Domain.Entities;

public sealed class Source
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public SourceType Type { get; set; }
    public string Url { get; set; } = string.Empty;
    public string Language { get; set; } = "en";
    public bool IsActive { get; set; } = true;
    public int Priority { get; set; }
    public int MaxItemsPerRun { get; set; } = 10;
    public string IncludeKeywordsJson { get; set; } = "[]";
    public string ExcludeKeywordsJson { get; set; } = "[]";
    public string TagsJson { get; set; } = "[]";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

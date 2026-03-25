namespace AiNewsCurator.Api.Models.Operations;

public sealed class UpdateSourceFormModel
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "Rss";
    public string Url { get; set; } = string.Empty;
    public string Language { get; set; } = "en";
    public bool IsActive { get; set; } = true;
    public int Priority { get; set; }
    public int MaxItemsPerRun { get; set; } = 10;
    public string IncludeKeywords { get; set; } = string.Empty;
    public string ExcludeKeywords { get; set; } = string.Empty;
    public string Tags { get; set; } = string.Empty;
}

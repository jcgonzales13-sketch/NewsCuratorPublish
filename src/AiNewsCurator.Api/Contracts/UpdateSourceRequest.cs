namespace AiNewsCurator.Api.Contracts;

public sealed class UpdateSourceRequest
{
    public string EditorialProfile { get; set; } = "auto";
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "Rss";
    public string Url { get; set; } = string.Empty;
    public string Language { get; set; } = "en";
    public bool IsActive { get; set; } = true;
    public int Priority { get; set; } = 5;
    public int MaxItemsPerRun { get; set; } = 10;
    public string[] IncludeKeywords { get; set; } = [];
    public string[] ExcludeKeywords { get; set; } = [];
    public string[] Tags { get; set; } = [];
}

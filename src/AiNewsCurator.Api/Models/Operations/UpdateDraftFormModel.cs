namespace AiNewsCurator.Api.Models.Operations;

public sealed class UpdateDraftFormModel
{
    public string? TitleSuggestion { get; set; }
    public string PostText { get; set; } = string.Empty;
}

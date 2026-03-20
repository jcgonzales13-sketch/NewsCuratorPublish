namespace AiNewsCurator.Api.Models.Operations;

public sealed class OperationsLoginViewModel
{
    public string ApiKey { get; set; } = string.Empty;
    public string ReturnUrl { get; set; } = "/ops";
    public string? ErrorMessage { get; set; }
}

namespace AiNewsCurator.Api.Models.Operations;

public sealed class RequestOpsLoginCodeFormModel
{
    public string Email { get; set; } = string.Empty;
    public string ReturnUrl { get; set; } = "/ops";
}

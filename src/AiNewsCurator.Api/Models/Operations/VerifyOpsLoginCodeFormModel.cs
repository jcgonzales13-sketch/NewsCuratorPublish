namespace AiNewsCurator.Api.Models.Operations;

public sealed class VerifyOpsLoginCodeFormModel
{
    public string Email { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string ReturnUrl { get; set; } = "/ops";
}

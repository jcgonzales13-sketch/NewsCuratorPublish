namespace AiNewsCurator.Api.Models.Operations;

public sealed class LoginOpsFormModel
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ReturnUrl { get; set; } = "/ops";
}

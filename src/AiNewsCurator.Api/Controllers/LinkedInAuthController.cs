using AiNewsCurator.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AiNewsCurator.Api.Controllers;

[ApiController]
[Route("internal/auth/linkedin")]
public sealed class LinkedInAuthController : ControllerBase
{
    private readonly ILinkedInAuthService _linkedInAuthService;
    private readonly ILinkedInPublisher _linkedInPublisher;

    public LinkedInAuthController(ILinkedInAuthService linkedInAuthService, ILinkedInPublisher linkedInPublisher)
    {
        _linkedInAuthService = linkedInAuthService;
        _linkedInPublisher = linkedInPublisher;
    }

    [HttpGet("status")]
    public async Task<IActionResult> Status(CancellationToken cancellationToken)
    {
        var status = await _linkedInAuthService.GetStatusAsync(cancellationToken);
        return Ok(status);
    }

    [HttpPost("start")]
    public async Task<IActionResult> Start(CancellationToken cancellationToken)
    {
        var authorizationUrl = await _linkedInAuthService.CreateAuthorizationUrlAsync(cancellationToken);
        return Ok(new { authorizationUrl });
    }

    [HttpPost("validate")]
    public async Task<IActionResult> Validate(CancellationToken cancellationToken)
    {
        var result = await _linkedInPublisher.ValidateCredentialsAsync(cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("callback")]
    public async Task<IActionResult> Callback(
        [FromQuery] string? code,
        [FromQuery] string? state,
        [FromQuery] string? error,
        [FromQuery(Name = "error_description")] string? errorDescription,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state))
        {
            var failed = await _linkedInAuthService.HandleCallbackAsync(string.Empty, state ?? string.Empty, error ?? "missing_code", errorDescription, cancellationToken);
            return Content(BuildHtml(failed.Success, failed.Message, failed.MemberName, failed.MemberEmail), "text/html");
        }

        var result = await _linkedInAuthService.HandleCallbackAsync(code, state, error, errorDescription, cancellationToken);
        return Content(BuildHtml(result.Success, result.Message, result.MemberName, result.MemberEmail), "text/html");
    }

    private static string BuildHtml(bool success, string message, string? memberName, string? memberEmail)
    {
        var title = success ? "LinkedIn conectado" : "Falha na conexao LinkedIn";
        var details = success
            ? $"<p><strong>Nome:</strong> {System.Net.WebUtility.HtmlEncode(memberName ?? "-")}</p><p><strong>Email:</strong> {System.Net.WebUtility.HtmlEncode(memberEmail ?? "-")}</p>"
            : string.Empty;

        return
            $"""
            <!doctype html>
            <html lang="pt-BR">
            <head>
              <meta charset="utf-8" />
              <meta name="viewport" content="width=device-width, initial-scale=1" />
              <title>{title}</title>
            </head>
            <body style="font-family: Arial, sans-serif; padding: 24px; max-width: 680px; margin: 0 auto;">
              <h1>{title}</h1>
              <p>{System.Net.WebUtility.HtmlEncode(message)}</p>
              {details}
              <p>Voce pode fechar esta janela e voltar para a aplicacao.</p>
            </body>
            </html>
            """;
    }
}

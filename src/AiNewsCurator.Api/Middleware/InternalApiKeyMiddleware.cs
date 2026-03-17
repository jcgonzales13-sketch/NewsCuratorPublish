using AiNewsCurator.Application.DTOs;
using Microsoft.Extensions.Options;

namespace AiNewsCurator.Api.Middleware;

public sealed class InternalApiKeyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly AppOptions _options;

    public InternalApiKeyMiddleware(RequestDelegate next, IOptions<AppOptions> options)
    {
        _next = next;
        _options = options.Value;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/internal/auth/linkedin/callback", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        if (!context.Request.Path.StartsWithSegments("/internal", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        if (context.Request.Headers.TryGetValue("X-API-Key", out var key) &&
            string.Equals(key.ToString(), _options.InternalApiKey, StringComparison.Ordinal))
        {
            await _next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsJsonAsync(new { error = "Invalid API key." });
    }
}

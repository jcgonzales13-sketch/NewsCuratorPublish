using AiNewsCurator.Application.DTOs;
using AiNewsCurator.Api.Operations;
using Microsoft.Extensions.Options;

namespace AiNewsCurator.Api.Middleware;

public sealed class OperationsAccessMiddleware
{
    private readonly RequestDelegate _next;
    private readonly AppOptions _options;

    public OperationsAccessMiddleware(RequestDelegate next, IOptions<AppOptions> options)
    {
        _next = next;
        _options = options.Value;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/ops", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        if (context.Request.Path.StartsWithSegments("/ops/login", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        if (context.Request.Cookies.TryGetValue(OperationsAuthCookie.CookieName, out var cookieValue) &&
            OperationsAuthCookie.Matches(cookieValue, _options.InternalApiKey))
        {
            await _next(context);
            return;
        }

        var returnUrl = $"{context.Request.Path}{context.Request.QueryString}";
        context.Response.Redirect($"/ops/login?returnUrl={Uri.EscapeDataString(returnUrl)}");
    }
}

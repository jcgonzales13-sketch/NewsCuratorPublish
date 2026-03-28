using Microsoft.AspNetCore.Authentication;

namespace AiNewsCurator.Api.Middleware;

public sealed class OperationsAccessMiddleware
{
    private readonly RequestDelegate _next;

    public OperationsAccessMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/ops", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        if (context.Request.Path.StartsWithSegments("/ops/login", StringComparison.OrdinalIgnoreCase) ||
            context.Request.Path.StartsWithSegments("/ops/auth", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var authResult = await context.AuthenticateAsync(Operations.OpsCookieAuthenticationDefaults.Scheme);
        if (authResult.Succeeded && authResult.Principal?.Identity?.IsAuthenticated == true)
        {
            context.User = authResult.Principal;
            await _next(context);
            return;
        }

        var returnUrl = $"{context.Request.Path}{context.Request.QueryString}";
        var loginUrl = $"/ops/login?reason=session-expired&returnUrl={Uri.EscapeDataString(returnUrl)}";
        context.Response.Redirect(loginUrl, permanent: false);
        if (!HttpMethods.IsGet(context.Request.Method))
        {
            context.Response.StatusCode = StatusCodes.Status303SeeOther;
        }
    }
}

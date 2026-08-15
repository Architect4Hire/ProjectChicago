using Microsoft.AspNetCore.Antiforgery;

namespace ProjectChicago.Gateway.Csrf;

/// <summary>
/// CSRF validation middleware for the BFF pattern (ADR-0018-superseding).
/// Validates CSRF tokens on all mutating requests (/api/**, /auth/*).
/// </summary>
public class CsrfValidationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IAntiforgery _antiforgery;

    // Routes that require CSRF validation (mutating methods only)
    private static readonly string[] CsrfValidationPaths = { "/api/", "/auth/" };
    private static readonly string[] SafeMethods = { "GET", "HEAD", "OPTIONS" };
    private static readonly string[] ExemptPaths = { "/auth/login" }; // POST /auth/login doesn't need CSRF (no session yet)

    public CsrfValidationMiddleware(RequestDelegate next, IAntiforgery antiforgery)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(antiforgery);
        _next = next;
        _antiforgery = antiforgery;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var request = context.Request;

        // Skip CSRF validation for safe methods
        if (Array.Exists(SafeMethods, m => m.Equals(request.Method, StringComparison.Ordinal)))
        {
            await _next(context);
            return;
        }

        // Skip CSRF validation for exempted paths
        if (IsExemptPath(request.Path))
        {
            await _next(context);
            return;
        }

        // Check if this path requires CSRF validation
        if (!RequiresCsrfValidation(request.Path))
        {
            await _next(context);
            return;
        }

        // Validate CSRF token
        try
        {
            await _antiforgery.ValidateRequestAsync(context);
        }
        catch (AntiforgeryValidationException ex)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new
            {
                type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                title = "Bad Request",
                status = StatusCodes.Status400BadRequest,
                detail = "CSRF token validation failed",
                traceId = context.TraceIdentifier,
            });
            return;
        }

        await _next(context);
    }

    private static bool RequiresCsrfValidation(PathString path)
    {
        var pathStr = path.Value.ToLowerInvariant();
        return CsrfValidationPaths.Any(p => pathStr.StartsWith(p));
    }

    private static bool IsExemptPath(PathString path)
    {
        var pathStr = path.Value.ToLowerInvariant();
        return ExemptPaths.Any(p => pathStr.Equals(p));
    }
}

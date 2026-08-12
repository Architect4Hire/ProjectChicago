using System.Collections.Immutable;
using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using ProjectChicago.Shared.Correlation;

namespace ProjectChicago.ServiceDefaults.Correlation;

public static class HttpRequestContextFactory
{
    public const string CorrelationIdHeaderName = "X-Correlation-Id";
    public const string CausationIdHeaderName = "X-Causation-Id";
    public const string RequestIdHeaderName = "X-Request-Id";

    private const int MaxPropagatedHeaderLength = 128;

    public static RequestContext Create(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        return RequestContext.FromPropagated(
            traceId: ResolveTraceId(),
            correlationId: ResolvePropagatedHeader(httpContext, CorrelationIdHeaderName),
            causationId: ResolvePropagatedHeader(httpContext, CausationIdHeaderName),
            requestId: ResolvePropagatedHeader(httpContext, RequestIdHeaderName),
            actor: ResolveActor(httpContext.User));
    }

    private static string? ResolveTraceId()
    {
        var traceId = Activity.Current?.TraceId;
        return traceId is { } id && id != default ? id.ToHexString() : null;
    }

    // Diagnostic correlation headers are opaque identifiers, not trust decisions, but a caller
    // (malicious or buggy) can still send oversized, control-character, or duplicated header
    // values. Treat anything outside a plain single-valued token as absent rather than
    // propagating it into logs/telemetry/downstream calls.
    private static string? ResolvePropagatedHeader(HttpContext httpContext, string headerName)
    {
        if (!httpContext.Request.Headers.TryGetValue(headerName, out var values) || values.Count != 1)
        {
            return null;
        }

        var value = values[0];
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        value = value.Trim();

        return value.Length <= MaxPropagatedHeaderLength && !ContainsControlCharacter(value)
            ? value
            : null;
    }

    private static bool ContainsControlCharacter(string value)
    {
        foreach (var character in value)
        {
            if (char.IsControl(character))
            {
                return true;
            }
        }

        return false;
    }

    // Actor identity/roles come only from the validated ClaimsPrincipal established by ASP.NET Core
    // authentication - never from client-supplied headers (identity.md).
    private static ActorContext ResolveActor(ClaimsPrincipal? user)
    {
        if (user?.Identity is not { IsAuthenticated: true })
        {
            return ActorContext.ForAnonymous();
        }

        var actorId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(actorId))
        {
            return ActorContext.ForAnonymous();
        }

        var roles = user.FindAll(ClaimTypes.Role)
            .Select(claim => claim.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct()
            .ToImmutableArray();

        return ActorContext.ForUser(actorId, roles);
    }
}

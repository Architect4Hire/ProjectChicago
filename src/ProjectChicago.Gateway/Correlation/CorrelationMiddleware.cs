using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using ProjectChicago.ServiceDefaults.Correlation;

namespace ProjectChicago.Gateway.Correlation;

// Edge-wide correlation normalization (gateway.md, TRACE-001..007). Accepts an already-valid
// propagated correlation identifier or creates one, folds the resolved value back onto the
// request so YARP forwards one canonical set of headers downstream, and echoes the safe
// correlation reference back to the caller.
public sealed class CorrelationMiddleware(RequestDelegate next, ILogger<CorrelationMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var requestContext = HttpRequestContextFactory.Create(context);

        context.Request.Headers[HttpRequestContextFactory.CorrelationIdHeaderName] = requestContext.CorrelationId;
        context.Request.Headers[HttpRequestContextFactory.RequestIdHeaderName] = requestContext.RequestId;
        if (requestContext.CausationId is { } causationId)
        {
            context.Request.Headers[HttpRequestContextFactory.CausationIdHeaderName] = causationId;
        }

        // Fold the resolved correlation id onto the ambient W3C Activity so it is visible on the
        // same span ASP.NET Core/OpenTelemetry already emit for this request (TRACE-002/003/005/006).
        Activity.Current?.SetTag("app.correlation_id", requestContext.CorrelationId);
        Activity.Current?.SetTag("app.request_id", requestContext.RequestId);

        // Structured, trace-correlated, payload-free (LOG-003/LOG-004, SEC-024/SEC-025).
        logger.LogDebug(
            "Gateway request correlated. CorrelationId={CorrelationId} RequestId={RequestId} TraceId={TraceId}",
            requestContext.CorrelationId,
            requestContext.RequestId,
            requestContext.TraceId);

        context.Response.OnStarting(static state =>
        {
            var (response, correlationId) = ((HttpResponse Response, string CorrelationId))state;
            response.Headers[HttpRequestContextFactory.CorrelationIdHeaderName] = correlationId;
            return Task.CompletedTask;
        }, (context.Response, requestContext.CorrelationId));

        await next(context);
    }
}

public static class CorrelationMiddlewareExtensions
{
    public static IApplicationBuilder UseCorrelation(this IApplicationBuilder app) =>
        app.UseMiddleware<CorrelationMiddleware>();
}

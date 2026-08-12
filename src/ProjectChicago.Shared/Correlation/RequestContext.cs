using System.Diagnostics;

namespace ProjectChicago.Shared.Correlation;

public readonly record struct RequestContext
{
    public string TraceId { get; }

    public string CorrelationId { get; }

    public string? CausationId { get; }

    public string RequestId { get; }

    public ActorContext Actor { get; }

    private RequestContext(string traceId, string correlationId, string? causationId, string requestId, ActorContext actor)
    {
        TraceId = traceId;
        CorrelationId = correlationId;
        CausationId = causationId;
        RequestId = requestId;
        Actor = actor;
    }

    public static RequestContext CreateNew(ActorContext? actor = null) =>
        new(
            traceId: ResolveTraceId(),
            correlationId: NewId(),
            causationId: null,
            requestId: NewId(),
            actor: actor ?? ActorContext.Unknown);

    public static RequestContext FromPropagated(
        string? traceId,
        string? correlationId,
        string? causationId,
        string? requestId,
        ActorContext? actor = null) =>
        new(
            traceId: string.IsNullOrWhiteSpace(traceId) ? ResolveTraceId() : traceId,
            correlationId: string.IsNullOrWhiteSpace(correlationId) ? NewId() : correlationId,
            causationId: string.IsNullOrWhiteSpace(causationId) ? null : causationId,
            requestId: string.IsNullOrWhiteSpace(requestId) ? NewId() : requestId,
            actor: actor ?? ActorContext.Unknown);

    public RequestContext CreateCaused(ActorContext? actor = null) =>
        new(
            traceId: TraceId,
            correlationId: CorrelationId,
            causationId: RequestId,
            requestId: NewId(),
            actor: actor ?? Actor);

    private static string NewId() => Guid.NewGuid().ToString("n");

    // Reuse the ambient OpenTelemetry Activity's W3C trace ID when one is active (TRACE-002/TRACE-003)
    // so logs and events emitted without HTTP context still correlate to the same distributed trace.
    private static string ResolveTraceId()
    {
        var activityTraceId = Activity.Current?.TraceId;
        return activityTraceId is { } traceId && traceId != default
            ? traceId.ToHexString()
            : ActivityTraceId.CreateRandom().ToHexString();
    }
}

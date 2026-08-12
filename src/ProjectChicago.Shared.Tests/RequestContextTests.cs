using System.Diagnostics;
using ProjectChicago.Shared.Correlation;
using Xunit;

namespace ProjectChicago.Shared.Tests;

public class RequestContextTests
{
    [Fact]
    public void CreateNew_GeneratesDistinctNonEmptyIdentifiers()
    {
        var context = RequestContext.CreateNew();

        Assert.False(string.IsNullOrWhiteSpace(context.TraceId));
        Assert.False(string.IsNullOrWhiteSpace(context.CorrelationId));
        Assert.False(string.IsNullOrWhiteSpace(context.RequestId));
        Assert.Null(context.CausationId);
        Assert.NotEqual(context.CorrelationId, context.RequestId);
    }

    [Fact]
    public void CreateNew_DefaultsActorToUnknownWhenNotSupplied()
    {
        var context = RequestContext.CreateNew();

        Assert.Equal(ActorContext.Unknown, context.Actor);
    }

    [Fact]
    public void CreateNew_UsesSuppliedActor()
    {
        var actor = ActorContext.ForUser("user-123");

        var context = RequestContext.CreateNew(actor);

        Assert.Equal(actor, context.Actor);
    }

    [Fact]
    public void CreateNew_ReusesAmbientActivityTraceId()
    {
        using var activity = new Activity("test-operation");
        activity.Start();

        var context = RequestContext.CreateNew();

        Assert.Equal(activity.TraceId.ToHexString(), context.TraceId);
    }

    [Fact]
    public void CreateNew_GeneratesTraceIdWhenNoActivityIsActive()
    {
        Assert.Null(Activity.Current);

        var context = RequestContext.CreateNew();

        Assert.False(string.IsNullOrWhiteSpace(context.TraceId));
    }

    [Fact]
    public void FromPropagated_PreservesAllSuppliedValues()
    {
        var actor = ActorContext.ForService("notification-consumer");

        var context = RequestContext.FromPropagated(
            traceId: "4bf92f3577b34da6a3ce929d0e0e4736",
            correlationId: "correlation-1",
            causationId: "causation-1",
            requestId: "request-1",
            actor: actor);

        Assert.Equal("4bf92f3577b34da6a3ce929d0e0e4736", context.TraceId);
        Assert.Equal("correlation-1", context.CorrelationId);
        Assert.Equal("causation-1", context.CausationId);
        Assert.Equal("request-1", context.RequestId);
        Assert.Equal(actor, context.Actor);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FromPropagated_GeneratesMissingCorrelationAndRequestIds(string? missing)
    {
        var context = RequestContext.FromPropagated(
            traceId: "4bf92f3577b34da6a3ce929d0e0e4736",
            correlationId: missing,
            causationId: missing,
            requestId: missing);

        Assert.False(string.IsNullOrWhiteSpace(context.CorrelationId));
        Assert.False(string.IsNullOrWhiteSpace(context.RequestId));
        Assert.Null(context.CausationId);
    }

    [Fact]
    public void FromPropagated_GeneratesTraceIdWhenMissing()
    {
        var context = RequestContext.FromPropagated(
            traceId: null,
            correlationId: "correlation-1",
            causationId: null,
            requestId: "request-1");

        Assert.False(string.IsNullOrWhiteSpace(context.TraceId));
    }

    [Fact]
    public void CreateCaused_PreservesTraceAndCorrelationButPointsCausationAtParentRequest()
    {
        var parent = RequestContext.CreateNew(ActorContext.ForUser("user-123"));

        var child = parent.CreateCaused();

        Assert.Equal(parent.TraceId, child.TraceId);
        Assert.Equal(parent.CorrelationId, child.CorrelationId);
        Assert.Equal(parent.RequestId, child.CausationId);
        Assert.NotEqual(parent.RequestId, child.RequestId);
        Assert.Equal(parent.Actor, child.Actor);
    }

    [Fact]
    public void CreateCaused_UsesSuppliedActorOverParent()
    {
        var parent = RequestContext.CreateNew(ActorContext.ForUser("user-123"));
        var relayActor = ActorContext.ForService("crm-outbox-relay");

        var child = parent.CreateCaused(relayActor);

        Assert.Equal(relayActor, child.Actor);
    }

    [Fact]
    public void CreateCaused_ChainPreservesOriginalCorrelationAcrossMultipleHops()
    {
        var request = RequestContext.CreateNew();
        var outboxRelay = request.CreateCaused(ActorContext.ForService("crm-outbox-relay"));
        var consumerFunction = outboxRelay.CreateCaused(ActorContext.ForService("audit-consumer"));

        Assert.Equal(request.CorrelationId, outboxRelay.CorrelationId);
        Assert.Equal(request.CorrelationId, consumerFunction.CorrelationId);
        Assert.Equal(request.RequestId, outboxRelay.CausationId);
        Assert.Equal(outboxRelay.RequestId, consumerFunction.CausationId);
    }
}

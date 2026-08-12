using ProjectChicago.Contracts.Audit;
using ProjectChicago.Shared.Messaging;
using Xunit;

namespace ProjectChicago.Shared.Tests;

public class EventEnvelopeSerializerTests
{
    private static EntityMutationAudited CreatePayload() => new()
    {
        EventId = "event-1",
        OccurredAtUtc = DateTimeOffset.Parse("2026-08-12T09:15:00Z"),
        SourceService = AuditSourceServices.Crm,
        EntityType = AuditEntityTypes.Client,
        EntityId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
        Action = AuditActions.StatusChanged,
        ActorType = AuditActorTypes.User,
        TraceId = "4bf92f3577b34da6a3ce929d0e0e4736",
        CorrelationId = "correlation-1",
    };

    private static EventEnvelope<EntityMutationAudited> CreateEnvelope() => new()
    {
        EventId = "event-1",
        ContractType = "Audit.EntityMutationAudited",
        ContractVersion = EntityMutationAudited.CurrentVersion,
        OccurredAtUtc = DateTimeOffset.Parse("2026-08-12T09:15:00Z"),
        CorrelationId = "correlation-1",
        CausationId = "causation-1",
        TraceId = "4bf92f3577b34da6a3ce929d0e0e4736",
        Payload = CreatePayload(),
    };

    [Fact]
    public void RoundTrip_PreservesEnvelopeAndPayload()
    {
        var envelope = CreateEnvelope();

        var json = EventEnvelopeSerializer.Serialize(envelope);
        var roundTripped = EventEnvelopeSerializer.Deserialize<EntityMutationAudited>(json, [EntityMutationAudited.CurrentVersion]);

        Assert.Equal(envelope, roundTripped);
    }

    [Fact]
    public void RoundTrip_PreservesNullCausationId()
    {
        var envelope = CreateEnvelope() with { CausationId = null };

        var json = EventEnvelopeSerializer.Serialize(envelope);
        var roundTripped = EventEnvelopeSerializer.Deserialize<EntityMutationAudited>(json, [EntityMutationAudited.CurrentVersion]);

        Assert.Null(roundTripped.CausationId);
    }

    [Fact]
    public void Serialize_IsDeterministic()
    {
        var envelope = CreateEnvelope();

        var first = EventEnvelopeSerializer.Serialize(envelope);
        var second = EventEnvelopeSerializer.Serialize(envelope);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Serialize_UsesCamelCasePropertyNames()
    {
        var json = EventEnvelopeSerializer.Serialize(CreateEnvelope());

        Assert.Contains("\"eventId\"", json);
        Assert.Contains("\"contractType\"", json);
        Assert.Contains("\"contractVersion\"", json);
        Assert.Contains("\"correlationId\"", json);
    }

    [Fact]
    public void Deserialize_UnsupportedVersion_ThrowsWithTypeAndVersionDetails()
    {
        var json = EventEnvelopeSerializer.Serialize(CreateEnvelope() with { ContractVersion = 99 });

        var exception = Assert.Throws<UnsupportedContractVersionException>(
            () => EventEnvelopeSerializer.Deserialize<EntityMutationAudited>(json, [EntityMutationAudited.CurrentVersion]));

        Assert.Equal("Audit.EntityMutationAudited", exception.ContractType);
        Assert.Equal(99, exception.ContractVersion);
        Assert.Equal([EntityMutationAudited.CurrentVersion], exception.SupportedVersions);
    }

    [Fact]
    public void Deserialize_UnknownContractType_IsRejectedByVersionCheck_WithoutBindingPayload()
    {
        var json = EventEnvelopeSerializer.Serialize(CreateEnvelope() with { ContractType = "Unknown.Contract", ContractVersion = 7 });

        var exception = Assert.Throws<UnsupportedContractVersionException>(
            () => EventEnvelopeSerializer.Deserialize<EntityMutationAudited>(json, [EntityMutationAudited.CurrentVersion]));

        Assert.Equal("Unknown.Contract", exception.ContractType);
    }

    [Fact]
    public void Deserialize_MalformedJson_ThrowsEnvelopeDeserializationException()
    {
        var exception = Assert.Throws<EnvelopeDeserializationException>(
            () => EventEnvelopeSerializer.Deserialize<EntityMutationAudited>("{ not valid json", [EntityMutationAudited.CurrentVersion]));

        Assert.NotNull(exception.InnerException);
    }

    [Fact]
    public void Deserialize_PayloadDoesNotMatchRequestedType_ThrowsEnvelopeDeserializationException()
    {
        // Version-supported and well-formed envelope, but Payload is missing EntityMutationAudited's
        // required members - this must fail as a malformed payload, not an unsupported version.
        var json = $$"""
            {
              "eventId": "event-1",
              "contractType": "Audit.EntityMutationAudited",
              "contractVersion": {{EntityMutationAudited.CurrentVersion}},
              "occurredAtUtc": "2026-08-12T09:15:00Z",
              "correlationId": "correlation-1",
              "causationId": null,
              "traceId": "4bf92f3577b34da6a3ce929d0e0e4736",
              "payload": { "unexpectedField": "no required EntityMutationAudited members here" }
            }
            """;

        Assert.Throws<EnvelopeDeserializationException>(
            () => EventEnvelopeSerializer.Deserialize<EntityMutationAudited>(json, [EntityMutationAudited.CurrentVersion]));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Deserialize_NullOrWhitespaceJson_Throws(string? json)
    {
        // ArgumentException.ThrowIfNullOrWhiteSpace throws ArgumentNullException for null and
        // ArgumentException for empty/whitespace - both derive from ArgumentException.
        Assert.ThrowsAny<ArgumentException>(
            () => EventEnvelopeSerializer.Deserialize<EntityMutationAudited>(json!, [EntityMutationAudited.CurrentVersion]));
    }

    [Fact]
    public void Serialize_NullEnvelope_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => EventEnvelopeSerializer.Serialize<EntityMutationAudited>(null!));
    }
}

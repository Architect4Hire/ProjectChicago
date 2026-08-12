namespace ProjectChicago.Shared.Messaging;

// Everything IServiceBusPublisher needs to construct a Service Bus message: an already-serialized
// EventEnvelope{TPayload} body (see EventEnvelopeSerializer.Serialize) plus the metadata that
// becomes native/application message properties. The publisher never re-parses Body to recover
// this metadata - the caller (which built the envelope) already has it at hand.
public sealed record OutboundServiceBusMessage
{
    public required string MessageId { get; init; }

    public required string ContractType { get; init; }

    public required int ContractVersion { get; init; }

    public required string CorrelationId { get; init; }

    public string? CausationId { get; init; }

    public required string TraceId { get; init; }

    public required string Body { get; init; }
}

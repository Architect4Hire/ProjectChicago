namespace ProjectChicago.Shared.Messaging;

// Generic transport envelope wrapping any ProjectChicago.Contracts payload for durable outbox
// storage and Service Bus transit (OUTBOX-005, ASYNC-004, TRACE-003..007). ContractType/
// ContractVersion sit outside Payload so a consumer can reject an unsupported version without
// first binding Payload to a specific CLR type (see EventEnvelopeSerializer). This is transport
// plumbing, not a business event type - it duplicates none of a contract's own domain fields and
// carries only what routing/correlation/version-gating needs regardless of payload shape.
public sealed record EventEnvelope<TPayload>
{
    public required string EventId { get; init; }

    public required string ContractType { get; init; }

    public required int ContractVersion { get; init; }

    public required DateTimeOffset OccurredAtUtc { get; init; }

    public required string CorrelationId { get; init; }

    public string? CausationId { get; init; }

    public required string TraceId { get; init; }

    public required TPayload Payload { get; init; }
}

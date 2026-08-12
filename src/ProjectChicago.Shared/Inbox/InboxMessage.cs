namespace ProjectChicago.Shared.Inbox;

// Inbox row for idempotent Service Bus-triggered consumption (ASYNC-005..008, AUDIT-004, DATA-006).
// The owning service's Data layer detects/registers this row, applies side effects, and marks it
// Completed in one transaction; MessageId is the explicit idempotency key - a duplicate delivery is
// a unique-constraint hit on the primary key, and the owning service treats an already-Completed row
// as a safe no-op. Service-owned: each service persists these against its own database only, never a
// shared table.
public sealed class InboxMessage
{
    // The inbound message/event ID (Service Bus ServiceBusReceivedMessage.MessageId, matching the
    // publisher's outbox-assigned event ID). This is the idempotency key: uniqueness is enforced by
    // it being the primary key, not by a separate lookup/index.
    public required string MessageId { get; set; }

    public required string ContractType { get; set; }

    public required int ContractVersion { get; set; }

    public required string CorrelationId { get; set; }

    public string? CausationId { get; set; }

    public required string TraceId { get; set; }

    public required DateTime ReceivedAtUtc { get; set; }

    public DateTime? ProcessingStartedAtUtc { get; set; }

    public DateTime? ProcessingCompletedAtUtc { get; set; }

    public InboxMessageStatus Status { get; set; } = InboxMessageStatus.Received;

    public int AttemptCount { get; set; }

    public DateTime? LastAttemptAtUtc { get; set; }

    public string? LastError { get; set; }

    // Lease fields give concurrent/duplicate deliveries a defined stale-recovery strategy
    // (messaging.md: "In-progress/stale handling must have a defined lease/recovery strategy if
    // concurrent delivery is possible"). RowVersion is the row's own optimistic-concurrency guard
    // (DATA-008) for the lease-claim/status-update itself.
    public string? LeaseOwner { get; set; }

    public DateTime? LeasedUntilUtc { get; set; }

    public byte[] RowVersion { get; set; } = [];
}

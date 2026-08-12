namespace ProjectChicago.Shared.Outbox;

// Transactional outbox row (OUTBOX-001..006, DATA-006). The owning service's Data layer inserts
// this in the same database transaction as the domain state change it describes; a timer-triggered
// relay Function in that service's Functions project later dispatches it to Service Bus and marks
// it Dispatched. Service-owned: each service persists these against its own database only, never a
// shared table.
public sealed class OutboxMessage
{
    public required Guid Id { get; set; }

    public required string ContractType { get; set; }

    public required int ContractVersion { get; set; }

    // Serialized contract payload (SQL Server-compatible representation - nvarchar(max) JSON, not
    // PostgreSQL jsonb). The relay publishes this verbatim; it is not re-derived from domain state.
    public required string Payload { get; set; }

    public required string CorrelationId { get; set; }

    public string? CausationId { get; set; }

    public required string TraceId { get; set; }

    // The business fact time carried in the published contract - preserved verbatim through relay,
    // distinct from CreatedAtUtc which is this row's own insertion time.
    public required DateTime OccurredAtUtc { get; set; }

    public required DateTime CreatedAtUtc { get; set; }

    public OutboxMessageStatus Status { get; set; } = OutboxMessageStatus.Pending;

    public DateTime? DispatchedAtUtc { get; set; }

    public int AttemptCount { get; set; }

    public DateTime? LastAttemptAtUtc { get; set; }

    public string? LastError { get; set; }

    // Lease fields let concurrent relay instances coordinate batch selection without double-dispatching
    // the same message (messaging.md: "Relay selection/lease must prevent uncontrolled duplicate
    // concurrent dispatch"). RowVersion is the row's own optimistic-concurrency guard (DATA-008) for
    // the lease-claim/status-update itself.
    public string? LeaseOwner { get; set; }

    public DateTime? LeasedUntilUtc { get; set; }

    public byte[] RowVersion { get; set; } = [];
}

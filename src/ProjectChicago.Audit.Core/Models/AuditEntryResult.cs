namespace ProjectChicago.Audit.Core.Models;

/// <summary>
/// Audit entry result DTO for queries (AUDIT-001..008, AUDIT-007).
/// Excludes RawEventPayload (forensics only, not for normal queries).
/// Supports safe display of audit trail and activity feeds.
/// </summary>
public sealed record AuditEntryResult
{
    /// <summary>
    /// Unique audit entry identifier.
    /// </summary>
    public required Guid AuditEntryId { get; init; }

    /// <summary>
    /// The business entity type (Client, Project, Task, ApplicationUser).
    /// </summary>
    public required string EntityType { get; init; }

    /// <summary>
    /// The business entity's identifier.
    /// </summary>
    public required Guid EntityId { get; init; }

    /// <summary>
    /// The action performed (Created, Updated, StatusChanged, etc.).
    /// </summary>
    public required string Action { get; init; }

    /// <summary>
    /// Machine-readable action category for filtering/reporting (WRITE, TRANSITION, ASSIGN, etc.).
    /// </summary>
    public required string ActionCategory { get; init; }

    /// <summary>
    /// The user ID performing the action; null for system/service-initiated mutations.
    /// </summary>
    public Guid? ActorUserId { get; init; }

    /// <summary>
    /// The actor type: User, System, Service, Anonymous.
    /// </summary>
    public required string ActorType { get; init; }

    /// <summary>
    /// Human-readable actor identifier (username, service name).
    /// </summary>
    public string? ActorDisplayName { get; init; }

    /// <summary>
    /// The bounded service that published the event (Crm, Identity, Notification).
    /// </summary>
    public required string SourceService { get; init; }

    /// <summary>
    /// Timestamp when the business event occurred (UTC).
    /// </summary>
    public required DateTime OccurredAtUtc { get; init; }

    /// <summary>
    /// Timestamp when the audit entry was persisted (UTC).
    /// </summary>
    public required DateTime AuditedAtUtc { get; init; }

    /// <summary>
    /// OpenTelemetry W3C trace context identifier for linking to distributed logs.
    /// </summary>
    public required string TraceId { get; init; }

    /// <summary>
    /// Correlation ID tracing the entire user request flow.
    /// </summary>
    public required string CorrelationId { get; init; }

    /// <summary>
    /// Causation ID pointing to the immediate cause; null for root-level actions.
    /// </summary>
    public string? CausationId { get; init; }

    /// <summary>
    /// JSON array of field names that changed. Field names only, never values.
    /// </summary>
    public required string ChangedFields { get; init; }

    /// <summary>
    /// JSON object mapping field names to their previous values (safe fields only).
    /// </summary>
    public string? PreviousValues { get; init; }

    /// <summary>
    /// JSON object mapping field names to their new values (safe fields only).
    /// </summary>
    public string? NewValues { get; init; }

    /// <summary>
    /// Human-readable summary for UI display.
    /// </summary>
    public string? SummaryDescription { get; init; }
}

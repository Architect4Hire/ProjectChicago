using ProjectChicago.Audit.Core.Models;
using ProjectChicago.Audit.Core.Persistence;
using ProjectChicago.Shared.Inbox;

namespace ProjectChicago.Audit.Core.Data;

/// <summary>
/// Data-layer seam for Audit Service append-only audit entry persistence with idempotent inbox-based consumption (ADR-0016, AUDIT-001..008, ASYNC-005..008).
/// Audit consumes integration events through Service Bus triggers, registers them idempotently via inbox pattern,
/// and appends immutable AuditEntry records in a single atomic transaction.
/// Audit also supports read-only queries for audit trails and activity displays.
/// </summary>
public interface IAuditData
{
    /// <summary>
    /// Idempotently appends an AuditEntry and marks the associated InboxMessage as Completed in a single transaction.
    ///
    /// Behavior:
    /// - First delivery: Creates new InboxMessage (Received → Processing → Completed) and creates new AuditEntry.
    /// - Duplicate delivery: If InboxMessage is already Completed, returns success (safe no-op).
    /// - Failure: If any error occurs, neither the AuditEntry is created nor the InboxMessage is marked Completed.
    ///
    /// Constraints (messaging.md, ASYNC-005..008):
    /// - The EventId field on AuditEntry enforces uniqueness per event across all retries/duplicates.
    /// - If a duplicate AuditEntry EventId already exists, the operation detects this and either returns success
    ///   (if the inbox is already completed) or rolls back (if inbox processing failed previously).
    /// - The InboxMessage is persisted atomically with the AuditEntry, or both are rolled back.
    ///
    /// Parameters:
    /// - auditEntry: The fully-constructed, audit-safe AuditEntry model with all required fields populated.
    /// - inboxMessage: The fully-constructed InboxMessage model with correlation/trace/contract metadata.
    /// - cancellationToken: Cancellation token for the async operation.
    ///
    /// Throws:
    /// - SqlException or provider-specific exceptions are allowed to propagate (caller/Function trigger handles retry).
    /// - ArgumentNullException if auditEntry or inboxMessage is null.
    ///
    /// (AUDIT-001..008, ASYNC-005, OUTBOX-001/002 atomicity constraint)
    /// </summary>
    Task AppendAuditEntryIdempotentlyAsync(
        AuditEntry auditEntry,
        InboxMessage inboxMessage,
        CancellationToken cancellationToken);

    /// <summary>
    /// Query audit entries by entity type and ID with pagination (AUDIT-001..008, AUDIT-007, PERF-003/004).
    /// Delegates to repository; returns read-only materialized results ordered by OccurredAtUtc descending.
    /// </summary>
    Task<AuditListResult> QueryByEntityAsync(
        string entityType,
        Guid entityId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);

    /// <summary>
    /// Query audit entries by trace ID or correlation ID with pagination (AUDIT-007, PERF-003/004).
    /// Supports distributed request tracing through W3C trace context and cross-service correlation.
    /// Delegates to repository; returns read-only materialized results ordered by OccurredAtUtc descending.
    /// </summary>
    Task<AuditListResult> QueryByTraceOrCorrelationIdAsync(
        string? traceId,
        string? correlationId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);
}

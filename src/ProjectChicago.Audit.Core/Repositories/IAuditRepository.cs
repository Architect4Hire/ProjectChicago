using ProjectChicago.Audit.Core.Models;

namespace ProjectChicago.Audit.Core.Repositories;

/// <summary>
/// Persistence-operation seam for read-only audit entry queries (AUDIT-001..008, AUDIT-007).
/// Supports querying by entity identifier or by trace/correlation ID with pagination (PERF-003/004).
/// Repository queries are read-only and ordered consistently by OccurredAtUtc descending.
/// </summary>
public interface IAuditRepository
{
    /// <summary>
    /// Query all audit entries for a specific entity (Client, Project, Task, etc.) with pagination.
    ///
    /// Behavior:
    /// - Filters by EntityType AND EntityId (both required).
    /// - Orders results by OccurredAtUtc descending (newest first).
    /// - Returns requested page with skip/take applied.
    /// - TotalCount reflects complete filtered set (all pages).
    ///
    /// Parameters:
    /// - entityType: Required, case-sensitive (e.g., "Client", "Project", "Task").
    /// - entityId: Required, non-empty GUID of the entity.
    /// - pageNumber: 1-based page number (1 = first page).
    /// - pageSize: Number of entries per page (typically 10-100).
    ///
    /// Returns:
    /// - AuditListResult with Items (empty list if no matches) and TotalCount.
    ///
    /// Constraints (AUDIT-004, PERF-003/004):
    /// - No cross-service database queries.
    /// - Append-only (no updates/deletes to audit entries).
    /// - Results exclude RawEventPayload.
    /// </summary>
    Task<AuditListResult> QueryByEntityAsync(
        string entityType,
        Guid entityId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);

    /// <summary>
    /// Query all audit entries linked to a specific request trace or correlation.
    ///
    /// Behavior:
    /// - Filters by TraceId (if provided) OR CorrelationId (if provided).
    /// - If both provided, filters by either (OR logic).
    /// - Orders results by OccurredAtUtc descending (newest first).
    /// - Returns requested page with skip/take applied.
    /// - TotalCount reflects complete filtered set.
    ///
    /// Parameters:
    /// - traceId: Optional, W3C trace context identifier (AUDIT-007, ADR-0021).
    /// - correlationId: Optional, cross-service request correlation identifier.
    /// - At least one of traceId or correlationId must be non-null/non-empty.
    /// - pageNumber: 1-based page number (1 = first page).
    /// - pageSize: Number of entries per page (typically 10-100).
    ///
    /// Returns:
    /// - AuditListResult with Items (empty list if no matches) and TotalCount.
    ///
    /// Constraints (AUDIT-007, PERF-003/004):
    /// - No cross-service database queries.
    /// - Results exclude RawEventPayload.
    /// - Supports distributed-trace reconstruction for operations investigation.
    /// </summary>
    Task<AuditListResult> QueryByTraceOrCorrelationIdAsync(
        string? traceId,
        string? correlationId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);
}

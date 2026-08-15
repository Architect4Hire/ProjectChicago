using ProjectChicago.Audit.Core.Models;

namespace ProjectChicago.Audit.Core.Facades;

/// <summary>
/// Application-layer seam for read-only audit entry queries (AUDIT-001..008, AUDIT-007).
/// Facade owns input validation, use-case orchestration, and delegation to business operations.
/// Provides controller-ready operations for retrieving audit trails and activity information.
/// </summary>
public interface IAuditReadFacade
{
    /// <summary>
    /// Retrieve paginated audit entries for a specific entity (AUDIT-001..008).
    ///
    /// Parameters:
    /// - entityType: Required, non-empty entity type identifier (e.g., "Client", "Project", "Task").
    /// - entityId: Required, non-empty GUID of the entity.
    /// - pageNumber: 1-based page number (validated to be >= 1).
    /// - pageSize: Entries per page (validated to be 1-1000 by default).
    ///
    /// Returns:
    /// - AuditListResult with paginated entries ordered by OccurredAtUtc descending.
    ///
    /// Throws:
    /// - ArgumentException if any parameter is invalid (validation failures).
    /// </summary>
    Task<AuditListResult> GetAuditByEntityAsync(
        string entityType,
        Guid entityId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);

    /// <summary>
    /// Retrieve paginated audit entries linked to a distributed request trace or correlation (AUDIT-007, ADR-0021).
    ///
    /// Supports operational traceability: find all audit entries across services linked to a single
    /// request by providing either its W3C trace ID or cross-service correlation ID.
    ///
    /// Parameters:
    /// - traceId: Optional, W3C trace context identifier.
    /// - correlationId: Optional, cross-service request correlation identifier.
    /// - At least one must be provided (both validated to be non-empty if provided).
    /// - pageNumber: 1-based page number (validated to be >= 1).
    /// - pageSize: Entries per page (validated to be 1-1000 by default).
    ///
    /// Returns:
    /// - AuditListResult with paginated entries ordered by OccurredAtUtc descending.
    ///
    /// Throws:
    /// - ArgumentException if parameters are invalid (no trace/correlation provided, invalid page number/size).
    /// </summary>
    Task<AuditListResult> GetAuditByTraceOrCorrelationAsync(
        string? traceId,
        string? correlationId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);
}

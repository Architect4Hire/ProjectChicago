using ProjectChicago.Audit.Core.Models;

namespace ProjectChicago.Audit.Core.Business;

/// <summary>
/// Business-layer seam for read-only audit entry queries (AUDIT-001..008, AUDIT-007).
/// Provides domain-oriented operations for retrieving audit trails and activity information.
/// Read operations are largely transparent delegations to the Data layer; business rules
/// for audit-safe field visibility and ordering are enforced at the repository/query level.
/// </summary>
public interface IAuditReadBusiness
{
    /// <summary>
    /// Retrieve audit entries for a specific entity with pagination (AUDIT-001..008).
    ///
    /// Parameters:
    /// - entityType: Required, the entity type (e.g., "Client", "Project", "Task").
    /// - entityId: Required, the entity's GUID identifier.
    /// - pageNumber: 1-based page number.
    /// - pageSize: Entries per page.
    ///
    /// Returns:
    /// - AuditListResult with paginated results ordered by OccurredAtUtc descending.
    /// </summary>
    Task<AuditListResult> GetAuditByEntityAsync(
        string entityType,
        Guid entityId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);

    /// <summary>
    /// Retrieve audit entries linked to a request trace or correlation ID with pagination (AUDIT-007, ADR-0021).
    ///
    /// Supports distributed request tracing: operators can follow a request through the entire system
    /// from gateway to database, across async Functions and Service Bus, by querying audit entries
    /// linked to a single trace ID or correlation ID.
    ///
    /// Parameters:
    /// - traceId: Optional, W3C trace context identifier.
    /// - correlationId: Optional, cross-service request correlation identifier.
    /// - At least one must be provided.
    /// - pageNumber: 1-based page number.
    /// - pageSize: Entries per page.
    ///
    /// Returns:
    /// - AuditListResult with paginated results ordered by OccurredAtUtc descending.
    /// </summary>
    Task<AuditListResult> GetAuditByTraceOrCorrelationAsync(
        string? traceId,
        string? correlationId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);
}

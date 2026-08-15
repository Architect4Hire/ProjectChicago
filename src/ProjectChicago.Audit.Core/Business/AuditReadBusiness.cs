using ProjectChicago.Audit.Core.Data;
using ProjectChicago.Audit.Core.Models;

namespace ProjectChicago.Audit.Core.Business;

/// <summary>
/// Business layer for read-only audit entry queries (AUDIT-001..008, AUDIT-007).
/// Delegates query operations to the Data layer, which composes repository access.
/// Audit reads are largely transparent: no domain rules to enforce, no state transitions,
/// no mutations. The repository and data layers enforce audit-safe field visibility and ordering.
/// </summary>
public class AuditReadBusiness : IAuditReadBusiness
{
    private readonly IAuditData _auditData;

    public AuditReadBusiness(IAuditData auditData)
    {
        _auditData = auditData ?? throw new ArgumentNullException(nameof(auditData));
    }

    /// <summary>
    /// Retrieve audit entries for a specific entity with pagination.
    /// See IAuditReadBusiness.GetAuditByEntityAsync for full contract.
    /// </summary>
    public async Task<AuditListResult> GetAuditByEntityAsync(
        string entityType,
        Guid entityId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityType);
        if (entityId == Guid.Empty)
            throw new ArgumentException("entityId must be a non-empty GUID", nameof(entityId));
        if (pageNumber < 1)
            throw new ArgumentException("pageNumber must be >= 1", nameof(pageNumber));
        if (pageSize < 1)
            throw new ArgumentException("pageSize must be >= 1", nameof(pageSize));

        return await _auditData.QueryByEntityAsync(entityType, entityId, pageNumber, pageSize, cancellationToken);
    }

    /// <summary>
    /// Retrieve audit entries linked to a request trace or correlation ID with pagination.
    /// See IAuditReadBusiness.GetAuditByTraceOrCorrelationAsync for full contract.
    /// </summary>
    public async Task<AuditListResult> GetAuditByTraceOrCorrelationAsync(
        string? traceId,
        string? correlationId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(traceId) && string.IsNullOrWhiteSpace(correlationId))
            throw new ArgumentException("At least one of traceId or correlationId must be non-empty");
        if (pageNumber < 1)
            throw new ArgumentException("pageNumber must be >= 1", nameof(pageNumber));
        if (pageSize < 1)
            throw new ArgumentException("pageSize must be >= 1", nameof(pageSize));

        return await _auditData.QueryByTraceOrCorrelationIdAsync(traceId, correlationId, pageNumber, pageSize, cancellationToken);
    }
}

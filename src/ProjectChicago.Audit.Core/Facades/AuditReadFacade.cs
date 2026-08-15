using ProjectChicago.Audit.Core.Business;
using ProjectChicago.Audit.Core.Models;

namespace ProjectChicago.Audit.Core.Facades;

/// <summary>
/// Application facade for read-only audit entry queries (AUDIT-001..008, AUDIT-007).
/// Owns input validation, use-case orchestration, and delegation to business operations.
/// Enforces page size bounds to prevent unbounded result sets (PERF-003).
/// </summary>
public class AuditReadFacade : IAuditReadFacade
{
    private const int MinPageSize = 1;
    private const int MaxPageSize = 1000;

    private readonly IAuditReadBusiness _auditReadBusiness;

    public AuditReadFacade(IAuditReadBusiness auditReadBusiness)
    {
        _auditReadBusiness = auditReadBusiness ?? throw new ArgumentNullException(nameof(auditReadBusiness));
    }

    /// <summary>
    /// Retrieve paginated audit entries for a specific entity.
    /// See IAuditReadFacade.GetAuditByEntityAsync for full contract.
    /// </summary>
    public async Task<AuditListResult> GetAuditByEntityAsync(
        string entityType,
        Guid entityId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        // Validate inputs.
        if (string.IsNullOrWhiteSpace(entityType))
            throw new ArgumentException("entityType is required and non-empty", nameof(entityType));
        if (entityId == Guid.Empty)
            throw new ArgumentException("entityId must be a non-empty GUID", nameof(entityId));
        if (pageNumber < 1)
            throw new ArgumentException("pageNumber must be >= 1", nameof(pageNumber));
        if (pageSize < MinPageSize || pageSize > MaxPageSize)
            throw new ArgumentException($"pageSize must be between {MinPageSize} and {MaxPageSize}", nameof(pageSize));

        // Delegate to business layer.
        return await _auditReadBusiness.GetAuditByEntityAsync(
            entityType,
            entityId,
            pageNumber,
            pageSize,
            cancellationToken);
    }

    /// <summary>
    /// Retrieve paginated audit entries linked to a distributed request trace or correlation.
    /// See IAuditReadFacade.GetAuditByTraceOrCorrelationAsync for full contract.
    /// </summary>
    public async Task<AuditListResult> GetAuditByTraceOrCorrelationAsync(
        string? traceId,
        string? correlationId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        // Validate inputs.
        if (string.IsNullOrWhiteSpace(traceId) && string.IsNullOrWhiteSpace(correlationId))
            throw new ArgumentException("At least one of traceId or correlationId must be non-empty");
        if (pageNumber < 1)
            throw new ArgumentException("pageNumber must be >= 1", nameof(pageNumber));
        if (pageSize < MinPageSize || pageSize > MaxPageSize)
            throw new ArgumentException($"pageSize must be between {MinPageSize} and {MaxPageSize}", nameof(pageSize));

        // Normalize empty strings to null.
        traceId = string.IsNullOrWhiteSpace(traceId) ? null : traceId;
        correlationId = string.IsNullOrWhiteSpace(correlationId) ? null : correlationId;

        // Delegate to business layer.
        return await _auditReadBusiness.GetAuditByTraceOrCorrelationAsync(
            traceId,
            correlationId,
            pageNumber,
            pageSize,
            cancellationToken);
    }
}

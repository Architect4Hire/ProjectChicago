using Microsoft.EntityFrameworkCore;
using ProjectChicago.Audit.Core.Models;
using ProjectChicago.Audit.Core.Persistence;

namespace ProjectChicago.Audit.Core.Repositories;

/// <summary>
/// Implementation of audit entry read queries (AUDIT-001..008, AUDIT-007, PERF-001..004).
/// Provides append-only query access to audit trail indexed by entity and by trace/correlation.
/// Materializes results as AuditEntryResult DTOs, excluding RawEventPayload and sensitive data.
/// </summary>
public class AuditRepository : IAuditRepository
{
    private readonly AuditDbContext _context;

    public AuditRepository(AuditDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// Query audit entries by entity type and ID with pagination.
    /// See IAuditRepository.QueryByEntityAsync for full contract.
    /// </summary>
    public async Task<AuditListResult> QueryByEntityAsync(
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

        // Calculate skip and take for pagination.
        var skip = (pageNumber - 1) * pageSize;

        // Query: filter by entity type and ID, order by OccurredAtUtc descending, then paginate.
        var totalCount = await _context.AuditEntries
            .Where(a => a.EntityType == entityType && a.EntityId == entityId)
            .CountAsync(cancellationToken);

        var items = await _context.AuditEntries
            .Where(a => a.EntityType == entityType && a.EntityId == entityId)
            .OrderByDescending(a => a.OccurredAtUtc)
            .Skip(skip)
            .Take(pageSize)
            .Select(a => MapToResult(a))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return new AuditListResult
        {
            Items = items,
            TotalCount = totalCount,
        };
    }

    /// <summary>
    /// Query audit entries by trace ID or correlation ID with pagination.
    /// See IAuditRepository.QueryByTraceOrCorrelationIdAsync for full contract.
    /// </summary>
    public async Task<AuditListResult> QueryByTraceOrCorrelationIdAsync(
        string? traceId,
        string? correlationId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(traceId) && string.IsNullOrWhiteSpace(correlationId))
            throw new ArgumentException("At least one of traceId or correlationId must be non-empty", nameof(traceId));
        if (pageNumber < 1)
            throw new ArgumentException("pageNumber must be >= 1", nameof(pageNumber));
        if (pageSize < 1)
            throw new ArgumentException("pageSize must be >= 1", nameof(pageSize));

        // Normalize empty/whitespace to null for consistent filtering.
        traceId = string.IsNullOrWhiteSpace(traceId) ? null : traceId;
        correlationId = string.IsNullOrWhiteSpace(correlationId) ? null : correlationId;

        // Calculate skip and take for pagination.
        var skip = (pageNumber - 1) * pageSize;

        // Build the filter predicate: TraceId matches (if provided) OR CorrelationId matches (if provided).
        IQueryable<AuditEntry> query = _context.AuditEntries;

        if (traceId != null && correlationId != null)
        {
            // Both provided: match either
            query = query.Where(a => a.TraceId == traceId || a.CorrelationId == correlationId);
        }
        else if (traceId != null)
        {
            // Only trace ID provided
            query = query.Where(a => a.TraceId == traceId);
        }
        else
        {
            // Only correlation ID provided (correlationId is not null due to the initial check)
            query = query.Where(a => a.CorrelationId == correlationId);
        }

        // Count total matches before pagination.
        var totalCount = await query.CountAsync(cancellationToken);

        // Order, paginate, and materialize.
        var items = await query
            .OrderByDescending(a => a.OccurredAtUtc)
            .Skip(skip)
            .Take(pageSize)
            .Select(a => MapToResult(a))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return new AuditListResult
        {
            Items = items,
            TotalCount = totalCount,
        };
    }

    /// <summary>
    /// Map AuditEntry entity to AuditEntryResult DTO.
    /// Excludes RawEventPayload (forensics only, not for normal queries).
    /// Used in LINQ Select expressions for efficient projection at the database layer.
    /// </summary>
    private static AuditEntryResult MapToResult(AuditEntry entry) =>
        new()
        {
            AuditEntryId = entry.AuditEntryId,
            EntityType = entry.EntityType,
            EntityId = entry.EntityId,
            Action = entry.Action,
            ActionCategory = entry.ActionCategory,
            ActorUserId = entry.ActorUserId,
            ActorType = entry.ActorType,
            ActorDisplayName = entry.ActorDisplayName,
            SourceService = entry.SourceService,
            OccurredAtUtc = entry.OccurredAtUtc,
            AuditedAtUtc = entry.AuditedAtUtc,
            TraceId = entry.TraceId,
            CorrelationId = entry.CorrelationId,
            CausationId = entry.CausationId,
            ChangedFields = entry.ChangedFields,
            PreviousValues = entry.PreviousValues,
            NewValues = entry.NewValues,
            SummaryDescription = entry.SummaryDescription,
            // Intentionally excluded: RawEventPayload
        };
}

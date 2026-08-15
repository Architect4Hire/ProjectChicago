namespace ProjectChicago.Audit.Core.Models;

/// <summary>
/// Repository output for audit entry list queries with pagination (AUDIT-001..008, PERF-003/004).
/// TotalCount represents the complete filtered result set; the caller computes pagination totals.
/// </summary>
public sealed record AuditListResult
{
    /// <summary>
    /// Page of audit entries matching the query filter, ordered by OccurredAtUtc descending.
    /// </summary>
    public required IReadOnlyList<AuditEntryResult> Items { get; init; }

    /// <summary>
    /// Total count of all audit entries matching the query filter (across all pages).
    /// Used to calculate total pages and pagination state.
    /// </summary>
    public required int TotalCount { get; init; }
}

namespace ProjectChicago.Audit.Core.Contracts;

/// <summary>
/// Public HTTP coordinates for audit entry queries (AUDIT-001..008, ACTIVITY-001..003, SEC-012).
/// Read-only endpoints restricted to privileged roles (Admin/Manager/Support).
///
/// -- GET api/audit/entries-by-entity --
///   Method + route:  GET api/audit/entries-by-entity
///   Request:         Query parameters: entityType (string), entityId (Guid), pageNumber (int), pageSize (int)
///   Success:         200 OK, AuditListResult body { Items: AuditEntryResult[], TotalCount: int }
///   Validation:      400 ValidationProblemDetails for missing/invalid entityType/entityId/pageNumber/pageSize.
///   Unauthenticated: 401 ProblemDetails (authentication required per SEC-012).
///   Unauthorized:    403 ProblemDetails (caller lacks Audit.Read policy).
///   Unexpected:      500 ProblemDetails.
///   Pagination:      Server-side (DefaultPage/DefaultPageSize applied, MaxPageSize bounded).
///
/// -- GET api/audit/entries-by-trace --
///   Method + route:  GET api/audit/entries-by-trace
///   Request:         Query parameters: traceId (string?), correlationId (string?), pageNumber (int), pageSize (int)
///   Success:         200 OK, AuditListResult body { Items: AuditEntryResult[], TotalCount: int }
///   Validation:      400 ValidationProblemDetails for invalid pageNumber/pageSize or neither traceId nor
///                     correlationId provided (at least one required).
///   Unauthenticated: 401 ProblemDetails (authentication required per SEC-012).
///   Unauthorized:    403 ProblemDetails (caller lacks Audit.Read policy).
///   Unexpected:      500 ProblemDetails.
///   Pagination:      Server-side (DefaultPage/DefaultPageSize applied, MaxPageSize bounded).
///
/// Policy registration/enforcement is composition-root work (Program.cs), out of scope for this contract.
/// </summary>
public static class AuditApiContract
{
    /// <summary>
    /// Base route for audit entry queries: "api/audit"
    /// </summary>
    public const string Route = "api/audit";

    /// <summary>
    /// Operation ID for GetEntriesByEntity endpoint (AUDIT-001..008, ACTIVITY-002).
    /// Stable identifier for OpenAPI documentation.
    /// </summary>
    public const string GetEntriesByEntityOperationId = "Audit_GetEntriesByEntity";

    /// <summary>
    /// Relative route suffix for GetEntriesByEntity: "entries-by-entity"
    /// Full path: GET /api/audit/entries-by-entity
    /// </summary>
    public const string EntriesByEntityRouteSuffix = "entries-by-entity";

    /// <summary>
    /// Operation ID for GetEntriesByTrace endpoint (AUDIT-007, ADR-0021).
    /// Stable identifier for OpenAPI documentation.
    /// </summary>
    public const string GetEntriesByTraceOperationId = "Audit_GetEntriesByTrace";

    /// <summary>
    /// Relative route suffix for GetEntriesByTrace: "entries-by-trace"
    /// Full path: GET /api/audit/entries-by-trace
    /// </summary>
    public const string EntriesByTraceRouteSuffix = "entries-by-trace";

    /// <summary>
    /// Read policy for audit entry queries (SEC-012, SEC-013).
    /// Restricted to privileged roles (Administrator, Manager, Support staff).
    /// All audit endpoints require this policy.
    /// </summary>
    public const string RequiredReadAuthorizationPolicy = "Audit.Read";

    /// <summary>
    /// Default page number for paginated queries (AUDIT-001..008, PERF-003/004).
    /// 1-based indexing.
    /// </summary>
    public const int DefaultPage = 1;

    /// <summary>
    /// Default page size for paginated queries when pageSize is omitted (AUDIT-001..008, PERF-003/004).
    /// Balances data transfer and query cost.
    /// </summary>
    public const int DefaultPageSize = 25;

    /// <summary>
    /// Maximum page size to prevent unbounded result sets (AUDIT-001..008, PERF-003/004).
    /// Callers requesting pageSize > MaxPageSize will receive a 400 ValidationProblemDetails.
    /// </summary>
    public const int MaxPageSize = 1000;
}

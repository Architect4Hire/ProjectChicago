using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectChicago.Audit.Core.Contracts;
using ProjectChicago.Audit.Core.Facades;
using ProjectChicago.Audit.Core.Models;

namespace ProjectChicago.Audit.Controllers;

/// <summary>
/// Read-only audit entry query endpoints (AUDIT-001..008, ACTIVITY-001..003, SEC-012).
/// Transport-only: binds query parameters to request models, applies coarse authentication check,
/// calls AuditReadFacade, and maps results/exceptions to HTTP responses (onion-boundaries.md step 3).
/// Fine-grained authorization (role-based policy) is enforced by [Authorize(Policy = ...)] and
/// ASP.NET Core middleware; business-level authorization remains in Facade/Business.
/// No field-by-field request/response mapping, no repository/DbContext injection, no direct Service Bus access.
/// </summary>
[ApiController]
[Route(AuditApiContract.Route)]
public sealed class AuditEntriesController : ControllerBase
{
    private readonly IAuditReadFacade _auditReadFacade;

    public AuditEntriesController(IAuditReadFacade auditReadFacade)
    {
        _auditReadFacade = auditReadFacade ?? throw new ArgumentNullException(nameof(auditReadFacade));
    }

    /// <summary>
    /// Retrieve paginated audit entries for a specific entity (Client, Project, Task, etc.).
    /// (AUDIT-001..008, ACTIVITY-002).
    ///
    /// Coarse authentication check (is any actor authenticated at all) stays here as plain
    /// ClaimsPrincipal inspection, distinct from the narrower "does this actor hold Audit.Read"
    /// policy check applied by [Authorize(Policy = ...)]. The policy check is enforcement by
    /// ASP.NET Core middleware; this controller-level check mirrors the pattern in Clients/Projects/Tasks.
    /// </summary>
    /// <param name="entityType">Required, non-empty entity type identifier (e.g., "Client", "Project", "Task").</param>
    /// <param name="entityId">Required, non-empty GUID of the entity whose audit trail is requested.</param>
    /// <param name="pageNumber">1-based page number; defaults to 1 if omitted. Validated by [ApiController] model binding.</param>
    /// <param name="pageSize">Entries per page; defaults to AuditApiContract.DefaultPageSize if omitted,
    /// clamped to AuditApiContract.MaxPageSize if too large. Validated by [ApiController] model binding.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>
    /// 200 OK with AuditListResult body (Items: AuditEntryResult[], TotalCount: int) ordered by OccurredAtUtc descending.
    /// 400 Bad Request if entityType/entityId/pageNumber/pageSize are invalid.
    /// 401 Unauthorized if the caller is not authenticated.
    /// 403 Forbidden if the caller lacks the Audit.Read policy/role.
    /// 500 Internal Server Error on unexpected failure.
    /// </returns>
    [HttpGet(AuditApiContract.EntriesByEntityRouteSuffix, Name = AuditApiContract.GetEntriesByEntityOperationId)]
    [Authorize(Policy = AuditApiContract.RequiredReadAuthorizationPolicy)]
    [ProducesResponseType(typeof(AuditListResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<AuditListResult>> GetEntriesByEntity(
        [FromQuery] string entityType,
        [FromQuery] Guid entityId,
        [FromQuery] int pageNumber = AuditApiContract.DefaultPage,
        [FromQuery] int pageSize = AuditApiContract.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        if (User.Identity is not { IsAuthenticated: true })
        {
            return Unauthorized();
        }

        var result = await _auditReadFacade.GetAuditByEntityAsync(
            entityType,
            entityId,
            pageNumber,
            pageSize,
            cancellationToken)
            .ConfigureAwait(false);

        return Ok(result);
    }

    /// <summary>
    /// Retrieve paginated audit entries linked to a distributed trace context or correlation ID.
    /// (AUDIT-007, ADR-0021, ACTIVITY-001).
    ///
    /// Supports operational traceability by finding all audit entries across services linked to a single
    /// request via its W3C trace ID or cross-service correlation ID. Enables troubleshooting and
    /// reconstructing the complete flow from browser request through all services and asynchronous work.
    ///
    /// Coarse authentication check (is any actor authenticated at all) stays here as plain
    /// ClaimsPrincipal inspection; the narrower policy authorization is enforced by [Authorize(Policy = ...)].
    /// </summary>
    /// <param name="traceId">Optional, W3C trace context identifier. Must be non-empty if provided.</param>
    /// <param name="correlationId">Optional, cross-service request correlation identifier. Must be non-empty if provided.</param>
    /// <param name="pageNumber">1-based page number; defaults to 1 if omitted. Validated by [ApiController] model binding.</param>
    /// <param name="pageSize">Entries per page; defaults to AuditApiContract.DefaultPageSize if omitted,
    /// clamped to AuditApiContract.MaxPageSize if too large. Validated by [ApiController] model binding.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>
    /// 200 OK with AuditListResult body (Items: AuditEntryResult[], TotalCount: int) ordered by OccurredAtUtc descending.
    /// 400 Bad Request if neither traceId nor correlationId is provided, or if pageNumber/pageSize are invalid.
    /// 401 Unauthorized if the caller is not authenticated.
    /// 403 Forbidden if the caller lacks the Audit.Read policy/role.
    /// 500 Internal Server Error on unexpected failure.
    /// </returns>
    [HttpGet(AuditApiContract.EntriesByTraceRouteSuffix, Name = AuditApiContract.GetEntriesByTraceOperationId)]
    [Authorize(Policy = AuditApiContract.RequiredReadAuthorizationPolicy)]
    [ProducesResponseType(typeof(AuditListResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<AuditListResult>> GetEntriesByTrace(
        [FromQuery] string? traceId,
        [FromQuery] string? correlationId,
        [FromQuery] int pageNumber = AuditApiContract.DefaultPage,
        [FromQuery] int pageSize = AuditApiContract.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        if (User.Identity is not { IsAuthenticated: true })
        {
            return Unauthorized();
        }

        var result = await _auditReadFacade.GetAuditByTraceOrCorrelationAsync(
            traceId,
            correlationId,
            pageNumber,
            pageSize,
            cancellationToken)
            .ConfigureAwait(false);

        return Ok(result);
    }
}

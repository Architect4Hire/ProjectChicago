using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectChicago.Audit.Core.Contracts;
using ProjectChicago.Audit.Core.Facades;
using ProjectChicago.Audit.Core.Models;
using ProjectChicago.ServiceDefaults.Filters;

namespace ProjectChicago.Audit.Controllers;

/// <summary>
/// Read-only audit entry query endpoints (AUDIT-001..008, ACTIVITY-001..003, SEC-012).
/// Transport-only: binds query parameters to request models, calls AuditReadFacade, and maps results/exceptions to HTTP responses.
/// Fine-grained authorization (role-based policy) is enforced by [Authorize(Policy = ...)] and ASP.NET Core middleware;
/// business-level authorization remains in Facade/Business.
/// No field-by-field request/response mapping, no repository/DbContext injection, no direct Service Bus access.
/// </summary>
[ApiController]
[Route(AuditApiContract.Route)]
[RequireAuthentication]
public sealed class AuditEntriesController : ControllerBase
{
    private readonly IAuditReadFacade _auditReadFacade;

    public AuditEntriesController(IAuditReadFacade auditReadFacade)
    {
        _auditReadFacade = auditReadFacade ?? throw new ArgumentNullException(nameof(auditReadFacade));
    }

    /// <summary>
    /// Retrieve paginated audit entries for a specific entity (Client, Project, Task, etc.).
    /// Requires Audit.Read authorization (AUDIT-001..008, ACTIVITY-002).
    /// </summary>
    /// <param name="entityType">Required, non-empty entity type identifier (e.g., "Client", "Project", "Task")</param>
    /// <param name="entityId">Required, non-empty GUID of the entity whose audit trail is requested</param>
    /// <param name="pageNumber">1-based page number; defaults to 1 if omitted. Validated by model binding</param>
    /// <param name="pageSize">Entries per page; defaults to AuditApiContract.DefaultPageSize if omitted,
    /// clamped to AuditApiContract.MaxPageSize if too large. Validated by model binding</param>
    /// <param name="cancellationToken">Cancellation token for async operations</param>
    /// <response code="200">Paginated audit entries ordered by OccurredAtUtc descending</response>
    /// <response code="400">Validation error (invalid entityType, entityId, pageNumber, or pageSize)</response>
    /// <response code="401">Not authenticated</response>
    /// <response code="403">Not authorized (requires Audit.Read policy)</response>
    /// <response code="500">Internal server error</response>
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
    /// Requires Audit.Read authorization (AUDIT-007, ADR-0021, ACTIVITY-001).
    /// Supports operational traceability by finding all audit entries across services linked to a single request
    /// via its W3C trace ID or cross-service correlation ID. Enables troubleshooting and reconstructing the complete
    /// flow from browser request through all services and asynchronous work.
    /// </summary>
    /// <param name="traceId">Optional, W3C trace context identifier. Must be non-empty if provided</param>
    /// <param name="correlationId">Optional, cross-service request correlation identifier. Must be non-empty if provided</param>
    /// <param name="pageNumber">1-based page number; defaults to 1 if omitted. Validated by model binding</param>
    /// <param name="pageSize">Entries per page; defaults to AuditApiContract.DefaultPageSize if omitted,
    /// clamped to AuditApiContract.MaxPageSize if too large. Validated by model binding</param>
    /// <param name="cancellationToken">Cancellation token for async operations</param>
    /// <response code="200">Paginated audit entries ordered by OccurredAtUtc descending</response>
    /// <response code="400">Validation error (neither traceId nor correlationId provided, or invalid pageNumber/pageSize)</response>
    /// <response code="401">Not authenticated</response>
    /// <response code="403">Not authorized (requires Audit.Read policy)</response>
    /// <response code="500">Internal server error</response>
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

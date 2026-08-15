using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectChicago.Crm.Contracts.Clients;
using ProjectChicago.Crm.Contracts.Common;
using ProjectChicago.Crm.Core.Data;
using ProjectChicago.Crm.Core.Facades;
using ProjectChicago.ServiceDefaults.Correlation;
using ProjectChicago.ServiceDefaults.Filters;
using ProjectChicago.Shared.Errors;

namespace ProjectChicago.Crm.Controllers;

/// <summary>
/// CRM Clients resource endpoints (CLIENT-001..004, CLIENT-020..024, CLIENT-030..032, API-006/007, SEC-010..013).
/// Transport-only: binds requests, delegates to IClientFacade, maps results to HTTP/ProblemDetails.
/// </summary>
[ApiController]
[Route(ClientsApiContract.Route)]
[RequireAuthentication]
public sealed class ClientsController : ControllerBase
{
    private readonly IClientFacade _clientFacade;

    public ClientsController(IClientFacade clientFacade)
    {
        _clientFacade = clientFacade ?? throw new ArgumentNullException(nameof(clientFacade));
    }

    /// <summary>
    /// Create a new client. Requires Clients.Write authorization (SEC-010..013).
    /// </summary>
    /// <param name="request">Client creation data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <response code="201">Client created</response>
    /// <response code="400">Validation error</response>
    /// <response code="401">Not authenticated</response>
    /// <response code="403">Not authorized (requires Clients.Write)</response>
    [HttpPost(Name = ClientsApiContract.CreateOperationId)]
    [Authorize(Policy = ClientsApiContract.RequiredAuthorizationPolicy)]
    [ProducesResponseType(typeof(ClientServiceModel), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ClientServiceModel>> Create(
        [FromBody] CreateClientViewModel request,
        CancellationToken cancellationToken)
    {
        var response = await _clientFacade.CreateAsync(request, cancellationToken).ConfigureAwait(false);

        return Created(new Uri($"{ClientsApiContract.Route}/{response.Id}", UriKind.Relative), response);
    }

    /// <summary>
    /// List clients with pagination and filtering. Requires Clients.Read authorization (SEC-010..013).
    /// Validation errors (page size, sort direction, etc.) return 400 before reaching Facade.
    /// </summary>
    /// <param name="request">Pagination and filter criteria</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <response code="200">Clients retrieved</response>
    /// <response code="400">Validation error</response>
    /// <response code="401">Not authenticated</response>
    /// <response code="403">Not authorized (requires Clients.Read)</response>
    [HttpGet(Name = ClientsApiContract.ListOperationId)]
    [Authorize(Policy = ClientsApiContract.RequiredReadAuthorizationPolicy)]
    [ProducesResponseType(typeof(PagedResponse<ClientServiceModel>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResponse<ClientServiceModel>>> List(
        [FromQuery] ListClientsRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _clientFacade.ListAsync(request, cancellationToken).ConfigureAwait(false);

        return Ok(response);
    }

    /// <summary>
    /// Get client detail by ID. Requires Clients.Read authorization (SEC-010..013).
    /// Returns 404 if client not found.
    /// </summary>
    /// <param name="clientId">Client ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <response code="200">Client retrieved</response>
    /// <response code="401">Not authenticated</response>
    /// <response code="403">Not authorized (requires Clients.Read)</response>
    /// <response code="404">Client not found</response>
    [HttpGet("{clientId:guid}", Name = ClientsApiContract.GetDetailOperationId)]
    [Authorize(Policy = ClientsApiContract.RequiredReadAuthorizationPolicy)]
    [ProducesResponseType(typeof(ClientDetailServiceModel), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClientDetailServiceModel>> GetDetail(
        Guid clientId,
        CancellationToken cancellationToken)
    {
        var response = await _clientFacade.GetDetailAsync(clientId, cancellationToken).ConfigureAwait(false);

        return response is null ? NotFound() : Ok(response);
    }

    /// <summary>
    /// Change client lifecycle status. Requires Clients.Write authorization (SEC-010..013).
    /// Returns 404 if client not found; 400 if transition invalid; 409 if concurrency conflict.
    /// </summary>
    /// <param name="clientId">Client ID</param>
    /// <param name="request">Lifecycle status change with concurrency token</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <response code="200">Status changed</response>
    /// <response code="400">Validation error or invalid state transition</response>
    /// <response code="401">Not authenticated</response>
    /// <response code="403">Not authorized (requires Clients.Write)</response>
    /// <response code="404">Client not found</response>
    /// <response code="409">Concurrency conflict (expected version mismatch)</response>
    [HttpPatch(ClientsApiContract.LifecycleStatusRouteSuffix, Name = ClientsApiContract.ChangeLifecycleStatusOperationId)]
    [Authorize(Policy = ClientsApiContract.RequiredAuthorizationPolicy)]
    [ProducesResponseType(typeof(ClientServiceModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ClientServiceModel>> ChangeLifecycleStatus(
        Guid clientId,
        [FromBody] ChangeClientLifecycleStatusViewModel request,
        CancellationToken cancellationToken)
    {

        try
        {
            var response = await _clientFacade.ChangeLifecycleStatusAsync(clientId, request, cancellationToken)
                .ConfigureAwait(false);

            return response is null ? NotFound() : Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            var requestContext = HttpRequestContextFactory.Create(HttpContext);
            var problem = ApiProblemDetailsFactory.Validation(
                requestContext,
                fieldErrors: new Dictionary<string, string[]>
                {
                    [nameof(ChangeClientLifecycleStatusViewModel.NewStatus)] = [ex.Message],
                });

            return BadRequest(problem);
        }
        catch (ClientConcurrencyConflictException)
        {
            var requestContext = HttpRequestContextFactory.Create(HttpContext);
            return Conflict(ApiProblemDetailsFactory.ConcurrencyConflict(requestContext));
        }
    }

    // Same 401-vs-403 split as the other actions. 404 is decided here the same way GetDetail
    // decides it: IClientFacade.ArchiveAsync returns null when no Client with the requested Id
    // exists. One additional outcome is specific to this mutation and is not classified anywhere in
    // the shared ApiExceptionHandler (backend.md: that handler "must not grow its switch with
    // bespoke business exception types" - domain/data-specific translation belongs here, at the
    // boundary that owns this one use case):
    //  - InvalidOperationException: Business rejected the archive because the Client has active
    //    Projects (CLIENT-015). Mapped as a 409 conflict - the state prevents the requested
    //    operation, not a race with another request.
    //  - ClientConcurrencyConflictException: request.ExpectedConcurrencyToken did not match the
    //    Client's currently persisted version (DATA-008). Mapped as a 409 conflict.
    [HttpPost(ClientsApiContract.ArchiveRouteSuffix, Name = ClientsApiContract.ArchiveOperationId)]
    [Authorize(Policy = ClientsApiContract.RequiredAuthorizationPolicy)]
    [ProducesResponseType(typeof(ClientServiceModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ClientServiceModel>> Archive(
        Guid clientId,
        [FromBody] ArchiveClientViewModel request,
        CancellationToken cancellationToken)
    {
        if (User.Identity is not { IsAuthenticated: true })
        {
            return Unauthorized();
        }

        try
        {
            var response = await _clientFacade.ArchiveAsync(clientId, request, cancellationToken)
                .ConfigureAwait(false);

            return response is null ? NotFound() : Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            var requestContext = HttpRequestContextFactory.Create(HttpContext);
            return Conflict(ApiProblemDetailsFactory.ConcurrencyConflict(requestContext, ex.Message));
        }
        catch (ClientConcurrencyConflictException)
        {
            var requestContext = HttpRequestContextFactory.Create(HttpContext);
            return Conflict(ApiProblemDetailsFactory.ConcurrencyConflict(requestContext));
        }
    }

    // Same 401-vs-403 split as the other actions. 404 is decided here the same way GetDetail
    // decides it: IClientFacade.RestoreAsync returns null when no Client with the requested Id
    // exists. One additional outcome is specific to this mutation and is not classified anywhere in
    // the shared ApiExceptionHandler (backend.md: that handler "must not grow its switch with
    // bespoke business exception types" - domain/data-specific translation belongs here, at the
    // boundary that owns this one use case):
    //  - InvalidOperationException: Business rejected the restore because the Client is not
    //    currently Archived (CLIENT-014), or the RestoredStatus is invalid. Mapped as a 400 field
    //    error - the request itself is invalid given the Client's current state.
    //  - ClientConcurrencyConflictException: request.ExpectedConcurrencyToken did not match the
    //    Client's currently persisted version (DATA-008). Mapped as a 409 conflict.
    [HttpPost(ClientsApiContract.RestoreRouteSuffix, Name = ClientsApiContract.RestoreOperationId)]
    [Authorize(Policy = ClientsApiContract.RequiredAuthorizationPolicy)]
    [ProducesResponseType(typeof(ClientServiceModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ClientServiceModel>> Restore(
        Guid clientId,
        [FromBody] RestoreClientViewModel request,
        CancellationToken cancellationToken)
    {
        if (User.Identity is not { IsAuthenticated: true })
        {
            return Unauthorized();
        }

        try
        {
            var response = await _clientFacade.RestoreAsync(clientId, request, cancellationToken)
                .ConfigureAwait(false);

            return response is null ? NotFound() : Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            var requestContext = HttpRequestContextFactory.Create(HttpContext);
            var problem = ApiProblemDetailsFactory.Validation(
                requestContext,
                fieldErrors: new Dictionary<string, string[]>
                {
                    [nameof(RestoreClientViewModel.RestoredStatus)] = [ex.Message],
                });

            return BadRequest(problem);
        }
        catch (ClientConcurrencyConflictException)
        {
            var requestContext = HttpRequestContextFactory.Create(HttpContext);
            return Conflict(ApiProblemDetailsFactory.ConcurrencyConflict(requestContext));
        }
    }

    // Same 401-vs-403 split as the other actions. 404 is decided here the same way GetDetail
    // decides it: IClientFacade.UpdateAsync returns null when no Client with the requested Id
    // exists. One additional outcome is specific to this mutation and is not classified anywhere in
    // the shared ApiExceptionHandler (backend.md: that handler "must not grow its switch with
    // bespoke business exception types" - domain/data-specific translation belongs here, at the
    // boundary that owns this one use case):
    //  - ClientConcurrencyConflictException: request.ExpectedConcurrencyToken did not match the
    //    Client's currently persisted version (DATA-008). Mapped as a 409 conflict.
    [HttpPatch(ClientsApiContract.UpdateRouteSuffix, Name = ClientsApiContract.UpdateOperationId)]
    [Authorize(Policy = ClientsApiContract.RequiredAuthorizationPolicy)]
    [ProducesResponseType(typeof(ClientServiceModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ClientServiceModel>> Update(
        Guid clientId,
        [FromBody] UpdateClientViewModel request,
        CancellationToken cancellationToken)
    {
        if (User.Identity is not { IsAuthenticated: true })
        {
            return Unauthorized();
        }

        try
        {
            var response = await _clientFacade.UpdateAsync(clientId, request, cancellationToken)
                .ConfigureAwait(false);

            return response is null ? NotFound() : Ok(response);
        }
        catch (ClientConcurrencyConflictException)
        {
            var requestContext = HttpRequestContextFactory.Create(HttpContext);
            return Conflict(ApiProblemDetailsFactory.ConcurrencyConflict(requestContext));
        }
    }
}

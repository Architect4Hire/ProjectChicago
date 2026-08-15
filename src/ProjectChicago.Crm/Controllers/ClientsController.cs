using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectChicago.Crm.Contracts.Clients;
using ProjectChicago.Crm.Contracts.Common;
using ProjectChicago.Crm.Core.Data;
using ProjectChicago.Crm.Core.Facades;
using ProjectChicago.ServiceDefaults.Correlation;
using ProjectChicago.Shared.Errors;

namespace ProjectChicago.Crm.Controllers;

// POST /api/clients, GET /api/clients, GET /api/clients/{clientId} (CLIENT-001..004,
// CLIENT-020..024, CLIENT-030..032, API-001..007, SEC-010..013, ERROR-001..005). Transport-only:
// binds the wire request, applies the coarse "is there an authenticated actor at all" check
// documented by ClientsApiContract's 401 case, calls the single IClientFacade use case, and maps its
// typed result/exception to the standard HTTP/ProblemDetails shape (onion-boundaries.md; add-endpoint
// skill step 3). No field-by-field request/response mapping lives here - IClientFacade accepts and
// returns the wire contract types directly, and ClientContractMappingExtensions (Business) owns the
// translation to/from Business models. Fine-grained SEC-012/013 policy authorization ("Clients.Write")
// and CLIENT-002/004 business rules live in Facade/Business - this controller injects no
// Business/Data/Repository/DbContext and never publishes directly (RESTRICTION).
[ApiController]
[Route(ClientsApiContract.Route)]
public sealed class ClientsController : ControllerBase
{
    private readonly IClientFacade _clientFacade;

    public ClientsController(IClientFacade clientFacade)
    {
        _clientFacade = clientFacade ?? throw new ArgumentNullException(nameof(clientFacade));
    }

    // Unauthenticated (401) vs unauthorized (403) are deliberately distinct per ClientsApiContract:
    // this coarse check (is there any authenticated actor at all) stays in the controller as plain
    // ASP.NET Core ClaimsPrincipal inspection - not a call into IClientFacade/IClientAuthorization -
    // so it never depends on the still-open ADR-0018 authentication-transport decision. The narrower
    // "does this actor hold Clients.Write" policy check happens in Facade/IClientAuthorization and
    // surfaces here only as an UnauthorizedAccessException the already-registered ApiExceptionHandler
    // classifies into 403.
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
        if (User.Identity is not { IsAuthenticated: true })
        {
            return Unauthorized();
        }

        var response = await _clientFacade.CreateAsync(request, cancellationToken).ConfigureAwait(false);

        return Created(new Uri($"{ClientsApiContract.Route}/{response.Id}", UriKind.Relative), response);
    }

    // Same 401-vs-403 split as Create: the coarse "is there any authenticated actor at all" check
    // stays here as plain ClaimsPrincipal inspection, while the narrower Clients.Read policy check
    // happens in Facade/IClientAuthorization and surfaces here only as an
    // UnauthorizedAccessException the registered ApiExceptionHandler classifies into 403.
    // [ApiController]'s automatic model-state validation covers CLIENT-024's bounded-page-size
    // requirement and the SortBy/SortDirection/LifecycleStatus [EnumDataType] checks on
    // ListClientsRequest before this action body ever runs, so an invalid query never reaches the
    // Facade (SEC-022).
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
        if (User.Identity is not { IsAuthenticated: true })
        {
            return Unauthorized();
        }

        var response = await _clientFacade.ListAsync(request, cancellationToken).ConfigureAwait(false);

        return Ok(response);
    }

    // Same 401-vs-403 split as Create/List: the coarse "is there any authenticated actor at all"
    // check stays here as plain ClaimsPrincipal inspection, while the narrower Clients.Read policy
    // check happens in Facade/IClientAuthorization and surfaces here only as an
    // UnauthorizedAccessException the registered ApiExceptionHandler classifies into 403. 404 is
    // decided here, not in Facade/Business: IClientFacade.GetDetailAsync returns null when no
    // Client with the requested Id exists, and this action is the only place that null maps to a
    // 404 ProblemDetails response (CLIENT-030..032).
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
        if (User.Identity is not { IsAuthenticated: true })
        {
            return Unauthorized();
        }

        var response = await _clientFacade.GetDetailAsync(clientId, cancellationToken).ConfigureAwait(false);

        return response is null ? NotFound() : Ok(response);
    }

    // Same 401-vs-403 split as the other actions. 404 is decided here the same way GetDetail
    // decides it: IClientFacade.ChangeLifecycleStatusAsync returns null when no Client with the
    // requested Id exists. Two additional outcomes are specific to this mutation and are not
    // classified anywhere in the shared ApiExceptionHandler (backend.md: that handler "must not
    // grow its switch with bespoke business exception types" - domain/data-specific translation
    // belongs here, at the boundary that owns this one use case):
    //  - InvalidOperationException: Business rejected the requested transition
    //    (CLIENT-010..015/ClientLifecycleTransitionRules). Mapped as a 400 field error on
    //    NewStatus - the request itself is invalid given the Client's current state, not a race
    //    with another request.
    //  - ClientConcurrencyConflictException: request.ExpectedConcurrencyToken did not match the
    //    Client's currently persisted version (DATA-008). Mapped as a 409 conflict.
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
        if (User.Identity is not { IsAuthenticated: true })
        {
            return Unauthorized();
        }

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

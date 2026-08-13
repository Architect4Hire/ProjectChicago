using System.ComponentModel.DataAnnotations;
using ProjectChicago.Crm.Contracts.Clients;
using ProjectChicago.Crm.Contracts.Common;
using ProjectChicago.Crm.Core.Business;
using ProjectChicago.Shared.Correlation;

namespace ProjectChicago.Crm.Core.Facades;

// IClientFacade implementation for Client creation and list/search (CLIENT-001..004,
// CLIENT-020..024, SEC-010..013; onion-boundaries.md, backend.md). Owns exactly: resolving the
// acting user/correlation context, the SEC-012/013 authorization check (Clients.Write for
// CreateAsync, Clients.Read for ListAsync), and CLIENT-002/CLIENT-020..024 contextual request
// validation, before delegating to IClientBusiness for CLIENT-004 duplicate-warning evaluation,
// persistence, filter translation/retrieval, and the wire<->domain mapping. No EF, cache,
// HttpContext, or Service Bus dependency - those belong to Data/Repository, the HTTP host, and the
// outbox relay respectively (RESTRICTION: Facade does not access Data/EF). This Facade never maps
// CreateClientViewModel/ClientServiceModel/ListClientsRequest fields itself - CreateAsync only
// resolves the actor/context/timestamp a caller must never supply itself and hands them, plus the
// untouched ViewModel, to IClientBusiness.CreateAsync; ListAsync hands the untouched request
// straight to IClientBusiness.ListAsync - both return their Business result straight back to the
// Controller.
public sealed class ClientFacade : IClientFacade
{
    private readonly IClientBusiness _clientBusiness;
    private readonly IClientAuthorization _authorization;
    private readonly ICurrentRequestContext _currentRequestContext;
    private readonly IClock _clock;

    public ClientFacade(
        IClientBusiness clientBusiness,
        IClientAuthorization authorization,
        ICurrentRequestContext currentRequestContext,
        IClock clock)
    {
        _clientBusiness = clientBusiness ?? throw new ArgumentNullException(nameof(clientBusiness));
        _authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
        _currentRequestContext = currentRequestContext ?? throw new ArgumentNullException(nameof(currentRequestContext));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<ClientServiceModel> CreateAsync(CreateClientViewModel request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var requestContext = _currentRequestContext.Current;

        // SEC-013: authorization is verified before any validation/business work changes or even
        // evaluates request data.
        var authorized = await _authorization.CanCreateAsync(requestContext.Actor, cancellationToken).ConfigureAwait(false);
        if (!authorized)
        {
            throw new UnauthorizedAccessException(
                "The current actor is not authorized to create a Client (Clients.Write).");
        }

        Validate(request);

        // CLIENT-004: Business decides/evaluates duplicate warnings and maps them onto the returned
        // ClientServiceModel. The Facade does not inspect or block on PossibleDuplicates, and does
        // not map the request/result itself - it only passes the ViewModel and resolved
        // actor/context/timestamp through unchanged, so a duplicate warning is surfaced to the
        // caller, never silently merged or dropped.
        return await _clientBusiness.CreateAsync(
            request, requestContext.Actor, requestContext, _clock.UtcNow, cancellationToken).ConfigureAwait(false);
    }

    public async Task<PagedResponse<ClientServiceModel>> ListAsync(
        ListClientsRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var requestContext = _currentRequestContext.Current;

        // SEC-012/013: authorization for the read policy (Clients.Read) is verified before any
        // validation/business work touches the request, mirroring CreateAsync's authorize-first
        // ordering so an unauthorized caller never learns which page/sort/filter values would have
        // failed validation.
        var authorized = await _authorization.CanListAsync(requestContext.Actor, cancellationToken).ConfigureAwait(false);
        if (!authorized)
        {
            throw new UnauthorizedAccessException(
                "The current actor is not authorized to list Clients (Clients.Read).");
        }

        Validate(request);

        // CLIENT-020..024: the Facade does not translate/map ListClientsRequest itself - it only
        // authorizes and validates before handing the untouched request to IClientBusiness.ListAsync
        // for filter translation, retrieval, and mapping into PagedResponse<ClientServiceModel>.
        return await _clientBusiness.ListAsync(request, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ClientDetailServiceModel?> GetDetailAsync(Guid clientId, CancellationToken cancellationToken)
    {
        if (clientId == Guid.Empty)
        {
            throw new ValidationException("Client Id must not be empty.");
        }

        var requestContext = _currentRequestContext.Current;

        // SEC-013: authorization is verified before any Business/Data work runs, mirroring
        // CreateAsync/ListAsync's authorize-first ordering.
        var authorized = await _authorization.CanGetDetailAsync(requestContext.Actor, cancellationToken).ConfigureAwait(false);
        if (!authorized)
        {
            throw new UnauthorizedAccessException(
                "The current actor is not authorized to view Client detail (Clients.Read).");
        }

        return await _clientBusiness.GetDetailAsync(clientId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ClientServiceModel?> ChangeLifecycleStatusAsync(
        Guid clientId, ChangeClientLifecycleStatusViewModel request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (clientId == Guid.Empty)
        {
            throw new ValidationException("Client Id must not be empty.");
        }

        var requestContext = _currentRequestContext.Current;

        // SEC-013: authorization is verified before any validation/business work runs, mirroring
        // CreateAsync/ListAsync/GetDetailAsync's authorize-first ordering.
        var authorized = await _authorization.CanChangeLifecycleStatusAsync(requestContext.Actor, cancellationToken).ConfigureAwait(false);
        if (!authorized)
        {
            throw new UnauthorizedAccessException(
                "The current actor is not authorized to change a Client's lifecycle status (Clients.Write).");
        }

        Validate(request);

        // CLIENT-010..015/DATA-008: the Facade does not evaluate the transition-rule or
        // concurrency-token check itself - it only authorizes and validates transport shape before
        // handing clientId, the untouched request, and the resolved actor/context/timestamp to
        // IClientBusiness.ChangeLifecycleStatusAsync.
        return await _clientBusiness.ChangeLifecycleStatusAsync(
            clientId,
            request.NewStatus,
            request.ExpectedConcurrencyToken,
            requestContext.Actor,
            requestContext,
            _clock.UtcNow,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<ClientServiceModel?> ArchiveAsync(
        Guid clientId, ArchiveClientViewModel request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (clientId == Guid.Empty)
        {
            throw new ValidationException("Client Id must not be empty.");
        }

        var requestContext = _currentRequestContext.Current;

        // SEC-013: authorization is verified before any validation/business work runs, mirroring
        // ChangeLifecycleStatusAsync's authorize-first ordering.
        var authorized = await _authorization.CanArchiveAsync(requestContext.Actor, cancellationToken).ConfigureAwait(false);
        if (!authorized)
        {
            throw new UnauthorizedAccessException(
                "The current actor is not authorized to archive a Client (Clients.Write).");
        }

        Validate(request);

        // CLIENT-013..015/DATA-008: the Facade does not evaluate the active-Projects check or
        // concurrency-token check itself - it only authorizes and validates transport shape before
        // handing clientId, the untouched request, and the resolved actor/context/timestamp to
        // IClientBusiness.ArchiveAsync.
        return await _clientBusiness.ArchiveAsync(
            clientId,
            request.ExpectedConcurrencyToken,
            requestContext.Actor,
            requestContext,
            _clock.UtcNow,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<ClientServiceModel?> RestoreAsync(
        Guid clientId, RestoreClientViewModel request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (clientId == Guid.Empty)
        {
            throw new ValidationException("Client Id must not be empty.");
        }

        var requestContext = _currentRequestContext.Current;

        // SEC-013: authorization is verified before any validation/business work runs, mirroring
        // ArchiveAsync's authorize-first ordering.
        var authorized = await _authorization.CanRestoreAsync(requestContext.Actor, cancellationToken).ConfigureAwait(false);
        if (!authorized)
        {
            throw new UnauthorizedAccessException(
                "The current actor is not authorized to restore a Client (Clients.Write).");
        }

        Validate(request);

        // CLIENT-013..014/DATA-008: the Facade does not evaluate the archive-status check or
        // concurrency-token check itself - it only authorizes and validates transport shape before
        // handing clientId, the untouched request, and the resolved actor/context/timestamp to
        // IClientBusiness.RestoreAsync.
        return await _clientBusiness.RestoreAsync(
            clientId,
            request.RestoredStatus,
            request.ExpectedConcurrencyToken,
            requestContext.Actor,
            requestContext,
            _clock.UtcNow,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<ClientServiceModel?> UpdateAsync(
        Guid clientId, UpdateClientViewModel request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (clientId == Guid.Empty)
        {
            throw new ValidationException("Client Id must not be empty.");
        }

        var requestContext = _currentRequestContext.Current;

        // SEC-013: authorization is verified before any validation/business work runs, mirroring
        // ChangeLifecycleStatusAsync's authorize-first ordering.
        var authorized = await _authorization.CanUpdateAsync(requestContext.Actor, cancellationToken).ConfigureAwait(false);
        if (!authorized)
        {
            throw new UnauthorizedAccessException(
                "The current actor is not authorized to update a Client (Clients.Write).");
        }

        Validate(request);

        // CLIENT-002/DATA-008: the Facade does not evaluate the concurrency-token check itself -
        // it only authorizes and validates transport shape before handing clientId, the untouched
        // request, and the resolved actor/context/timestamp to IClientBusiness.UpdateAsync.
        return await _clientBusiness.UpdateAsync(
            clientId,
            request,
            request.ExpectedConcurrencyToken,
            requestContext.Actor,
            requestContext,
            _clock.UtcNow,
            cancellationToken).ConfigureAwait(false);
    }

    private static void Validate(CreateClientViewModel request) => Validate((object)request);

    private static void Validate(ChangeClientLifecycleStatusViewModel request) => Validate((object)request);

    private static void Validate(ArchiveClientViewModel request) => Validate((object)request);

    private static void Validate(RestoreClientViewModel request) => Validate((object)request);

    private static void Validate(UpdateClientViewModel request) => Validate((object)request);

    private static void Validate(ListClientsRequest request) => Validate((object)request);

    private static void Validate(object request)
    {
        var validationContext = new ValidationContext(request);
        var results = new List<ValidationResult>();

        if (!Validator.TryValidateObject(request, validationContext, results, validateAllProperties: true))
        {
            var message = string.Join("; ", results.Select(result => result.ErrorMessage));
            throw new ValidationException(
                string.IsNullOrWhiteSpace(message) ? "Client request failed validation." : message);
        }
    }
}

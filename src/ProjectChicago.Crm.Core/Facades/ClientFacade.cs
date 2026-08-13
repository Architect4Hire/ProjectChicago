using System.ComponentModel.DataAnnotations;
using ProjectChicago.Crm.Contracts.Clients;
using ProjectChicago.Crm.Core.Business;
using ProjectChicago.Shared.Correlation;

namespace ProjectChicago.Crm.Core.Facades;

// IClientFacade implementation for Client creation (CLIENT-001..004, SEC-010..013;
// onion-boundaries.md, backend.md). Owns exactly: resolving the acting user/correlation context,
// the SEC-012/013 authorization check, and CLIENT-002 contextual request validation, before
// delegating to IClientBusiness for CLIENT-004 duplicate-warning evaluation, persistence, and the
// wire<->domain mapping. No EF, cache, HttpContext, or Service Bus dependency - those belong to
// Data/Repository, the HTTP host, and the outbox relay respectively (RESTRICTION: Facade does not
// access Data/EF). This Facade never maps CreateClientViewModel/ClientServiceModel fields itself -
// it only resolves the actor/context/timestamp a caller must never supply itself and hands them,
// plus the untouched ViewModel, to IClientBusiness.CreateAsync, returning its ClientServiceModel
// straight back to the Controller.
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

    private static void Validate(CreateClientViewModel request)
    {
        var validationContext = new ValidationContext(request);
        var results = new List<ValidationResult>();

        if (!Validator.TryValidateObject(request, validationContext, results, validateAllProperties: true))
        {
            var message = string.Join("; ", results.Select(result => result.ErrorMessage));
            throw new ValidationException(
                string.IsNullOrWhiteSpace(message) ? "Client creation request failed validation." : message);
        }
    }
}

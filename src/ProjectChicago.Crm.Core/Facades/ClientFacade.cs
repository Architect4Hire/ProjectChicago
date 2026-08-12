using System.ComponentModel.DataAnnotations;
using ProjectChicago.Crm.Core.Business;
using ProjectChicago.Crm.Core.Models.ServiceModels;
using ProjectChicago.Shared.Correlation;

namespace ProjectChicago.Crm.Core.Facades;

// IClientFacade implementation for Client creation (CLIENT-001..004, SEC-010..013;
// onion-boundaries.md, backend.md). Owns exactly: resolving the acting user/correlation context,
// the SEC-012/013 authorization check, and CLIENT-002 contextual request validation, before
// delegating to IClientBusiness for CLIENT-004 duplicate-warning evaluation and persistence. No EF,
// cache, HttpContext, or Service Bus dependency - those belong to Data/Repository, the HTTP host,
// and the outbox relay respectively (RESTRICTION: Facade does not access Data/EF).
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

    public async Task<ClientCreationResult> CreateAsync(CreateClientRequest request, CancellationToken cancellationToken)
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

        var command = new CreateClientCommand
        {
            Name = request.Name,
            PrimaryContactName = request.PrimaryContactName,
            PrimaryEmail = request.PrimaryEmail,
            PrimaryPhone = request.PrimaryPhone,
            Website = request.Website,
            AddressLine = request.AddressLine,
            City = request.City,
            StateOrProvince = request.StateOrProvince,
            PostalCode = request.PostalCode,
            Country = request.Country,
            LifecycleStatus = request.LifecycleStatus,
            Description = request.Description,
            OwnerUserId = request.OwnerUserId,
            Actor = requestContext.Actor,
            RequestContext = requestContext,
            CreatedAtUtc = _clock.UtcNow,
        };

        // CLIENT-004: Business decides/evaluates duplicate warnings and returns them on the result.
        // The Facade does not inspect or block on PossibleDuplicates - it only passes the result
        // through unchanged, so a duplicate warning is surfaced to the caller, never silently
        // merged or dropped.
        return await _clientBusiness.CreateAsync(command, cancellationToken).ConfigureAwait(false);
    }

    private static void Validate(CreateClientRequest request)
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

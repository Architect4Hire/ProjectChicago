using ProjectChicago.Crm.Contracts.Clients;

namespace ProjectChicago.Crm.Core.Facades;

// Public application/use-case seam for Client creation (CLIENT-001..004, SEC-010..013;
// onion-boundaries.md: "Facades are the only application entry point callable by controllers").
// Accepts CreateClientViewModel and returns ClientServiceModel directly (rather than a separate
// Facade-only request/result shape) so ClientsController stays transport-only: it binds the
// request, calls this one method, and returns the ClientServiceModel straight into a 201 - no
// field-by-field mapping of its own. This Facade does not map either type itself - it resolves
// actor/context/timestamp and delegates the ViewModel<->domain<->ServiceModel translation entirely
// to IClientBusiness.CreateAsync (ClientContractMappingExtensions, in Business).
public interface IClientFacade
{
    // Verifies SEC-012/013 authorization, runs CLIENT-002 contextual validation on request, and
    // delegates to Business for CLIENT-004 duplicate-warning evaluation, model translation, and
    // persistence. Throws UnauthorizedAccessException when the resolved actor lacks the
    // Clients.Write policy, or System.ComponentModel.DataAnnotations.ValidationException when
    // request fails validation - both already classified by ApiExceptionHandler into the 403/400
    // ProblemDetails shape (ERROR-003).
    Task<ClientServiceModel> CreateAsync(CreateClientViewModel request, CancellationToken cancellationToken);
}

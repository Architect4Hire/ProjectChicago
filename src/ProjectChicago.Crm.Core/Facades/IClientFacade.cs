using ProjectChicago.Crm.Contracts.Clients;

namespace ProjectChicago.Crm.Core.Facades;

// Public application/use-case seam for Client creation (CLIENT-001..004, SEC-010..013;
// onion-boundaries.md: "Facades are the only application entry point callable by controllers").
// Accepts and returns the wire contract types directly (rather than a separate Facade-only
// request/result shape) so ClientsController stays transport-only: it binds the request, calls
// this one method, and maps the returned ClientResponse straight into a 201 - no field-by-field
// mapping of its own. CreateAsync itself delegates the wire<->Business translation to
// ClientContractMappingExtensions, which lives in Business alongside the rules it feeds.
public interface IClientFacade
{
    // Verifies SEC-012/013 authorization, runs CLIENT-002 contextual validation on request, and
    // delegates to Business for CLIENT-004 duplicate-warning evaluation, model translation, and
    // persistence. Throws UnauthorizedAccessException when the resolved actor lacks the
    // Clients.Write policy, or System.ComponentModel.DataAnnotations.ValidationException when
    // request fails validation - both already classified by ApiExceptionHandler into the 403/400
    // ProblemDetails shape (ERROR-003).
    Task<ClientResponse> CreateAsync(CreateClientRequest request, CancellationToken cancellationToken);
}

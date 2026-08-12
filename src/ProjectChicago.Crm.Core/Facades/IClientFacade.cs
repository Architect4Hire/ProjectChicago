using ProjectChicago.Crm.Core.Models.ServiceModels;

namespace ProjectChicago.Crm.Core.Facades;

// Public application/use-case seam for Client creation (CLIENT-001..004, SEC-010..013;
// onion-boundaries.md: "Facades are the only application entry point callable by controllers").
// Not yet called by a controller - this microstep implements the Facade only (CLIENT create
// scope).
public interface IClientFacade
{
    // Verifies SEC-012/013 authorization, runs CLIENT-002 contextual validation on request, and
    // delegates to Business for CLIENT-004 duplicate-warning evaluation, model translation, and
    // persistence. Throws UnauthorizedAccessException when the resolved actor lacks the
    // Clients.Write policy, or System.ComponentModel.DataAnnotations.ValidationException when
    // request fails validation - both already classified by ApiExceptionHandler into the 403/400
    // ProblemDetails shape (ERROR-003).
    Task<ClientCreationResult> CreateAsync(CreateClientRequest request, CancellationToken cancellationToken);
}

using ProjectChicago.Crm.Core.Models.ServiceModels;

namespace ProjectChicago.Crm.Core.Business;

// Business-layer Client creation seam (CLIENT-001..004, AUDIT-001..003; backend.md, onion-
// boundaries.md). Callable only by a Facade (not yet implemented) - this interface accepts an
// already-resolved command so Business owns state-transition rules and model translation only, with
// no HttpContext, EF, cache, or Service Bus dependency of its own.
public interface IClientBusiness
{
    // Normalizes business values, assigns identity and the initial lifecycle status, decides
    // CLIENT-004 duplicate warnings, builds the AUDIT-001..003 audit fact, and persists both the
    // Client and the audit fact through the single IClientData seam. Duplicate warnings never block
    // creation (CLIENT-004: "warn ... rather than silently merge").
    Task<ClientCreationResult> CreateAsync(CreateClientCommand command, CancellationToken cancellationToken);
}

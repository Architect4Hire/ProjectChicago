using ProjectChicago.Crm.Contracts.Clients;
using ProjectChicago.Shared.Correlation;

namespace ProjectChicago.Crm.Core.Business;

// Business-layer Client creation seam (CLIENT-001..004, AUDIT-001..003; backend.md, onion-
// boundaries.md). Accepts the wire CreateClientViewModel directly and returns the wire
// ClientServiceModel directly - Business owns the entire ViewModel<->domain<->ServiceModel
// translation (ClientContractMappingExtensions), so Facade only resolves/supplies the
// actor/context/timestamp a caller must never set itself and ClientsController does no mapping at
// all.
public interface IClientBusiness
{
    // Normalizes business values, assigns identity and the initial lifecycle status, decides
    // CLIENT-004 duplicate warnings, builds the AUDIT-001..003 audit fact, persists both the Client
    // and the audit fact through the single IClientData seam, and maps the result into a
    // ClientServiceModel. Duplicate warnings never block creation (CLIENT-004: "warn ... rather than
    // silently merge").
    Task<ClientServiceModel> CreateAsync(
        CreateClientViewModel request,
        ActorContext actor,
        RequestContext requestContext,
        DateTime createdAtUtc,
        CancellationToken cancellationToken);
}

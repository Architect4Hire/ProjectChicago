using ProjectChicago.Shared.Correlation;

namespace ProjectChicago.Crm.Core.Facades;

// Mechanism-neutral record-level authorization seam for the Client use case (SEC-010..013;
// onion-boundaries.md: "Facades own ... record-level authorization"). ClientFacade depends on this
// abstraction rather than Microsoft.AspNetCore.Authorization/ClaimsPrincipal directly, so the same
// Facade stays callable from a Functions entry point later without an HttpContext dependency
// (mirrors ICurrentRequestContext's mechanism-neutral design). An HTTP host composition root wires
// the concrete adapter against ASP.NET Core Identity's authorization mechanisms (roles/claims/
// policies - SEC-011), evaluating ProjectChicago.Crm.Contracts.Clients.ClientsApiContract's
// "Clients.Write" policy for the resolved actor; that wiring is composition-root work, out of scope
// here.
public interface IClientAuthorization
{
    // Returns whether actor is authorized to create a Client (SEC-012/SEC-013: every mutation
    // verifies authorization before changing data). actor is never trusted from ordinary client
    // input - it is always the already-authenticated ActorContext resolved from
    // ICurrentRequestContext (security.md: "Resolve the actor through ICurrentUser").
    Task<bool> CanCreateAsync(ActorContext actor, CancellationToken cancellationToken);
}

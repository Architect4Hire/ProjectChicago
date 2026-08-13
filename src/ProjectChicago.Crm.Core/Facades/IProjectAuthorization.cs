using ProjectChicago.Shared.Correlation;

namespace ProjectChicago.Crm.Core.Facades;

// Mechanism-neutral record-level authorization seam for Project use cases (SEC-010..013,
// PROJECT-020..023; onion-boundaries.md: "Facades own ... record-level authorization"). ProjectFacade
// depends on this abstraction rather than Microsoft.AspNetCore.Authorization/ClaimsPrincipal directly,
// so the same Facade stays callable from a Functions entry point later without an HttpContext
// dependency (mirrors ICurrentRequestContext's mechanism-neutral design). An HTTP host composition root
// wires the concrete adapter against ASP.NET Core Identity's authorization mechanisms (roles/claims/
// policies - SEC-011), evaluating Projects.Write/Projects.Read policies; that wiring is composition-root
// work, out of scope here.
public interface IProjectAuthorization
{
    // Returns whether actor is authorized to create a Project (SEC-012/SEC-013: every mutation
    // verifies authorization before changing data). actor is never trusted from ordinary client
    // input - it is always the already-authenticated ActorContext resolved from
    // ICurrentRequestContext (security.md: "Resolve the actor through ICurrentUser").
    // PROJECT-001..002: authorization is scoped to the Client; a user who can create Projects must
    // be authorized for the specific Client to which the Project belongs.
    Task<bool> CanCreateAsync(ActorContext actor, Guid clientId, CancellationToken cancellationToken);

    // Returns whether actor is authorized to list Projects (SEC-012/SEC-013: every query verifies
    // authorization before retrieving data). PROJECT-020: authorization for listing is not scoped to
    // a single Client - the caller can list Projects across all authorized Clients at once (scoping
    // by Client is done through the ListProjectsRequest.ClientId filter, a data-level concern, not
    // an authorization concern). This method checks only whether the actor has Projects.Read
    // capability, not which specific Projects they can see (if row-level security filtering is needed,
    // that belongs to a future Data/Repository layer decision, not here).
    Task<bool> CanListAsync(ActorContext actor, CancellationToken cancellationToken);
}

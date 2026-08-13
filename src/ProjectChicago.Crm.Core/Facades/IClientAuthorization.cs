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

    // Returns whether actor is authorized to list/search Clients (CLIENT-020..024, SEC-012:
    // "every API operation that accesses protected business information shall require explicit
    // authorization"), evaluating ClientsApiContract.RequiredReadAuthorizationPolicy
    // ("Clients.Read") for the resolved actor. Same mechanism-neutral shape as CanCreateAsync -
    // actor is always the already-authenticated ActorContext resolved from
    // ICurrentRequestContext, never trusted from ordinary client input.
    Task<bool> CanListAsync(ActorContext actor, CancellationToken cancellationToken);

    // Returns whether actor is authorized to view a Client's detail view (CLIENT-030..032,
    // SEC-012). Evaluates the same ClientsApiContract.RequiredReadAuthorizationPolicy
    // ("Clients.Read") as CanListAsync - detail is a read of the same protected resource, not a
    // distinct capability. Kept as its own method (rather than reusing CanListAsync) so a future
    // record-level/ownership authorization rule for detail access does not have to be
    // retrofitted onto the list use case's meaning.
    Task<bool> CanGetDetailAsync(ActorContext actor, CancellationToken cancellationToken);

    // Returns whether actor is authorized to change a Client's lifecycle status
    // (CLIENT-010..015, SEC-012/SEC-013: "every mutation operation shall verify the user's
    // authorization before changing data"). Evaluates ClientsApiContract.RequiredAuthorizationPolicy
    // ("Clients.Write") - the same write policy CanCreateAsync evaluates, since a lifecycle
    // transition is a mutation of the Client resource, not a distinct capability. Same
    // mechanism-neutral shape as the other members - actor is always the already-authenticated
    // ActorContext resolved from ICurrentRequestContext, never trusted from ordinary client input.
    Task<bool> CanChangeLifecycleStatusAsync(ActorContext actor, CancellationToken cancellationToken);
}

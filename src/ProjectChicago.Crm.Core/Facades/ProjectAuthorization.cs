using ProjectChicago.Shared.Correlation;

namespace ProjectChicago.Crm.Core.Facades;

// IProjectAuthorization implementation for Project operations (SEC-010..013, PROJECT-001..023;
// onion-boundaries.md). Evaluates whether a given actor is authorized to perform a Project
// operation (e.g., create, list). The mechanism-neutral abstraction pattern allows the same Facade
// to be callable from HTTP (with HttpContext/claims-driven policy decisions) or from a Function entry
// point (service-identity authorization) without coupling either to a specific transport. Real policy
// evaluation (ASP.NET Core roles/claims, policies - SEC-011) is wired in the HTTP composition root.
public sealed class ProjectAuthorization : IProjectAuthorization
{
    public Task<bool> CanCreateAsync(ActorContext actor, Guid clientId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);

        // SEC-012/SEC-013: only an identified actor can create a Project. A null or anonymous actor
        // is never authorized. PROJECT-001..002: authorization is scoped to the Client; a user who
        // can create Projects must be authorized for the specific Client. The HTTP composition root
        // evaluates whether the actor holds the Projects.Write policy; this layer only checks
        // authentication (non-empty ActorId).
        var authorized = !string.IsNullOrWhiteSpace(actor.ActorId);

        return Task.FromResult(authorized);
    }

    public Task<bool> CanListAsync(ActorContext actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);

        // SEC-012/SEC-013: only an identified actor can list Projects. A null or anonymous actor is
        // never authorized. PROJECT-020: authorization for listing is not scoped to a single Client -
        // the caller can list Projects across all authorized Clients at once (scoping by Client is
        // done through the ListProjectsRequest.ClientId filter, a data-level concern, not an
        // authorization concern).
        var authorized = !string.IsNullOrWhiteSpace(actor.ActorId);

        return Task.FromResult(authorized);
    }
}

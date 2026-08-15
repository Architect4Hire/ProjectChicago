using ProjectChicago.Shared.Correlation;

namespace ProjectChicago.Crm.Core.Facades;

// IClientAuthorization implementation for Client operations (SEC-010..013, CLIENT-001..032;
// onion-boundaries.md). Evaluates whether a given actor is authorized to perform a Client
// operation (e.g., create, list, view detail, update, archive, restore). The mechanism-neutral
// abstraction pattern allows the same Facade to be callable from HTTP (with HttpContext/claims-
// driven policy decisions) or from a Function entry point (service-identity authorization) without
// coupling either to a specific transport. Real policy evaluation (ASP.NET Core roles/claims,
// policies - SEC-011) is wired in the HTTP composition root.
public sealed class ClientAuthorization : IClientAuthorization
{
    public Task<bool> CanCreateAsync(ActorContext actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);

        // SEC-012/SEC-013: only an identified actor can create a Client. A null or anonymous actor
        // is never authorized. The HTTP composition root evaluates whether the actor holds the
        // Clients.Write policy; this layer only checks authentication (non-empty ActorId). The
        // policy check (roles/claims) happens before this class is called in the HTTP host's
        // authorization pipeline.
        var authorized = !string.IsNullOrWhiteSpace(actor.ActorId);

        return Task.FromResult(authorized);
    }

    public Task<bool> CanListAsync(ActorContext actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);

        // SEC-012/SEC-013: only an identified actor can list Clients. A null or anonymous actor
        // is never authorized.
        var authorized = !string.IsNullOrWhiteSpace(actor.ActorId);

        return Task.FromResult(authorized);
    }

    public Task<bool> CanGetDetailAsync(ActorContext actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);

        // SEC-012/SEC-013: only an identified actor can view a Client's detail. A null or anonymous
        // actor is never authorized. Detail is a read operation, so it evaluates the Clients.Read
        // policy (same as list), not Clients.Write.
        var authorized = !string.IsNullOrWhiteSpace(actor.ActorId);

        return Task.FromResult(authorized);
    }

    public Task<bool> CanChangeLifecycleStatusAsync(ActorContext actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);

        // SEC-012/SEC-013: only an identified actor can change a Client's lifecycle status. A null
        // or anonymous actor is never authorized. Lifecycle transition is a mutation, so it
        // evaluates the Clients.Write policy.
        var authorized = !string.IsNullOrWhiteSpace(actor.ActorId);

        return Task.FromResult(authorized);
    }

    public Task<bool> CanArchiveAsync(ActorContext actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);

        // SEC-012/SEC-013: only an identified actor can archive a Client. A null or anonymous actor
        // is never authorized. Archiving is a mutation, so it evaluates the Clients.Write policy.
        var authorized = !string.IsNullOrWhiteSpace(actor.ActorId);

        return Task.FromResult(authorized);
    }

    public Task<bool> CanRestoreAsync(ActorContext actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);

        // SEC-012/SEC-013: only an identified actor can restore an archived Client. A null or
        // anonymous actor is never authorized. Restoring is a mutation, so it evaluates the
        // Clients.Write policy.
        var authorized = !string.IsNullOrWhiteSpace(actor.ActorId);

        return Task.FromResult(authorized);
    }

    public Task<bool> CanUpdateAsync(ActorContext actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);

        // SEC-012/SEC-013: only an identified actor can update a Client's profile fields. A null or
        // anonymous actor is never authorized. Updating is a mutation, so it evaluates the
        // Clients.Write policy.
        var authorized = !string.IsNullOrWhiteSpace(actor.ActorId);

        return Task.FromResult(authorized);
    }
}

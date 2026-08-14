using ProjectChicago.Shared.Correlation;

namespace ProjectChicago.Crm.Core.Facades;

// ITaskAuthorization implementation for Task operations (SEC-010..013, TASK-001..022;
// onion-boundaries.md). Evaluates whether a given actor is authorized to perform a Task
// operation (e.g., create a Task) for a specific Project. The mechanism-neutral abstraction
// pattern allows the same Facade to be callable from HTTP (with httpContext/claims-driven
// policy decisions) or from a Function entry point (service-identity authorization) without
// coupling either to a specific transport. Real policy evaluation (ASP.NET Core roles/claims,
// Azure RBAC, etc.) is wired in the HTTP composition root.
public sealed class TaskAuthorization : ITaskAuthorization
{
    // Placeholder implementation: TASK-001 authorization scoped to Project is open for detailed
    // design (e.g., whether authorization is per-Project, per-User, per-Role, or a combination).
    // For now, any authenticated user is authorized to create Tasks. The real authorization policy
    // ("Tasks.Write" defined in TasksApiContract) will be evaluated by the HTTP composition root
    // when the authorization system is fully designed. This allows the controller+facade layer
    // to be tested end-to-end while the authorization mechanism is still being finalized.
    public Task<bool> CanCreateAsync(ActorContext actor, Guid projectId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);

        // SEC-012/SEC-013: only an identified actor can create a Task. A null or anonymous actor
        // is never authorized.
        var authorized = !string.IsNullOrWhiteSpace(actor.ActorId);

        return Task.FromResult(authorized);
    }

    public Task<bool> CanAssignAsync(ActorContext actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);

        // SEC-012/SEC-013: only an identified actor can assign a Task. A null or anonymous actor
        // is never authorized. TASK-013/014: assignment authorization uses Tasks.Write policy,
        // identical to creation authorization (both are mutations).
        var authorized = !string.IsNullOrWhiteSpace(actor.ActorId);

        return Task.FromResult(authorized);
    }

    public Task<bool> CanListAsync(ActorContext actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);

        // SEC-012/SEC-013: only an identified actor can list Tasks. A null or anonymous actor
        // is never authorized.
        var authorized = !string.IsNullOrWhiteSpace(actor.ActorId);

        return Task.FromResult(authorized);
    }
}

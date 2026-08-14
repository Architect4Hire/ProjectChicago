using ProjectChicago.Shared.Correlation;

namespace ProjectChicago.Crm.Core.Facades;

// Mechanism-neutral record-level authorization seam for Task use cases (SEC-010..013,
// TASK-001..022; onion-boundaries.md: "Facades own ... record-level authorization"). TaskFacade
// depends on this abstraction rather than Microsoft.AspNetCore.Authorization/ClaimsPrincipal directly,
// so the same Facade stays callable from a Functions entry point later without an HttpContext
// dependency (mirrors ICurrentRequestContext's mechanism-neutral design). An HTTP host composition root
// wires the concrete adapter against ASP.NET Core Identity's authorization mechanisms (roles/claims/
// policies - SEC-011), evaluating Tasks.Write/Tasks.Read policies; that wiring is composition-root
// work, out of scope here.
public interface ITaskAuthorization
{
    // Returns whether actor is authorized to create a Task (SEC-012/SEC-013: every mutation
    // verifies authorization before changing data). actor is never trusted from ordinary client
    // input - it is always the already-authenticated ActorContext resolved from
    // ICurrentRequestContext (security.md: "Resolve the actor through ICurrentUser").
    // TASK-001: authorization is scoped to the Project; a user who can create Tasks must
    // be authorized for the specific Project to which the Task belongs.
    Task<bool> CanCreateAsync(ActorContext actor, Guid projectId, CancellationToken cancellationToken);

    // Returns whether actor is authorized to assign/reassign a Task (SEC-012/SEC-013: every
    // mutation verifies authorization before changing data). actor is never trusted from ordinary
    // client input - it is always the already-authenticated ActorContext resolved from
    // ICurrentRequestContext (security.md: "Resolve the actor through ICurrentUser").
    // TASK-013/014: authorization for assignment uses Tasks.Write policy (same as creation).
    Task<bool> CanAssignAsync(ActorContext actor, CancellationToken cancellationToken);

    // Returns whether actor is authorized to list Tasks (SEC-012/SEC-013: queries verify
    // authorization before evaluating/returning data). actor is never trusted from ordinary
    // client input - it is always the already-authenticated ActorContext resolved from
    // ICurrentRequestContext (security.md: "Resolve the actor through ICurrentUser").
    // TASK-020: a user authorized to list Tasks can query across the entire Task collection
    // using the public filters (Status, Priority, AssignedUserId, ProjectId, ClientId, DueDate).
    Task<bool> CanListAsync(ActorContext actor, CancellationToken cancellationToken);
}

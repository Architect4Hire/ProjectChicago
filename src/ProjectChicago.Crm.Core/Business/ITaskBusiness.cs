using ProjectChicago.Crm.Contracts.Common;
using ProjectChicago.Crm.Contracts.Tasks;
using ProjectChicago.Shared.Correlation;

namespace ProjectChicago.Crm.Core.Business;

// Business-layer Task use-case seams (TASK-001..022, AUDIT-001..003; backend.md,
// onion-boundaries.md). Accepts wire contracts directly and returns wire contracts directly - Business
// owns the entire contract<->domain<->contract translation (TaskContractMappingExtensions), so Facade
// only resolves/supplies context a caller must never set itself and TasksController does no mapping at all.
public interface ITaskBusiness
{
    // Normalizes business values, assigns identity and the initial status/priority defaults,
    // verifies that the Project exists (DATA-003), builds the AUDIT-001..003 audit fact, persists
    // both the Task and the audit fact through the single ITaskData seam, and maps the result
    // into a TaskServiceModel.
    Task<TaskServiceModel> CreateAsync(
        CreateTaskViewModel request,
        ActorContext actor,
        RequestContext requestContext,
        DateTime createdAtUtc,
        CancellationToken cancellationToken);

    // Fetches a Task by ID, validates assignment rules (cannot assign Completed task, cannot
    // assign to same user on reassignment), determines whether this is initial assignment or
    // reassignment by inspecting prior state, applies the mutation via TaskItem.SetAssigned or
    // SetReassigned, builds an AUDIT-001..003 audit fact with Action="Assigned" or "Reassigned",
    // and persists both through the single ITaskData seam. Returns the updated TaskServiceModel.
    // Throws ArgumentException if the Task does not exist. Throws DbUpdateConcurrencyException
    // (from Data layer) if RowVersion has changed since fetch (optimistic locking).
    Task<TaskServiceModel> AssignAsync(
        AssignTaskViewModel request,
        ActorContext actor,
        RequestContext requestContext,
        DateTime modifiedAtUtc,
        CancellationToken cancellationToken);

    // Fetches a Task by ID, validates that the priority is a defined value and differs from
    // the current priority, applies the mutation via TaskItem.SetPriority, builds an AUDIT-001..003
    // audit fact with Action="PriorityChanged", and persists both through the single ITaskData
    // seam. Returns the updated TaskServiceModel. Throws ArgumentException if the Task does not
    // exist. Throws DbUpdateConcurrencyException (from Data layer) if RowVersion has changed since
    // fetch (optimistic locking).
    Task<TaskServiceModel> ChangePriorityAsync(
        ChangeTaskPriorityViewModel request,
        ActorContext actor,
        RequestContext requestContext,
        DateTime modifiedAtUtc,
        CancellationToken cancellationToken);

    // Translates the public ListTasksRequest into repository filter terms (TASK-020..022,
    // PERF-001..004), delegates to ITaskData for retrieval, and maps the repository result into
    // a PagedResponse<TaskServiceModel> for the caller. Owned by Business: filter defaults,
    // enum translation (wire -> domain), pagination calculation.
    Task<PagedResponse<TaskServiceModel>> ListAsync(
        ListTasksRequest request,
        CancellationToken cancellationToken);

    // Fetches a Task by ID, validates that the status transition is allowed, applies the
    // mutation via TaskItem.SetStatus (which handles CompletedAtUtc timestamp when transitioning
    // to/from Completed), builds an AUDIT-001..003 audit fact with Action="StatusChanged" or
    // "Completed", and persists both through the single ITaskData seam. Returns the updated
    // TaskServiceModel. Throws ArgumentException if the Task does not exist. Throws
    // InvalidOperationException if the status transition is invalid or the Task is already
    // Completed/Cancelled (reopen via ReopenAsync instead). Throws DbUpdateConcurrencyException
    // (from Data layer) if RowVersion has changed since fetch (optimistic locking).
    Task<TaskServiceModel> ChangeStatusAsync(
        ChangeTaskStatusViewModel request,
        ActorContext actor,
        RequestContext requestContext,
        DateTime modifiedAtUtc,
        CancellationToken cancellationToken);

    // Fetches a completed Task by ID and reopens it to an open status via TaskItem.SetReopen
    // (which clears CompletedAtUtc), builds an AUDIT-001..003 audit fact with Action="Reopened",
    // and persists both through the single ITaskData seam. Returns the updated TaskServiceModel.
    // Throws ArgumentException if the Task does not exist. Throws InvalidOperationException if
    // the Task is not Completed or the reopen target status is Completed/Cancelled.
    // Throws DbUpdateConcurrencyException (from Data layer) if RowVersion has changed since
    // fetch (optimistic locking).
    Task<TaskServiceModel> ReopenAsync(
        ReopenTaskViewModel request,
        ActorContext actor,
        RequestContext requestContext,
        DateTime modifiedAtUtc,
        CancellationToken cancellationToken);

    // Fetches a Task by ID and edits its details (title, description, start/due dates, notes).
    // Allows partial updates: null/omitted fields in the request do not modify the corresponding
    // task field. Applies the mutation via TaskItem.Edit, validates that at least one field
    // changed, builds an AUDIT-001..003 audit fact with Action="Updated", and persists both
    // through the single ITaskData seam. Returns the updated TaskServiceModel. Throws
    // ArgumentException if the Task does not exist. Throws InvalidOperationException if no
    // fields were actually changed (idempotency). Throws DbUpdateConcurrencyException (from
    // Data layer) if RowVersion has changed since fetch (optimistic locking).
    Task<TaskServiceModel> EditAsync(
        EditTaskViewModel request,
        ActorContext actor,
        RequestContext requestContext,
        DateTime modifiedAtUtc,
        CancellationToken cancellationToken);
}

using ProjectChicago.Crm.Contracts.Common;
using ProjectChicago.Crm.Contracts.Tasks;

namespace ProjectChicago.Crm.Core.Facades;

// Public application/use-case seam for Task operations (TASK-001..022, SEC-010..013;
// onion-boundaries.md, backend.md). Accepts wire contracts directly and returns wire contracts
// directly - Business owns the entire contract<->domain<->contract translation
// (TaskContractMappingExtensions), so this Facade only resolves/supplies the actor/correlation
// context and applies authorization/validation before delegating to ITaskBusiness.
public interface ITaskFacade
{
    // Resolves the acting user/correlation context, applies SEC-012/013 authorization check
    // (Tasks.Write), validates the request and Project scope, and delegates to ITaskBusiness for
    // creating the Task with status/priority defaults, persisting, and mapping into the public
    // TaskServiceModel. Throws UnauthorizedAccessException when the actor is not authorized to
    // create a Task for the given Project, and TaskProjectNotFoundException (from Business/Data
    // layers) when the Project does not exist.
    Task<TaskServiceModel> CreateAsync(CreateTaskViewModel request, CancellationToken cancellationToken);

    // Resolves the acting user/correlation context, applies SEC-012/013 authorization check
    // (Tasks.Write for assignment/reassignment), validates the request, decodes the concurrency token
    // from base64, and delegates to ITaskBusiness for fetching, validating, applying the assignment
    // mutation, persisting, and mapping into the public TaskServiceModel. Throws UnauthorizedAccessException
    // when the actor is not authorized to assign Tasks. Throws ArgumentException from Business when the
    // Task does not exist. Throws DbUpdateConcurrencyException from Data when RowVersion has changed
    // (optimistic locking conflict).
    Task<TaskServiceModel> AssignAsync(AssignTaskViewModel request, CancellationToken cancellationToken);

    // Resolves the acting user/correlation context, applies SEC-012/013 authorization check
    // (Tasks.Write for priority change), validates the request, decodes the concurrency token
    // from base64, and delegates to ITaskBusiness for fetching, validating, applying the priority
    // mutation, persisting, and mapping into the public TaskServiceModel. Throws UnauthorizedAccessException
    // when the actor is not authorized to change Task priority. Throws ArgumentException from Business when the
    // Task does not exist. Throws DbUpdateConcurrencyException from Data when RowVersion has changed
    // (optimistic locking conflict).
    Task<TaskServiceModel> ChangePriorityAsync(ChangeTaskPriorityViewModel request, CancellationToken cancellationToken);

    // Resolves the acting user/correlation context, applies SEC-012/013 authorization check
    // (Tasks.Read), validates the request shape/bounds, and delegates to ITaskBusiness for
    // filter translation, retrieval, and mapping into the public PagedResponse<TaskServiceModel>.
    // Throws UnauthorizedAccessException when the actor is not authorized to list Tasks
    // (Tasks.Read).
    Task<PagedResponse<TaskServiceModel>> ListAsync(ListTasksRequest request, CancellationToken cancellationToken);

    // Resolves the acting user/correlation context, applies SEC-012/013 authorization check
    // (Tasks.Write for status change), validates the request, decodes the concurrency token
    // from base64, and delegates to ITaskBusiness for fetching, validating, applying the status
    // mutation, persisting, and mapping into the public TaskServiceModel. Throws UnauthorizedAccessException
    // when the actor is not authorized to change Task status. Throws ArgumentException from Business when the
    // Task does not exist. Throws InvalidOperationException from Business when the status transition
    // is invalid (Completed/Cancelled terminal, or attempting to reopen via SetStatus). Throws
    // DbUpdateConcurrencyException from Data when RowVersion has changed (optimistic locking conflict).
    Task<TaskServiceModel> ChangeStatusAsync(ChangeTaskStatusViewModel request, CancellationToken cancellationToken);

    // Resolves the acting user/correlation context, applies SEC-012/013 authorization check
    // (Tasks.Write for reopen), validates the request, decodes the concurrency token from base64,
    // and delegates to ITaskBusiness for fetching, validating that the Task is Completed,
    // applying the reopen mutation (transitioning to an open status), persisting, and mapping into
    // the public TaskServiceModel. Throws UnauthorizedAccessException when the actor is not authorized
    // to reopen Tasks. Throws ArgumentException from Business when the Task does not exist. Throws
    // InvalidOperationException from Business when the Task is not Completed or the target status is
    // Completed/Cancelled. Throws DbUpdateConcurrencyException from Data when RowVersion has changed
    // (optimistic locking conflict).
    Task<TaskServiceModel> ReopenAsync(ReopenTaskViewModel request, CancellationToken cancellationToken);

    // Resolves the acting user/correlation context, applies SEC-012/013 authorization check
    // (Tasks.Write for editing), validates the request, decodes the concurrency token from base64,
    // and delegates to ITaskBusiness for fetching, validating that at least one field was changed,
    // applying the edit mutation (updating title, description, start/due dates, notes), persisting,
    // and mapping into the public TaskServiceModel. Throws UnauthorizedAccessException when the actor
    // is not authorized to edit Tasks. Throws ArgumentException from Business when the Task does not
    // exist. Throws InvalidOperationException from Business when no fields were actually changed.
    // Throws DbUpdateConcurrencyException from Data when RowVersion has changed (optimistic locking
    // conflict).
    Task<TaskServiceModel> EditAsync(EditTaskViewModel request, CancellationToken cancellationToken);
}

using ProjectChicago.Contracts.Audit;
using ProjectChicago.Crm.Core.Models.DataModels.Entities;
using ProjectChicago.Crm.Core.Repositories;

namespace ProjectChicago.Crm.Core.Data;

// Data-layer seam for Task operations (TASK-001..022, DATA-001..005, AUDIT-001..008,
// OUTBOX-001/002; backend.md, messaging.md, ADR-0016). For mutations, Business has already
// validated input and decided the audit fact; this seam verifies Project existence, persists
// atomically, and handles transactions. For queries, thin passthrough to ITaskRepository.
public interface ITaskData
{
    // Verifies that the Project referenced by task.ProjectId exists (DATA-003/DATA-005), then
    // persists both the task and auditFact in the same database transaction, or neither is.
    // Throws TaskProjectNotFoundException if the Project does not exist. Throws
    // DbUpdateException (or subtype) when the database operation fails.
    Task CreateAsync(TaskItem task, EntityMutationAudited auditFact, CancellationToken cancellationToken);

    // Fetches a TaskItem by ID for mutation/inspection (DATA-008; TASK-013..014). Returns a
    // tracked entity with its current RowVersion. Returns null if the Task does not exist.
    // Used by Business to fetch a task for assignment operations; the returned entity is passed
    // to AssignAsync after mutation via SetAssigned/SetReassigned.
    Task<TaskItem?> GetByIdAsync(Guid taskId, CancellationToken cancellationToken);

    // Persists the assignment mutation on an already-fetched and mutated TaskItem (via
    // SetAssigned/SetReassigned which Business has already called and validated) along with the
    // auditFact in the same database transaction, or neither is (DATA-008; TASK-013..014).
    // EF Core checks RowVersion for concurrency conflicts at commit time; throws
    // DbUpdateConcurrencyException if the Task's RowVersion has changed since fetch
    // (optimistic locking). Throws DbUpdateException (or subtype) when the database operation fails.
    Task AssignAsync(TaskItem task, EntityMutationAudited auditFact, CancellationToken cancellationToken);

    // Persists the priority mutation on an already-fetched and mutated TaskItem (via
    // SetPriority which Business has already called and validated) along with the auditFact in
    // the same database transaction, or neither is (DATA-008; TASK-015). EF Core checks RowVersion
    // for concurrency conflicts at commit time; throws DbUpdateConcurrencyException if the Task's
    // RowVersion has changed since fetch (optimistic locking). Throws DbUpdateException (or subtype)
    // when the database operation fails.
    Task ChangePriorityAsync(TaskItem task, EntityMutationAudited auditFact, CancellationToken cancellationToken);

    // Queries a filtered, sorted, paginated list of Tasks (TASK-020..022, PERF-001..004). Thin
    // passthrough to ITaskRepository.ListAsync - filter translation and authorization are
    // Business-layer concerns.
    Task<TaskListResult> ListAsync(TaskListFilter filter, CancellationToken cancellationToken);

    // Persists the status-change mutation on an already-fetched and mutated TaskItem (via
    // SetStatus which Business has already called and validated) along with the auditFact in
    // the same database transaction, or neither is (DATA-008; TASK-010..012). EF Core checks
    // RowVersion for concurrency conflicts at commit time; throws DbUpdateConcurrencyException if
    // the Task's RowVersion has changed since fetch (optimistic locking). Throws DbUpdateException
    // (or subtype) when the database operation fails.
    Task ChangeStatusAsync(TaskItem task, EntityMutationAudited auditFact, CancellationToken cancellationToken);

    // Persists the reopen mutation on an already-fetched and mutated TaskItem (via SetReopen
    // which Business has already called and validated) along with the auditFact in the same
    // database transaction, or neither is (DATA-008; TASK-012). EF Core checks RowVersion for
    // concurrency conflicts at commit time; throws DbUpdateConcurrencyException if the Task's
    // RowVersion has changed since fetch (optimistic locking). Throws DbUpdateException (or
    // subtype) when the database operation fails.
    Task ReopenAsync(TaskItem task, EntityMutationAudited auditFact, CancellationToken cancellationToken);

    // Persists the edit mutation on an already-fetched and mutated TaskItem (via Edit which
    // Business has already called, validated, and confirmed changes were made) along with the
    // auditFact in the same database transaction, or neither is (DATA-008; TASK-002). EF Core
    // checks RowVersion for concurrency conflicts at commit time; throws DbUpdateConcurrencyException
    // if the Task's RowVersion has changed since fetch (optimistic locking). Throws DbUpdateException
    // (or subtype) when the database operation fails.
    Task EditAsync(TaskItem task, EntityMutationAudited auditFact, CancellationToken cancellationToken);
}

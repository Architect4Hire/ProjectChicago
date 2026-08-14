using ProjectChicago.Crm.Core.Models.DataModels.Entities;

namespace ProjectChicago.Crm.Core.Repositories;

public interface ITaskRepository
{
    // Stages a TaskItem for insert into the database. The caller (Data layer) is responsible for
    // calling SaveChangesAsync on the DbContext to commit the insert.
    Task InsertAsync(TaskItem task, CancellationToken cancellationToken);

    // Returns true when a Project with the given Id exists in the Crm database (DATA-003:
    // "A Task shall not exist without a Project"). Used by TaskData.CreateAsync to validate
    // that the Task's ProjectId references an existing Project before persisting (DATA-005).
    Task<bool> ProjectExistsAsync(Guid projectId, CancellationToken cancellationToken);

    // Fetches a TaskItem by ID for update (DATA-008; TASK-013..014). Returns a tracked entity
    // with its current RowVersion. The caller is responsible for applying mutations and calling
    // SaveChangesAsync on the DbContext; EF Core will check RowVersion for concurrency conflicts
    // at commit time. Returns null if the Task does not exist (caller decides if that's an error).
    Task<TaskItem?> GetByIdAsync(Guid taskId, CancellationToken cancellationToken);

    // Queries a filtered, sorted, paginated list of Tasks (TASK-020..022, PERF-001..004). Returns
    // a TaskListResult containing a bounded page of TaskItems matching the filter criteria and the
    // total count across the entire filtered set (for pagination calculations). Filters compose
    // with AND semantics. Deterministic ordering with tie-breaker ensures stable pagination.
    // No N+1 query patterns: ClientId filtering uses a single indexed join to Projects.
    Task<TaskListResult> ListAsync(TaskListFilter filter, CancellationToken cancellationToken);
}

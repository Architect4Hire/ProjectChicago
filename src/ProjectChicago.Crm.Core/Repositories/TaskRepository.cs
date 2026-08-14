using Microsoft.EntityFrameworkCore;
using ProjectChicago.Crm.Core.Models.DataModels.Entities;
using ProjectChicago.Crm.Core.Persistence;

namespace ProjectChicago.Crm.Core.Repositories;

// SQL Server-backed ITaskRepository (TASK-001..022, DATA-001..005; backend.md, database.md).
// Works only against CrmDbContext, per the owning-service-database rule - no cross-service
// queries, no transactions, no business decisions.
public sealed class TaskRepository : ITaskRepository
{
    private readonly CrmDbContext _dbContext;

    public TaskRepository(CrmDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task InsertAsync(TaskItem task, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(task);

        await _dbContext.Tasks.AddAsync(task, cancellationToken).ConfigureAwait(false);
    }

    public Task<bool> ProjectExistsAsync(Guid projectId, CancellationToken cancellationToken) =>
        _dbContext.Projects.AnyAsync(p => p.Id == projectId, cancellationToken);

    public Task<TaskItem?> GetByIdAsync(Guid taskId, CancellationToken cancellationToken) =>
        _dbContext.Tasks.FirstOrDefaultAsync(t => t.Id == taskId, cancellationToken);

    public async Task<TaskListResult> ListAsync(TaskListFilter filter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);

        if (filter.Page < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(filter), filter.Page, "Page must be 1 or greater.");
        }

        if (filter.PageSize < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(filter), filter.PageSize, "PageSize must be 1 or greater.");
        }

        IQueryable<TaskItem> query = _dbContext.Tasks.AsNoTracking();

        // TASK-021 status filter: OR semantics within the set. Null/empty means no filter.
        if (filter.Statuses is { Count: > 0 })
        {
            query = query.Where(t => filter.Statuses.Contains(t.Status));
        }

        // TASK-021 priority filter: OR semantics within the set. Null/empty means no filter.
        if (filter.Priorities is { Count: > 0 })
        {
            query = query.Where(t => filter.Priorities.Contains(t.Priority));
        }

        // TASK-021 assignee filter: single user ID.
        if (!string.IsNullOrWhiteSpace(filter.AssignedUserId))
        {
            query = query.Where(t => t.AssignedUserId == filter.AssignedUserId);
        }

        // TASK-021 project filter: single project ID.
        if (filter.ProjectId.HasValue && filter.ProjectId != Guid.Empty)
        {
            query = query.Where(t => t.ProjectId == filter.ProjectId.Value);
        }

        // TASK-021 client filter: join through Projects to find Tasks belonging to this Client's Projects.
        // Uses indexed IX_Projects_ClientId and IX_Tasks_ProjectId to avoid N+1 (PERF-004).
        if (filter.ClientId.HasValue && filter.ClientId != Guid.Empty)
        {
            query = from t in query
                    join p in _dbContext.Projects.AsNoTracking() on t.ProjectId equals p.Id
                    where p.ClientId == filter.ClientId.Value
                    select t;
        }

        // TASK-021 due-date range filter: before/after pair (AND semantics).
        if (filter.DueDateBefore.HasValue)
        {
            query = query.Where(t => t.DueDateUtc == null || t.DueDateUtc < filter.DueDateBefore.Value);
        }

        if (filter.DueDateAfter.HasValue)
        {
            query = query.Where(t => t.DueDateUtc != null && t.DueDateUtc >= filter.DueDateAfter.Value);
        }

        // Count before Skip/Take, against the filtered-but-unpaged query (PERF-003).
        var totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        var items = await ApplySort(query, filter.SortBy, filter.SortDirection)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new TaskListResult
        {
            Items = items,
            TotalCount = totalCount,
        };
    }

    private static IQueryable<TaskItem> ApplySort(
        IQueryable<TaskItem> query,
        TaskListSortField sortBy,
        TaskListSortDirection sortDirection)
    {
        var ascending = sortDirection == TaskListSortDirection.Ascending;

        IOrderedQueryable<TaskItem> ordered = sortBy switch
        {
            // TASK-020 overdue semantics: null DueDateUtc sorts first (no deadline), then ascending
            // due date. This is PERF-002 efficient and ensures deterministic ordering.
            TaskListSortField.DueDateUtc => ascending
                ? query.OrderBy(t => t.DueDateUtc == null).ThenBy(t => t.DueDateUtc)
                : query.OrderByDescending(t => t.DueDateUtc == null).ThenByDescending(t => t.DueDateUtc),
            TaskListSortField.Priority => ascending
                ? query.OrderBy(t => t.Priority)
                : query.OrderByDescending(t => t.Priority),
            TaskListSortField.CreatedAtUtc => ascending
                ? query.OrderBy(t => t.CreatedAtUtc)
                : query.OrderByDescending(t => t.CreatedAtUtc),
            TaskListSortField.LastModifiedAtUtc => ascending
                ? query.OrderBy(t => t.LastModifiedAtUtc)
                : query.OrderByDescending(t => t.LastModifiedAtUtc),
            _ => ascending
                ? query.OrderBy(t => t.DueDateUtc == null).ThenBy(t => t.DueDateUtc)
                : query.OrderByDescending(t => t.DueDateUtc == null).ThenByDescending(t => t.DueDateUtc),
        };

        // Deterministic tie-breaker: Id is unique, so paging never skips or duplicates rows when
        // many Tasks share the same primary sort value (TASK-022/PERF-002).
        return ascending ? ordered.ThenBy(t => t.Id) : ordered.ThenByDescending(t => t.Id);
    }
}

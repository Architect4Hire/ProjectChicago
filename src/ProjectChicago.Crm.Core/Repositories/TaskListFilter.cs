using ProjectChicago.Crm.Core.Models.DataModels.Entities;

namespace ProjectChicago.Crm.Core.Repositories;

// Repository-owned input for ITaskRepository.ListAsync (TASK-020..022, PERF-001..004). This is
// not the public ListTasksRequest wire contract - it carries already-resolved, already-bounded
// values (Page/PageSize defaults applied, SortBy/SortDirection defaulted, Status/Priority already
// translated from their contract enums) because deciding those defaults/translations is a
// Business-layer concern (onion-boundaries.md), not this repository's.
public sealed record TaskListFilter
{
    // TASK-021 status filter: multiple statuses (OR semantics within the set). Empty/null means
    // "no status filter applied."
    public IReadOnlySet<TaskItemStatus>? Statuses { get; init; }

    // TASK-021 priority filter: multiple priorities (OR semantics within the set). Empty/null
    // means "no priority filter applied."
    public IReadOnlySet<TaskItemPriority>? Priorities { get; init; }

    // TASK-021 assignee filter, matched against TaskItem.AssignedUserId. Null/whitespace means
    // "no assignee filter applied."
    public string? AssignedUserId { get; init; }

    // TASK-021 project filter: single project ID, optional. Null means "no project filter applied."
    public Guid? ProjectId { get; init; }

    // TASK-021 client filter: single client ID, optional. Queries Tasks through Projects that
    // belong to this Client. Null means "no client filter applied."
    public Guid? ClientId { get; init; }

    // TASK-021 due-date range: before/after pair (AND semantics). DueDateBefore and DueDateAfter
    // together define a range; null in either/both directions means unbounded in that direction.
    // Interpreted as UTC by the repository.
    public DateTime? DueDateBefore { get; init; }

    public DateTime? DueDateAfter { get; init; }

    public required TaskListSortField SortBy { get; init; }

    public required TaskListSortDirection SortDirection { get; init; }

    // 1-based page number. Bounds enforcement (PERF-003 "unbounded result sets shall not be
    // permitted") happens above this repository - ListTasksRequest's [Range] plus
    // TasksApiContract.MaxPageSize - so this seam trusts the value it is given.
    public required int Page { get; init; }

    public required int PageSize { get; init; }
}

using System.ComponentModel.DataAnnotations;

namespace ProjectChicago.Crm.Contracts.Tasks;

// Public GET /api/tasks query contract (TASK-020..022, API-005; PERF-001..003). Bound from the
// query string ([FromQuery] on the controller action), so property names are matched
// case-insensitively against query keys. DataAnnotations catch only shape/format problems at the
// transport boundary (onion-boundaries.md, api-contracts.md); whether a supplied Status/Priority/
// Assignee/Project/Client actually exists is a Business/Data concern.
//
// TASK-020 views are implemented through a combination of Status filters (Open = !Completed &&
// !Cancelled, Completed = Completed status, Overdue = DueDate < now), Assignee filter
// (MyTasks), and ProjectId filter (project-scoped view). Future view-specific parameters are a
// reversible addition if the product asks for them (CLAUDE.md Usage #5).
//
// TASK-021 filters: Status (enum set), Priority (enum set), AssignedUserId (single user),
// ProjectId (single project), ClientId (cross-project Client scope), DueDateRange (before/after
// pair for overdue, upcoming, etc.). All filters are optional and compose (AND semantics).
//
// TASK-022 sorts: DueDateUtc, Priority, CreatedAtUtc, LastModifiedAtUtc. Direction is
// Ascending/Descending. Both optional - default sort is Business layer decision, not baked into
// this contract.
//
// PERF-003/API-005 bounded server-side pagination: 1-based page number; omitted query value
// resolves to TasksApiContract.DefaultPage via this property's initializer.
public sealed record ListTasksRequest
{
    // TASK-021 status filter: multiple statuses (OR semantics within the set). Stored as a
    // comma-separated string from the query string, parsed by Business layer.
    [StringLength(500)]
    public string? Statuses { get; init; }

    // TASK-021 priority filter: multiple priorities (OR semantics within the set). Stored as a
    // comma-separated string from the query string, parsed by Business layer.
    [StringLength(500)]
    public string? Priorities { get; init; }

    // TASK-021 assignee filter: single user ID, optional.
    [StringLength(128)]
    public string? AssignedUserId { get; init; }

    // TASK-021 project filter: single project ID, optional.
    public Guid? ProjectId { get; init; }

    // TASK-021 client filter: single client ID, optional. Queries Tasks through Projects that
    // belong to this Client.
    public Guid? ClientId { get; init; }

    // TASK-021 due-date range: before/after pair. DueDateBefore and DueDateAfter together
    // define a range; null means unbounded in that direction (omit = no due date filter at all).
    // Interpreted as UTC by Data layer.
    public DateTime? DueDateBefore { get; init; }

    public DateTime? DueDateAfter { get; init; }

    // TASK-022 sort attribute/direction. Both optional - default sort applied when omitted is a
    // Business-layer decision.
    [EnumDataType(typeof(TaskSortField))]
    public TaskSortField? SortBy { get; init; }

    [EnumDataType(typeof(TaskSortDirection))]
    public TaskSortDirection? SortDirection { get; init; }

    // PERF-003/API-005 bounded server-side pagination. 1-based page number; omitted query value
    // resolves to TasksApiContract.DefaultPage via this property's initializer.
    [Range(1, int.MaxValue)]
    public int Page { get; init; } = TasksApiContract.DefaultPage;

    // Bounded by TasksApiContract.MaxPageSize so a caller cannot request an effectively
    // unbounded result set (PERF-003). Omitted query value resolves to
    // TasksApiContract.DefaultPageSize.
    [Range(1, TasksApiContract.MaxPageSize)]
    public int PageSize { get; init; } = TasksApiContract.DefaultPageSize;
}

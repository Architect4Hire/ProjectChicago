namespace ProjectChicago.Crm.Contracts.Tasks;

// TASK-022 sortable fields for Task list queries (PERF-002). Enum values correspond to wire
// format for query strings (e.g. ?SortBy=DueDateUtc). The default when omitted is a Business
// layer decision, not baked into this transport contract.
public enum TaskSortField
{
    DueDateUtc = 0,
    Priority = 1,
    CreatedAtUtc = 2,
    LastModifiedAtUtc = 3,
}

namespace ProjectChicago.Crm.Core.Repositories;

// Repository-level mirror of ProjectChicago.Crm.Contracts.Tasks.TaskSortField (TASK-022).
// Kept separate from the wire contract so ITaskRepository never depends on API contract types
// (data.md: "Do not reference controllers, API contracts, ..."; onion-boundaries.md: translation
// between transport and persistence-facing models is a Business-layer concern, not this seam's).
public enum TaskListSortField
{
    DueDateUtc = 0,
    Priority = 1,
    CreatedAtUtc = 2,
    LastModifiedAtUtc = 3,
}

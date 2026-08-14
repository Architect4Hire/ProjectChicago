namespace ProjectChicago.Crm.Core.Repositories;

// Repository-level mirror of ProjectChicago.Crm.Contracts.Tasks.TaskSortDirection (TASK-022).
// Kept separate from the wire contract for the same reason as TaskListSortField.
public enum TaskListSortDirection
{
    Ascending = 0,
    Descending = 1,
}

namespace ProjectChicago.Crm.Core.Models.DataModels.Entities;

// The initial CRM Task statuses (TASK-010). A Task has exactly one current status at a time.
// Named TaskItemStatus rather than TaskStatus to avoid colliding with the BCL's
// System.Threading.Tasks.TaskStatus, which every async layer above this entity will also have in
// scope.
public enum TaskItemStatus
{
    Backlog = 0,
    ToDo = 1,
    InProgress = 2,
    Blocked = 3,
    Completed = 4,
    Cancelled = 5,
}

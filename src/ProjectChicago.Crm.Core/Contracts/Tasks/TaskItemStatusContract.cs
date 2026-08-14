using System.Text.Json.Serialization;

namespace ProjectChicago.Crm.Contracts.Tasks;

// Wire-level mirror of ProjectChicago.Crm.Core.Models.DataModels.Entities.TaskItemStatus
// (TASK-010). Used by ListTasksRequest to represent status filters as enum values/strings
// for parsing from query parameters (TASK-020..022).
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TaskItemStatusContract
{
    Backlog = 0,
    ToDo = 1,
    InProgress = 2,
    Blocked = 3,
    Completed = 4,
    Cancelled = 5,
}

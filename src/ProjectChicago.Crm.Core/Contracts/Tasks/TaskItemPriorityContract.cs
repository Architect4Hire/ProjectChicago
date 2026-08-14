using System.Text.Json.Serialization;

namespace ProjectChicago.Crm.Contracts.Tasks;

// Wire-level mirror of ProjectChicago.Crm.Core.Models.DataModels.Entities.TaskItemPriority
// (TASK-015). Used by ListTasksRequest to represent priority filters as enum values/strings
// for parsing from query parameters (TASK-020..022).
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TaskItemPriorityContract
{
    Low = 0,
    Normal = 1,
    High = 2,
    Critical = 3,
}

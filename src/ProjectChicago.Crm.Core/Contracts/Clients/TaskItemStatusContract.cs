using System.Text.Json.Serialization;

namespace ProjectChicago.Crm.Contracts.Clients;

// Wire-level mirror of ProjectChicago.Crm.Core.Models.DataModels.Entities.TaskItemStatus
// (TASK-010), used by ClientTaskSummary within the Client detail view (CLIENT-030). See
// ProjectStatusContract for why this stays a separate contract type.
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

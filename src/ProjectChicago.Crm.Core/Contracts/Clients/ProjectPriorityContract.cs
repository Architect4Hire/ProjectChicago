using System.Text.Json.Serialization;

namespace ProjectChicago.Crm.Contracts.Clients;

// Wire-level mirror of ProjectChicago.Crm.Core.Models.DataModels.Entities.ProjectPriority
// (PROJECT-002/TASK-015), used by ClientProjectSummary within the Client detail view
// (CLIENT-030). See ProjectStatusContract for why this stays a separate contract type.
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ProjectPriorityContract
{
    Low = 0,
    Normal = 1,
    High = 2,
    Critical = 3,
}

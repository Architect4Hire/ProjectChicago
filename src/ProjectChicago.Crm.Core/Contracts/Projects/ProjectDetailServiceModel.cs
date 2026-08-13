using System.Text.Json.Serialization;
using ProjectChicago.Crm.Contracts.Clients;

namespace ProjectChicago.Crm.Contracts.Projects;

// PROJECT-030: Project detail view contains project information, Client, status/owner/priority/dates,
// open tasks, completed tasks, recent activity, and audit history where authorized. This is the
// business-owned output of the project detail query operation. ProjectDetailServiceModel wraps
// the basic ProjectServiceModel with Client summary, task lists, and activity metadata.
// No Controller/Facade code maps into or out of it; ProjectContractMappingExtensions.ToDetailServiceModel
// is the only place that translation happens (backend.md, onion-boundaries.md).
public sealed record ProjectDetailServiceModel
{
    [JsonPropertyName("project")]
    public required ProjectServiceModel Project { get; init; }

    [JsonPropertyName("client")]
    public required ClientSummary Client { get; init; }

    [JsonPropertyName("openTasks")]
    public required IReadOnlyList<ProjectTaskSummary> OpenTasks { get; init; }

    [JsonPropertyName("completedTasks")]
    public required IReadOnlyList<ProjectTaskSummary> CompletedTasks { get; init; }

    [JsonPropertyName("recentActivityCount")]
    public required int RecentActivityCount { get; init; }
}

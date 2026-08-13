using System.Text.Json.Serialization;
using ProjectChicago.Crm.Contracts.Clients;

namespace ProjectChicago.Crm.Contracts.Projects;

// One Task's summary within a Project's detail view (PROJECT-030/PROJECT-031). Mirrors the
// ClientTaskSummary pattern used in Client detail views, allowing navigation and understanding
// of task status within project context.
public sealed record ProjectTaskSummary
{
    [JsonPropertyName("id")]
    public required Guid Id { get; init; }

    [JsonPropertyName("title")]
    public required string Title { get; init; }

    [JsonPropertyName("status")]
    public required TaskItemStatusContract Status { get; init; }

    [JsonPropertyName("priority")]
    public required TaskItemPriorityContract Priority { get; init; }

    [JsonPropertyName("assignedUserId")]
    public string? AssignedUserId { get; init; }

    [JsonPropertyName("dueDateUtc")]
    public DateTime? DueDateUtc { get; init; }

    [JsonPropertyName("completedAtUtc")]
    public DateTime? CompletedAtUtc { get; init; }
}

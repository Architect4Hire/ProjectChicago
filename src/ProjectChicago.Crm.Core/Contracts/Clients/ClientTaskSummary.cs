using System.Text.Json.Serialization;

namespace ProjectChicago.Crm.Contracts.Clients;

// One Task's summary within a Client's detail view (CLIENT-030/CLIENT-032). ProjectId is carried
// so a caller can navigate from a Task shown here to the owning Project (CLIENT-032: "navigate
// from the Client to Tasks belonging to those Projects") even though the Task itself has no direct
// Client reference (DATA-001: Task belongs to Project, which belongs to Client).
public sealed record ClientTaskSummary
{
    [JsonPropertyName("id")]
    public required Guid Id { get; init; }

    [JsonPropertyName("projectId")]
    public required Guid ProjectId { get; init; }

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

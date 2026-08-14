using System.Text.Json.Serialization;
using ProjectChicago.Crm.Contracts.Clients;

namespace ProjectChicago.Crm.Contracts.Tasks;

// Business-owned output of Task creation, and the public response contract for POST /api/projects/{projectId}/tasks
// returned as 201 Created (API-003/API-004; onion-boundaries.md: "Business owns ... translation
// between Facade and Data models"). TaskBusiness builds this directly from the persisted Task
// aggregate - it is never the EF Task entity itself (api-contracts.md; backend.md), and no
// Controller/Facade code maps into or out of it; TaskContractMappingExtensions.ToServiceModel is
// the only place that translation happens.
//
// ConcurrencyToken carries the Task's optimistic-concurrency value (DATA-008; mirrors
// Task.RowVersion) opaquely as an ASCII/base64 string, not a raw byte array, so REST clients can
// round-trip it (e.g. as a future PUT/PATCH If-Match header or request-body token) without a
// binary-encoding decision baked into this contract now.
public sealed record TaskServiceModel
{
    [JsonPropertyName("id")]
    public required Guid Id { get; init; }

    [JsonPropertyName("projectId")]
    public required Guid ProjectId { get; init; }

    [JsonPropertyName("title")]
    public required string Title { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("status")]
    public required TaskItemStatusContract Status { get; init; }

    [JsonPropertyName("priority")]
    public required TaskItemPriorityContract Priority { get; init; }

    [JsonPropertyName("assignedUserId")]
    public string? AssignedUserId { get; init; }

    [JsonPropertyName("startDateUtc")]
    public DateTime? StartDateUtc { get; init; }

    [JsonPropertyName("dueDateUtc")]
    public DateTime? DueDateUtc { get; init; }

    [JsonPropertyName("completedAtUtc")]
    public DateTime? CompletedAtUtc { get; init; }

    [JsonPropertyName("notes")]
    public string? Notes { get; init; }

    [JsonPropertyName("createdAtUtc")]
    public required DateTime CreatedAtUtc { get; init; }

    [JsonPropertyName("createdBy")]
    public required string CreatedBy { get; init; }

    [JsonPropertyName("lastModifiedAtUtc")]
    public required DateTime LastModifiedAtUtc { get; init; }

    [JsonPropertyName("lastModifiedBy")]
    public required string LastModifiedBy { get; init; }

    [JsonPropertyName("concurrencyToken")]
    public required string ConcurrencyToken { get; init; }
}

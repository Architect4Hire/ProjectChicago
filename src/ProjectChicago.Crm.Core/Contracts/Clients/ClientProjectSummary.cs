using System.Text.Json.Serialization;

namespace ProjectChicago.Crm.Contracts.Clients;

// One Project's summary within a Client's detail view (CLIENT-030/CLIENT-031). Carries enough of
// PROJECT-002's fields to display and to navigate to the full Project (Id) - not every Project
// field, since the full Project detail view is a separate, not-yet-built use case (PROJECT-030).
public sealed record ClientProjectSummary
{
    [JsonPropertyName("id")]
    public required Guid Id { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("status")]
    public required ProjectStatusContract Status { get; init; }

    [JsonPropertyName("priority")]
    public required ProjectPriorityContract Priority { get; init; }

    [JsonPropertyName("ownerUserId")]
    public required string OwnerUserId { get; init; }

    [JsonPropertyName("startDateUtc")]
    public DateTime? StartDateUtc { get; init; }

    [JsonPropertyName("targetCompletionDateUtc")]
    public DateTime? TargetCompletionDateUtc { get; init; }

    [JsonPropertyName("actualCompletionDateUtc")]
    public DateTime? ActualCompletionDateUtc { get; init; }

    [JsonPropertyName("lastModifiedAtUtc")]
    public required DateTime LastModifiedAtUtc { get; init; }
}

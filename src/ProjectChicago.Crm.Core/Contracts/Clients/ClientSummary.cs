using System.Text.Json.Serialization;

namespace ProjectChicago.Crm.Contracts.Clients;

// Lightweight Client summary for display within a Project detail view (PROJECT-030).
// Includes essential client identity and status information without full contact details.
// Used when a caller needs to understand the Client context of a Project without
// requesting the full ClientDetailServiceModel.
public sealed record ClientSummary
{
    [JsonPropertyName("id")]
    public required Guid Id { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("lifecycleStatus")]
    public required ClientLifecycleStatusContract LifecycleStatus { get; init; }

    [JsonPropertyName("ownerUserId")]
    public required string OwnerUserId { get; init; }

    [JsonPropertyName("primaryContactName")]
    public string? PrimaryContactName { get; init; }

    [JsonPropertyName("primaryEmail")]
    public string? PrimaryEmail { get; init; }
}

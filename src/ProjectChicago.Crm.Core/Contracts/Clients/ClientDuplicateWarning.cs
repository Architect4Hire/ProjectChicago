using System.Text.Json.Serialization;

namespace ProjectChicago.Crm.Contracts.Clients;

// One likely-duplicate match surfaced by POST /api/clients (CLIENT-004). Duplicate detection warns
// rather than blocks or silently merges, so this rides alongside the created ClientServiceModel instead
// of a separate blocking status code.
public sealed record ClientDuplicateWarning
{
    [JsonPropertyName("clientId")]
    public required Guid ClientId { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("matchedOn")]
    public required IReadOnlyList<ClientDuplicateMatchField> MatchedOn { get; init; }
}

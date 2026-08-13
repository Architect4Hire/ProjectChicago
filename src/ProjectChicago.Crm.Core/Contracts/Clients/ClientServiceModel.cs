using System.Text.Json.Serialization;

namespace ProjectChicago.Crm.Contracts.Clients;

// Business-owned output of Client creation, and the public response contract for POST /api/clients
// returned as 201 Created (API-003/API-004; onion-boundaries.md: "Business owns ... translation
// between Facade and Data models"). ClientBusiness builds this directly from the persisted Client
// aggregate - it is never the EF Client entity itself (api-contracts.md; backend.md), and no
// Controller/Facade code maps into or out of it; ClientContractMappingExtensions.ToServiceModel is
// the only place that translation happens.
//
// ConcurrencyToken carries the Client's optimistic-concurrency value (DATA-008; mirrors
// Client.RowVersion) opaquely as an ASCII/base64 string, not a raw byte array, so REST clients can
// round-trip it (e.g. as a future PUT/PATCH If-Match header or request-body token) without a
// binary-encoding decision baked into this contract now.
//
// PossibleDuplicates surfaces CLIENT-004 warnings without blocking creation or silently merging:
// the Client is always created when validation/authorization succeed, and any likely-duplicate
// matches ride along on the same response for the caller to review.
public sealed record ClientServiceModel
{
    [JsonPropertyName("id")]
    public required Guid Id { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("primaryContactName")]
    public string? PrimaryContactName { get; init; }

    [JsonPropertyName("primaryEmail")]
    public string? PrimaryEmail { get; init; }

    [JsonPropertyName("primaryPhone")]
    public string? PrimaryPhone { get; init; }

    [JsonPropertyName("website")]
    public string? Website { get; init; }

    [JsonPropertyName("addressLine")]
    public string? AddressLine { get; init; }

    [JsonPropertyName("city")]
    public string? City { get; init; }

    [JsonPropertyName("stateOrProvince")]
    public string? StateOrProvince { get; init; }

    [JsonPropertyName("postalCode")]
    public string? PostalCode { get; init; }

    [JsonPropertyName("country")]
    public string? Country { get; init; }

    [JsonPropertyName("lifecycleStatus")]
    public required ClientLifecycleStatusContract LifecycleStatus { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("ownerUserId")]
    public required string OwnerUserId { get; init; }

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

    [JsonPropertyName("possibleDuplicates")]
    public IReadOnlyList<ClientDuplicateWarning> PossibleDuplicates { get; init; } = [];
}

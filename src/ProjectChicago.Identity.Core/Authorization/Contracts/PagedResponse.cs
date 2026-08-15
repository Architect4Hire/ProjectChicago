using System.Text.Json.Serialization;

namespace ProjectChicago.Identity.Core.Authorization.Contracts;

// Collection-response envelope for Administrator user listing endpoint (SEC-004, SEC-010..016).
// Provides paginated results with support-safe user metadata. Follows the pattern established
// by CRM's list endpoints (api-contracts.md: "Collection endpoints use a shared pagination envelope").
public sealed record PagedResponse<TItem>
{
    [JsonPropertyName("items")]
    public required IReadOnlyList<TItem> Items { get; init; }

    [JsonPropertyName("page")]
    public required int Page { get; init; }

    [JsonPropertyName("pageSize")]
    public required int PageSize { get; init; }

    [JsonPropertyName("totalCount")]
    public required int TotalCount { get; init; }

    [JsonPropertyName("totalPages")]
    public required int TotalPages { get; init; }
}

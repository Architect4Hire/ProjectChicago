using System.Text.Json.Serialization;

namespace ProjectChicago.Crm.Contracts.Common;

// Shared collection-response envelope for Project Chicago gateway-visible list endpoints
// (api-contracts.md: "Collection endpoints use a shared pagination envelope and explicit
// sort/filter defaults."). GET api/clients (CLIENT-020..024, API-005) is the first collection
// endpoint contract in the repository, so this envelope is defined here in CRM's own contract
// area rather than pre-emptively promoted into ProjectChicago.Shared - CRM is currently the only
// consumer, and Shared is cross-cutting mechanism only (CLAUDE.md Constraints). If a second
// bounded service needs an identical shape, promote this type then (CLAUDE.md Usage #5 -
// narrowest reversible assumption, not a permanent CRM-only decision).
//
// TotalPages is carried on the wire (rather than left for every client to recompute) so paging
// controls do not each reimplement ceil(TotalCount / PageSize) against a PageSize that could be
// zero.
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

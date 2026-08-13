using System.Text.Json.Serialization;

namespace ProjectChicago.Crm.Contracts.Clients;

// Approved sort attributes for GET api/clients (CLIENT-023 - "commonly used attributes").
// Requirements name Name, Created date, Modified date, and Lifecycle status; no other attribute
// is a valid ListClientsRequest.SortBy value until a superseding requirement adds one.
//
// Stable string enum (api-contracts.md) - add a new member rather than renaming/renumbering an
// existing one. JsonStringEnumConverter is applied for consistency with every other public
// Client contract enum even though this type is query-bound today, not JSON-body-bound; it costs
// nothing now and avoids an inconsistent converter decision if a future response ever echoes the
// applied sort back to the caller.
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ClientSortField
{
    Name = 0,
    CreatedAtUtc = 1,
    LastModifiedAtUtc = 2,
    LifecycleStatus = 3,
}

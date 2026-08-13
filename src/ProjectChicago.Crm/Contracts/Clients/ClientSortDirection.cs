using System.Text.Json.Serialization;

namespace ProjectChicago.Crm.Contracts.Clients;

// Sort direction paired with ClientSortField for GET api/clients (CLIENT-023). Stable string enum
// (api-contracts.md) - add a new member rather than renaming/renumbering an existing one.
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ClientSortDirection
{
    Ascending = 0,
    Descending = 1,
}

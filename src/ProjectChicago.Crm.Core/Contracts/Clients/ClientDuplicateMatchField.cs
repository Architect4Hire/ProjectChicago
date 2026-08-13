using System.Text.Json.Serialization;

namespace ProjectChicago.Crm.Contracts.Clients;

// Which CLIENT-004 duplicate-detection criterion matched an existing Client. Stable string enum
// (api-contracts.md) - add a new member rather than renaming an existing one.
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ClientDuplicateMatchField
{
    Name = 0,
    PrimaryEmail = 1,
    PrimaryPhone = 2,
}

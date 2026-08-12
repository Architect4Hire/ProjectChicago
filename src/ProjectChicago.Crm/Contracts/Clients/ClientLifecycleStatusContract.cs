using System.Text.Json.Serialization;

namespace ProjectChicago.Crm.Contracts.Clients;

// Wire-level mirror of ProjectChicago.Crm.Core.Models.DataModels.Entities.ClientLifecycleStatus
// (CLIENT-010). Kept as a separate contract type rather than reusing the Core enum directly so the
// API host never references a Core persistence-model namespace (api-contracts.md: "Request and
// response DTOs are ... separate from EF entities"; backend.md: "EF entities/data models stay in
// .Core ... and do not leak to the browser"). Mapping between the two lives with the future
// Facade/mapping microstep, not here.
//
// Serializes as its member name (api-contracts.md: "Use stable string enum serialization when
// enums cross HTTP boundaries") - do not renumber or rename an existing member; add a new one
// instead.
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ClientLifecycleStatusContract
{
    Lead = 0,
    Prospect = 1,
    Active = 2,
    OnHold = 3,
    Inactive = 4,
    Archived = 5,
}

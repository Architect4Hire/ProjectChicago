using System.Text.Json.Serialization;

namespace ProjectChicago.Crm.Contracts.Clients;

// Wire-level mirror of ProjectChicago.Crm.Core.Models.DataModels.Entities.ProjectStatus
// (PROJECT-010), used by ClientProjectSummary within the Client detail view (CLIENT-030). Kept as
// a separate contract type for the same reason as ClientLifecycleStatusContract - persistence-
// model detail never leaks onto the wire (api-contracts.md; backend.md). Mapping lives in
// ProjectChicago.Crm.Core.Business.ClientContractMappingExtensions.
//
// Serializes as its member name (api-contracts.md) - do not renumber or rename an existing
// member; add a new one instead.
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ProjectStatusContract
{
    Planned = 0,
    Active = 1,
    OnHold = 2,
    Completed = 3,
    Cancelled = 4,
    Archived = 5,
}

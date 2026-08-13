using System.Text.Json.Serialization;

namespace ProjectChicago.Crm.Contracts.Projects;

// Wire-level mirror of ProjectChicago.Crm.Core.Models.DataModels.Entities.ProjectStatus
// (PROJECT-010). Kept as a separate contract type so persistence-model detail never leaks
// onto the wire (api-contracts.md; backend.md). Mapping lives in
// ProjectChicago.Crm.Core.Business.ProjectContractMappingExtensions.
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

namespace ProjectChicago.Crm.Core.Models.ServiceModels;

// Domain-owned mirror of ProjectChicago.Crm.Contracts.Clients.ClientDuplicateMatchField
// (CLIENT-004). Kept as a separate type so Business's own duplicate-matching decision stays
// independent of the wire enum's serialized shape (api-contracts.md: DTOs are separate from
// domain/persistence models) - ClientContractMappingExtensions translates between the two.
public enum ClientDuplicateMatchField
{
    Name = 0,
    PrimaryEmail = 1,
    PrimaryPhone = 2,
}

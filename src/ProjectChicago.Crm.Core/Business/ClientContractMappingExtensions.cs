using ProjectChicago.Crm.Contracts.Clients;
using ProjectChicago.Crm.Core.Models.DataModels.Entities;
using ProjectChicago.Crm.Core.Models.ServiceModels;
using CoreDuplicateMatchField = ProjectChicago.Crm.Core.Models.ServiceModels.ClientDuplicateMatchField;
using ContractDuplicateMatchField = ProjectChicago.Crm.Contracts.Clients.ClientDuplicateMatchField;

namespace ProjectChicago.Crm.Core.Business;

// Wire-contract <-> domain-model translation for the Client creation use case (CLIENT-001..004;
// onion-boundaries.md: "Business owns ... translation between Facade and Data models"). Lives in
// Business, as extension methods, alongside ClientBusiness - the only caller. ClientFacade never
// touches these; it passes CreateClientViewModel straight through and returns whatever
// IClientBusiness.CreateAsync hands back, so ClientsController stays transport-only too.
public static class ClientContractMappingExtensions
{
    public static ClientLifecycleStatus ToCoreLifecycleStatus(this ClientLifecycleStatusContract status) => status switch
    {
        ClientLifecycleStatusContract.Lead => ClientLifecycleStatus.Lead,
        ClientLifecycleStatusContract.Prospect => ClientLifecycleStatus.Prospect,
        ClientLifecycleStatusContract.Active => ClientLifecycleStatus.Active,
        ClientLifecycleStatusContract.OnHold => ClientLifecycleStatus.OnHold,
        ClientLifecycleStatusContract.Inactive => ClientLifecycleStatus.Inactive,
        ClientLifecycleStatusContract.Archived => ClientLifecycleStatus.Archived,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unmapped ClientLifecycleStatusContract value."),
    };

    private static ClientLifecycleStatusContract ToContractLifecycleStatus(this ClientLifecycleStatus status) => status switch
    {
        ClientLifecycleStatus.Lead => ClientLifecycleStatusContract.Lead,
        ClientLifecycleStatus.Prospect => ClientLifecycleStatusContract.Prospect,
        ClientLifecycleStatus.Active => ClientLifecycleStatusContract.Active,
        ClientLifecycleStatus.OnHold => ClientLifecycleStatusContract.OnHold,
        ClientLifecycleStatus.Inactive => ClientLifecycleStatusContract.Inactive,
        ClientLifecycleStatus.Archived => ClientLifecycleStatusContract.Archived,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unmapped ClientLifecycleStatus value."),
    };

    // The single exit-point mapping ClientBusiness.CreateAsync calls once the Client is persisted -
    // builds the public ClientServiceModel straight from the domain aggregate plus the CLIENT-004
    // duplicate candidates, so no other layer ever reads Client fields directly.
    public static ClientServiceModel ToServiceModel(this Client client, IReadOnlyList<ClientDuplicateCandidate> possibleDuplicates) => new()
    {
        Id = client.Id,
        Name = client.Name,
        PrimaryContactName = client.PrimaryContactName,
        PrimaryEmail = client.PrimaryEmail,
        PrimaryPhone = client.PrimaryPhone,
        Website = client.Website,
        AddressLine = client.AddressLine,
        City = client.City,
        StateOrProvince = client.StateOrProvince,
        PostalCode = client.PostalCode,
        Country = client.Country,
        LifecycleStatus = client.LifecycleStatus.ToContractLifecycleStatus(),
        Description = client.Description,
        OwnerUserId = client.OwnerUserId,
        CreatedAtUtc = client.CreatedAtUtc,
        CreatedBy = client.CreatedBy,
        LastModifiedAtUtc = client.LastModifiedAtUtc,
        LastModifiedBy = client.LastModifiedBy,
        ConcurrencyToken = Convert.ToBase64String(client.RowVersion),
        PossibleDuplicates = possibleDuplicates.Select(ToDuplicateWarning).ToList(),
    };

    private static ClientDuplicateWarning ToDuplicateWarning(this ClientDuplicateCandidate candidate) => new()
    {
        ClientId = candidate.ClientId,
        Name = candidate.Name,
        MatchedOn = candidate.MatchedOn.Select(ToContractMatchField).ToList(),
    };

    private static ContractDuplicateMatchField ToContractMatchField(CoreDuplicateMatchField matchField) => matchField switch
    {
        CoreDuplicateMatchField.Name => ContractDuplicateMatchField.Name,
        CoreDuplicateMatchField.PrimaryEmail => ContractDuplicateMatchField.PrimaryEmail,
        CoreDuplicateMatchField.PrimaryPhone => ContractDuplicateMatchField.PrimaryPhone,
        _ => throw new ArgumentOutOfRangeException(nameof(matchField), matchField, "Unmapped ClientDuplicateMatchField value."),
    };
}

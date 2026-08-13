using ProjectChicago.Crm.Contracts.Clients;
using ProjectChicago.Crm.Core.Models.DataModels.Entities;
using ProjectChicago.Crm.Core.Models.ServiceModels;
using ProjectChicago.Shared.Correlation;
using CoreDuplicateMatchField = ProjectChicago.Crm.Core.Models.ServiceModels.ClientDuplicateMatchField;
using ContractDuplicateMatchField = ProjectChicago.Crm.Contracts.Clients.ClientDuplicateMatchField;

namespace ProjectChicago.Crm.Core.Business;

// Wire-contract <-> Business-model translation for the Client creation use case (CLIENT-001..004;
// onion-boundaries.md: "Business owns ... translation between Facade and Data models"). Lives in
// Business, as extension methods, so ClientsController stays transport-only (binds the request,
// calls one Facade method, maps the result to ActionResult<T> - no field-by-field mapping of its
// own) and ClientFacade stays a thin validate/authorize/delegate seam. ClientBusiness's own
// CreateAsync(CreateClientCommand) is untouched by this file and keeps testing domain rules against
// plain Business models, independent of the wire shape.
public static class ClientContractMappingExtensions
{
    public static CreateClientCommand ToCommand(
        this CreateClientRequest request,
        ActorContext actor,
        RequestContext requestContext,
        DateTime createdAtUtc) => new()
    {
        Name = request.Name,
        PrimaryContactName = request.PrimaryContactName,
        PrimaryEmail = request.PrimaryEmail,
        PrimaryPhone = request.PrimaryPhone,
        Website = request.Website,
        AddressLine = request.AddressLine,
        City = request.City,
        StateOrProvince = request.StateOrProvince,
        PostalCode = request.PostalCode,
        Country = request.Country,
        LifecycleStatus = request.LifecycleStatus is { } status ? status.ToCoreLifecycleStatus() : null,
        Description = request.Description,
        OwnerUserId = request.OwnerUserId,
        Actor = actor,
        RequestContext = requestContext,
        CreatedAtUtc = createdAtUtc,
    };

    public static ClientResponse ToResponse(this ClientCreationResult result)
    {
        var client = result.Client;

        return new ClientResponse
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
            PossibleDuplicates = result.PossibleDuplicates.Select(ToDuplicateWarning).ToList(),
        };
    }

    private static ClientDuplicateWarning ToDuplicateWarning(this ClientDuplicateCandidate candidate) => new()
    {
        ClientId = candidate.ClientId,
        Name = candidate.Name,
        MatchedOn = candidate.MatchedOn.Select(ToContractMatchField).ToList(),
    };

    private static ClientLifecycleStatus ToCoreLifecycleStatus(this ClientLifecycleStatusContract status) => status switch
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

    private static ContractDuplicateMatchField ToContractMatchField(CoreDuplicateMatchField matchField) => matchField switch
    {
        CoreDuplicateMatchField.Name => ContractDuplicateMatchField.Name,
        CoreDuplicateMatchField.PrimaryEmail => ContractDuplicateMatchField.PrimaryEmail,
        CoreDuplicateMatchField.PrimaryPhone => ContractDuplicateMatchField.PrimaryPhone,
        _ => throw new ArgumentOutOfRangeException(nameof(matchField), matchField, "Unmapped ClientDuplicateMatchField value."),
    };
}

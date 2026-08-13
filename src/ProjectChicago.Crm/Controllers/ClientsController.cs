using Microsoft.AspNetCore.Mvc;
using ProjectChicago.Crm.Contracts.Clients;
using ProjectChicago.Crm.Core.Facades;
using CoreLifecycleStatus = ProjectChicago.Crm.Core.Models.DataModels.Entities.ClientLifecycleStatus;
using CoreDuplicateCandidate = ProjectChicago.Crm.Core.Models.ServiceModels.ClientDuplicateCandidate;
using CoreDuplicateMatchField = ProjectChicago.Crm.Core.Models.ServiceModels.ClientDuplicateMatchField;
using CoreCreateClientRequest = ProjectChicago.Crm.Core.Models.ServiceModels.CreateClientRequest;
using CoreClientCreationResult = ProjectChicago.Crm.Core.Models.ServiceModels.ClientCreationResult;

namespace ProjectChicago.Crm.Controllers;

// POST /api/clients (CLIENT-001..004, API-001..007, SEC-010..013, ERROR-001..005). Transport-only:
// binds the wire request, applies the coarse "is there an authenticated actor at all" check
// documented by ClientsApiContract's 401 case, calls the single IClientFacade use case, and maps its
// typed result/exception to the standard HTTP/ProblemDetails shape (onion-boundaries.md; add-endpoint
// skill step 3). Fine-grained SEC-012/013 policy authorization ("Clients.Write") and CLIENT-002/004
// business rules live in Facade/Business - this controller injects no Business/Data/Repository/
// DbContext and never publishes directly (RESTRICTION).
[ApiController]
[Route(ClientsApiContract.Route)]
public sealed class ClientsController : ControllerBase
{
    private readonly IClientFacade _clientFacade;

    public ClientsController(IClientFacade clientFacade)
    {
        _clientFacade = clientFacade ?? throw new ArgumentNullException(nameof(clientFacade));
    }

    // Unauthenticated (401) vs unauthorized (403) are deliberately distinct per ClientsApiContract:
    // this coarse check (is there any authenticated actor at all) stays in the controller as plain
    // ASP.NET Core ClaimsPrincipal inspection - not a call into IClientFacade/IClientAuthorization -
    // so it never depends on the still-open ADR-0018 authentication-transport decision. The narrower
    // "does this actor hold Clients.Write" policy check happens in Facade/IClientAuthorization and
    // surfaces here only as an UnauthorizedAccessException the already-registered ApiExceptionHandler
    // classifies into 403.
    [HttpPost(Name = ClientsApiContract.CreateOperationId)]
    [ProducesResponseType(typeof(ClientResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ClientResponse>> Create(
        [FromBody] CreateClientRequest request,
        CancellationToken cancellationToken)
    {
        if (User.Identity is not { IsAuthenticated: true })
        {
            return Unauthorized();
        }

        var result = await _clientFacade.CreateAsync(ToCoreRequest(request), cancellationToken).ConfigureAwait(false);

        var response = ToResponse(result);

        return Created(new Uri($"{ClientsApiContract.Route}/{response.Id}", UriKind.Relative), response);
    }

    private static CoreCreateClientRequest ToCoreRequest(CreateClientRequest request) => new()
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
        LifecycleStatus = request.LifecycleStatus is { } status ? ToCoreLifecycleStatus(status) : null,
        Description = request.Description,
        OwnerUserId = request.OwnerUserId,
    };

    private static ClientResponse ToResponse(CoreClientCreationResult result)
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
            LifecycleStatus = ToContractLifecycleStatus(client.LifecycleStatus),
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

    private static ClientDuplicateWarning ToDuplicateWarning(CoreDuplicateCandidate candidate) => new()
    {
        ClientId = candidate.ClientId,
        Name = candidate.Name,
        MatchedOn = candidate.MatchedOn.Select(ToContractMatchField).ToList(),
    };

    private static CoreLifecycleStatus ToCoreLifecycleStatus(ClientLifecycleStatusContract status) => status switch
    {
        ClientLifecycleStatusContract.Lead => CoreLifecycleStatus.Lead,
        ClientLifecycleStatusContract.Prospect => CoreLifecycleStatus.Prospect,
        ClientLifecycleStatusContract.Active => CoreLifecycleStatus.Active,
        ClientLifecycleStatusContract.OnHold => CoreLifecycleStatus.OnHold,
        ClientLifecycleStatusContract.Inactive => CoreLifecycleStatus.Inactive,
        ClientLifecycleStatusContract.Archived => CoreLifecycleStatus.Archived,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unmapped ClientLifecycleStatusContract value."),
    };

    private static ClientLifecycleStatusContract ToContractLifecycleStatus(CoreLifecycleStatus status) => status switch
    {
        CoreLifecycleStatus.Lead => ClientLifecycleStatusContract.Lead,
        CoreLifecycleStatus.Prospect => ClientLifecycleStatusContract.Prospect,
        CoreLifecycleStatus.Active => ClientLifecycleStatusContract.Active,
        CoreLifecycleStatus.OnHold => ClientLifecycleStatusContract.OnHold,
        CoreLifecycleStatus.Inactive => ClientLifecycleStatusContract.Inactive,
        CoreLifecycleStatus.Archived => ClientLifecycleStatusContract.Archived,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unmapped ClientLifecycleStatus value."),
    };

    private static ClientDuplicateMatchField ToContractMatchField(CoreDuplicateMatchField matchField) => matchField switch
    {
        CoreDuplicateMatchField.Name => ClientDuplicateMatchField.Name,
        CoreDuplicateMatchField.PrimaryEmail => ClientDuplicateMatchField.PrimaryEmail,
        CoreDuplicateMatchField.PrimaryPhone => ClientDuplicateMatchField.PrimaryPhone,
        _ => throw new ArgumentOutOfRangeException(nameof(matchField), matchField, "Unmapped ClientDuplicateMatchField value."),
    };
}

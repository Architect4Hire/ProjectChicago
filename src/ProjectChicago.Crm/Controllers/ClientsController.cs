using Microsoft.AspNetCore.Mvc;
using ProjectChicago.Crm.Contracts.Clients;
using ProjectChicago.Crm.Core.Facades;

namespace ProjectChicago.Crm.Controllers;

// POST /api/clients (CLIENT-001..004, API-001..007, SEC-010..013, ERROR-001..005). Transport-only:
// binds the wire request, applies the coarse "is there an authenticated actor at all" check
// documented by ClientsApiContract's 401 case, calls the single IClientFacade use case, and maps its
// typed result/exception to the standard HTTP/ProblemDetails shape (onion-boundaries.md; add-endpoint
// skill step 3). No field-by-field request/response mapping lives here - IClientFacade accepts and
// returns the wire contract types directly, and ClientContractMappingExtensions (Business) owns the
// translation to/from Business models. Fine-grained SEC-012/013 policy authorization ("Clients.Write")
// and CLIENT-002/004 business rules live in Facade/Business - this controller injects no
// Business/Data/Repository/DbContext and never publishes directly (RESTRICTION).
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

        var response = await _clientFacade.CreateAsync(request, cancellationToken).ConfigureAwait(false);

        return Created(new Uri($"{ClientsApiContract.Route}/{response.Id}", UriKind.Relative), response);
    }
}

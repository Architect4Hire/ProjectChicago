using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectChicago.Crm.Contracts.Common;
using ProjectChicago.Crm.Contracts.Projects;
using ProjectChicago.Crm.Core.Data;
using ProjectChicago.Crm.Core.Facades;
using ProjectChicago.ServiceDefaults.Correlation;
using ProjectChicago.ServiceDefaults.Filters;
using ProjectChicago.Shared.Errors;

namespace ProjectChicago.Crm.Controllers;

/// <summary>
/// CRM Projects resource endpoints (PROJECT-001..002, API-006/007, SEC-010..013).
/// Transport-only: binds requests, delegates to IProjectFacade, maps results to HTTP/ProblemDetails.
/// </summary>
[ApiController]
[Route("")]
[RequireAuthentication]
public sealed class ProjectsController : ControllerBase
{
    private readonly IProjectFacade _projectFacade;

    public ProjectsController(IProjectFacade projectFacade)
    {
        _projectFacade = projectFacade ?? throw new ArgumentNullException(nameof(projectFacade));
    }

    /// <summary>
    /// Create a new project for a client. Requires Projects.Write authorization (SEC-010..013).
    /// Returns 400 if client does not exist.
    /// </summary>
    /// <param name="clientId">Client ID</param>
    /// <param name="request">Project creation data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <response code="201">Project created</response>
    /// <response code="400">Validation error or client not found</response>
    /// <response code="401">Not authenticated</response>
    /// <response code="403">Not authorized (requires Projects.Write)</response>
    [Route("api/clients/{clientId}/projects")]
    [HttpPost(Name = ProjectsApiContract.CreateOperationId)]
    [Authorize(Policy = "Projects.Write")]
    [ProducesResponseType(typeof(ProjectServiceModel), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ProjectServiceModel>> Create(
        Guid clientId,
        [FromBody] CreateProjectViewModel request,
        CancellationToken cancellationToken)
    {

        try
        {
            // CreateProjectViewModel already carries the clientId from the request body (for symmetry
            // with the wire shape), so this route parameter is redundant verification - the Facade will
            // use request.ClientId. If the two differ, that is a client/binding error; the MVC binding
            // layer will have rejected an invalid route Guid, so they are never silently mismatched here.
            var response = await _projectFacade.CreateAsync(request, cancellationToken).ConfigureAwait(false);

            return Created(new Uri($"api/projects/{response.Id}", UriKind.Relative), response);
        }
        catch (ProjectClientNotFoundException ex)
        {
            var requestContext = HttpRequestContextFactory.Create(HttpContext);
            var problem = ApiProblemDetailsFactory.Validation(
                requestContext,
                fieldErrors: new Dictionary<string, string[]>
                {
                    [nameof(CreateProjectViewModel.ClientId)] = [ex.Message],
                });

            return BadRequest(problem);
        }
    }

    // GET /api/projects (PROJECT-020..023, API-001..007, SEC-010..013, ERROR-001..005).
    // Transport-only: binds the wire query request, applies the coarse "is there an authenticated
    // actor at all" check, calls the single IProjectFacade use case, and maps its typed result/exception
    // to the standard HTTP/ProblemDetails shape (onion-boundaries.md; add-endpoint skill step 3). No
    // field-by-field request/response mapping lives here - IProjectFacade accepts and returns the wire
    // contract types directly, and ProjectContractMappingExtensions (Business) owns the translation
    // to/from Business models. Fine-grained SEC-012/013 policy authorization ("Projects.Read") and
    // PROJECT-020..023 business rules live in Facade/Business - this controller injects no
    // Business/Data/Repository/DbContext and never publishes directly (RESTRICTION).
    [Route("api/projects")]
    [HttpGet(Name = ProjectsApiContract.ListOperationId)]
    [Authorize(Policy = "Projects.Read")]
    [ProducesResponseType(typeof(PagedResponse<ProjectServiceModel>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResponse<ProjectServiceModel>>> List(
        [FromQuery] ListProjectsRequest request,
        CancellationToken cancellationToken)
    {
        if (User.Identity is not { IsAuthenticated: true })
        {
            return Unauthorized();
        }

        // PROJECT-020..023: the Facade validates the request, checks authorization for
        // Projects.Read capability, and delegates to IProjectBusiness for filter translation,
        // retrieval, and mapping into PagedResponse<ProjectServiceModel>.
        var response = await _projectFacade.ListAsync(request, cancellationToken).ConfigureAwait(false);

        return Ok(response);
    }

    // GET /api/projects/{projectId} (PROJECT-030..031, API-001..007, SEC-010..013, ERROR-001..005).
    // Transport-only: binds the route projectId, applies the coarse "is there an authenticated
    // actor at all" check, calls the single IProjectFacade use case, and maps its typed result/exception
    // to the standard HTTP/ProblemDetails shape (onion-boundaries.md; add-endpoint skill step 3). No
    // field-by-field request/response mapping lives here - IProjectFacade accepts and returns the wire
    // contract types directly, and ProjectContractMappingExtensions (Business) owns the translation
    // to/from Business models. Fine-grained SEC-012/013 policy authorization ("Projects.Read") and
    // PROJECT-030 business rules live in Facade/Business - this controller injects no
    // Business/Data/Repository/DbContext and never publishes directly (RESTRICTION).
    [Route("api/projects/{projectId}")]
    [HttpGet(Name = ProjectsApiContract.DetailOperationId)]
    [Authorize(Policy = "Projects.Read")]
    [ProducesResponseType(typeof(ProjectDetailServiceModel), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ProjectDetailServiceModel>> GetDetail(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        if (User.Identity is not { IsAuthenticated: true })
        {
            return Unauthorized();
        }

        // PROJECT-030: the Facade checks authorization for Projects.Read capability, validates
        // the projectId, and delegates to IProjectBusiness for retrieval and mapping into
        // ProjectDetailServiceModel. Returns 404 when the Project does not exist.
        var response = await _projectFacade.GetDetailAsync(projectId, cancellationToken).ConfigureAwait(false);

        return response is null ? NotFound() : Ok(response);
    }

    // PATCH /api/projects/{projectId}/status (PROJECT-010..014, API-001..007, SEC-012..013,
    // DATA-008, ERROR-001..005). Transport-only: binds the route projectId and wire request,
    // applies the coarse "is there an authenticated actor at all" check, calls the single
    // IProjectFacade use case, and maps its typed result/exception to the standard HTTP/ProblemDetails
    // shape (onion-boundaries.md; add-endpoint skill step 3). No field-by-field request/response
    // mapping lives here - IProjectFacade accepts and returns the wire contract types directly,
    // and ProjectContractMappingExtensions (Business) owns the translation to/from Business models.
    // Fine-grained SEC-012/013 policy authorization ("Projects.Write"), PROJECT-010..014 transition
    // validation, PROJECT-012 completion timestamp, PROJECT-013 open-task acknowledgement, and
    // DATA-008 concurrency conflict detection all live in Facade/Business - this controller injects
    // no Business/Data/Repository/DbContext and never publishes directly (RESTRICTION).
    [Route("api/projects/{projectId}/status")]
    [HttpPatch(Name = ProjectsApiContract.TransitionStatusOperationId)]
    [Authorize(Policy = "Projects.Write")]
    [ProducesResponseType(typeof(ProjectServiceModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProjectServiceModel>> TransitionStatus(
        Guid projectId,
        [FromBody] ChangeProjectStatusViewModel request,
        CancellationToken cancellationToken)
    {
        if (User.Identity is not { IsAuthenticated: true })
        {
            return Unauthorized();
        }

        try
        {
            // PROJECT-010..014: the Facade validates the request, checks authorization for
            // Projects.Write capability, validates the projectId, enforces PROJECT-013
            // open-task acknowledgement and PROJECT-012 completion timestamp rules,
            // and delegates to IProjectBusiness for transition validation, persistence, and mapping
            // into ProjectServiceModel. Returns 404 when the Project does not exist.
            var response = await _projectFacade.TransitionStatusAsync(projectId, request, cancellationToken)
                .ConfigureAwait(false);

            return response is null ? NotFound() : Ok(response);
        }
        catch (ProjectConcurrencyConflictException)
        {
            // DATA-008: optimistic concurrency conflict (RowVersion mismatch). The client supplied
            // an expectedConcurrencyToken that no longer matches the current Project state.
            var requestContext = HttpRequestContextFactory.Create(HttpContext);
            return Conflict(ApiProblemDetailsFactory.ConcurrencyConflict(requestContext));
        }
        catch (InvalidOperationException ex)
        {
            // PROJECT-010..013: status transition invalid (disallowed state change or
            // unacknowledged open Tasks when completing).
            var requestContext = HttpRequestContextFactory.Create(HttpContext);
            var problem = ApiProblemDetailsFactory.Validation(
                requestContext,
                fieldErrors: new Dictionary<string, string[]>
                {
                    [nameof(ChangeProjectStatusViewModel.NewStatus)] = [ex.Message],
                });

            return BadRequest(problem);
        }
    }

    // DELETE /api/projects/{projectId}/archive (PROJECT-014, API-001..007, SEC-012..013,
    // DATA-008, ERROR-001..005). Transport-only: binds the route projectId and wire request,
    // applies the coarse "is there an authenticated actor at all" check, calls the single
    // IProjectFacade use case, and maps its typed result/exception to the standard HTTP/ProblemDetails
    // shape (onion-boundaries.md; add-endpoint skill step 3). No field-by-field request/response
    // mapping lives here - IProjectFacade accepts and returns the wire contract types directly,
    // and ProjectContractMappingExtensions (Business) owns the translation to/from Business models.
    // Fine-grained SEC-012/013 policy authorization ("Projects.Write") and DATA-008 concurrency
    // conflict detection all live in Facade/Business - this controller injects no Business/Data/
    // Repository/DbContext and never publishes directly (RESTRICTION).
    [Route("api/projects/{projectId}/archive")]
    [HttpDelete(Name = ProjectsApiContract.ArchiveOperationId)]
    [Authorize(Policy = "Projects.Write")]
    [ProducesResponseType(typeof(ProjectServiceModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProjectServiceModel>> Archive(
        Guid projectId,
        [FromBody] ArchiveProjectViewModel request,
        CancellationToken cancellationToken)
    {
        if (User.Identity is not { IsAuthenticated: true })
        {
            return Unauthorized();
        }

        try
        {
            // PROJECT-014: the Facade validates the request, checks authorization for
            // Projects.Write capability, validates the projectId, and delegates to
            // IProjectBusiness for persistence and mapping into ProjectServiceModel.
            // Returns 404 when the Project does not exist.
            var response = await _projectFacade.ArchiveAsync(projectId, request, cancellationToken)
                .ConfigureAwait(false);

            return response is null ? NotFound() : Ok(response);
        }
        catch (ProjectConcurrencyConflictException)
        {
            // DATA-008: optimistic concurrency conflict (RowVersion mismatch). The client supplied
            // an expectedConcurrencyToken that no longer matches the current Project state.
            var requestContext = HttpRequestContextFactory.Create(HttpContext);
            return Conflict(ApiProblemDetailsFactory.ConcurrencyConflict(requestContext));
        }
        catch (InvalidOperationException ex)
        {
            // PROJECT-014: archive operation rejected by business rules.
            var requestContext = HttpRequestContextFactory.Create(HttpContext);
            return Conflict(ApiProblemDetailsFactory.ConcurrencyConflict(requestContext, ex.Message));
        }
    }
}

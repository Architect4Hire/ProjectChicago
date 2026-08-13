using System.ComponentModel.DataAnnotations;
using ProjectChicago.Crm.Contracts.Common;
using ProjectChicago.Crm.Contracts.Projects;
using ProjectChicago.Crm.Core.Business;
using ProjectChicago.Shared.Correlation;

namespace ProjectChicago.Crm.Core.Facades;

// IProjectFacade implementation for Project operations (PROJECT-001..002, PROJECT-020..023,
// SEC-010..013; onion-boundaries.md, backend.md). Owns exactly: resolving the acting
// user/correlation context, the SEC-012/013 authorization checks, and contextual request
// validation, before delegating to IProjectBusiness for defaults, persistence, retrieval, and
// the wire<->domain mapping. No EF, cache, HttpContext, or Service Bus dependency - those belong
// to Data/Repository, the HTTP host, and the outbox relay respectively (RESTRICTION: Facade does
// not access Data/EF). This Facade never maps contract fields itself - methods only resolve the
// actor/context/timestamp a caller must never supply itself and hand them, plus the untouched
// contract, to IProjectBusiness - and return its Business result straight back to the Controller.
public sealed class ProjectFacade : IProjectFacade
{
    private readonly IProjectBusiness _projectBusiness;
    private readonly IProjectAuthorization _authorization;
    private readonly ICurrentRequestContext _currentRequestContext;
    private readonly IClock _clock;

    public ProjectFacade(
        IProjectBusiness projectBusiness,
        IProjectAuthorization authorization,
        ICurrentRequestContext currentRequestContext,
        IClock clock)
    {
        _projectBusiness = projectBusiness ?? throw new ArgumentNullException(nameof(projectBusiness));
        _authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
        _currentRequestContext = currentRequestContext ?? throw new ArgumentNullException(nameof(currentRequestContext));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<ProjectServiceModel> CreateAsync(CreateProjectViewModel request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var requestContext = _currentRequestContext.Current;

        // SEC-013: authorization is verified before any validation/business work changes or even
        // evaluates request data. PROJECT-001: authorization is scoped to the Client - the actor
        // must be authorized for the specific Client to which the Project is being added.
        var authorized = await _authorization.CanCreateAsync(requestContext.Actor, request.ClientId, cancellationToken)
            .ConfigureAwait(false);
        if (!authorized)
        {
            throw new UnauthorizedAccessException(
                "The current actor is not authorized to create a Project for the specified Client (Projects.Write).");
        }

        Validate(request);

        // PROJECT-001..002: the Facade does not translate/map CreateProjectViewModel itself - it
        // only authorizes and validates before handing the untouched request to
        // IProjectBusiness.CreateAsync for status/priority defaults, persistence, and mapping into
        // ProjectServiceModel.
        return await _projectBusiness.CreateAsync(
            request, requestContext.Actor, requestContext, _clock.UtcNow, cancellationToken).ConfigureAwait(false);
    }

    public async Task<PagedResponse<ProjectServiceModel>> ListAsync(
        ListProjectsRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var requestContext = _currentRequestContext.Current;

        // SEC-013: authorization is verified before any validation/business work. PROJECT-020:
        // authorization is checked for the Projects.Read capability - a caller can list Projects
        // they are authorized to read across all Clients they have access to.
        var authorized = await _authorization.CanListAsync(requestContext.Actor, cancellationToken)
            .ConfigureAwait(false);
        if (!authorized)
        {
            throw new UnauthorizedAccessException(
                "The current actor is not authorized to list Projects (Projects.Read).");
        }

        Validate(request);

        // PROJECT-020..023: the Facade only authorizes and validates before handing the untouched
        // request to IProjectBusiness.ListAsync for filter translation, retrieval, and mapping
        // into PagedResponse<ProjectServiceModel>.
        return await _projectBusiness.ListAsync(request, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ProjectDetailServiceModel?> GetDetailAsync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var requestContext = _currentRequestContext.Current;

        // SEC-013: authorization is verified before any retrieval. PROJECT-030: authorization is
        // checked for the Projects.Read capability.
        var authorized = await _authorization.CanListAsync(requestContext.Actor, cancellationToken)
            .ConfigureAwait(false);
        if (!authorized)
        {
            throw new UnauthorizedAccessException(
                "The current actor is not authorized to view Project details (Projects.Read).");
        }

        // PROJECT-030: the Facade validates the identifier before delegation, then returns whatever
        // IProjectBusiness.GetDetailAsync provides (null or the full detail model).
        if (projectId == Guid.Empty)
        {
            throw new ArgumentException("Project Id cannot be empty.", nameof(projectId));
        }

        return await _projectBusiness.GetDetailAsync(projectId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ProjectServiceModel?> TransitionStatusAsync(
        Guid projectId,
        ChangeProjectStatusViewModel request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var requestContext = _currentRequestContext.Current;

        // SEC-013: authorization is verified before any validation/business work. PROJECT-010..014:
        // authorization is checked for the Projects.Write capability.
        var authorized = await _authorization.CanCreateAsync(requestContext.Actor, Guid.Empty, cancellationToken)
            .ConfigureAwait(false);
        if (!authorized)
        {
            throw new UnauthorizedAccessException(
                "The current actor is not authorized to transition Project status (Projects.Write).");
        }

        // PROJECT-010..014: the Facade validates the request before handing the untouched
        // request to IProjectBusiness.TransitionStatusAsync for transition validation,
        // open-task acknowledgement enforcement (PROJECT-013), completion timestamp capture
        // (PROJECT-012), persistence, and mapping into ProjectServiceModel.
        if (projectId == Guid.Empty)
        {
            throw new ArgumentException("Project Id cannot be empty.", nameof(projectId));
        }

        Validate(request);

        return await _projectBusiness.TransitionStatusAsync(
            projectId,
            request.NewStatus,
            request.ExpectedConcurrencyToken,
            requestContext.Actor,
            requestContext,
            _clock.UtcNow,
            acknowledgeOpenTasks: request.AcknowledgeOpenTasks,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<ProjectServiceModel?> ArchiveAsync(
        Guid projectId,
        ArchiveProjectViewModel request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var requestContext = _currentRequestContext.Current;

        // SEC-013: authorization is verified before any validation/business work. PROJECT-014:
        // authorization is checked for the Projects.Write capability.
        var authorized = await _authorization.CanCreateAsync(requestContext.Actor, Guid.Empty, cancellationToken)
            .ConfigureAwait(false);
        if (!authorized)
        {
            throw new UnauthorizedAccessException(
                "The current actor is not authorized to archive Projects (Projects.Write).");
        }

        // PROJECT-014: the Facade validates the request before handing the untouched
        // request to IProjectBusiness.ArchiveAsync for persistence and mapping into ProjectServiceModel.
        if (projectId == Guid.Empty)
        {
            throw new ArgumentException("Project Id cannot be empty.", nameof(projectId));
        }

        Validate(request);

        return await _projectBusiness.ArchiveAsync(
            projectId,
            request.ExpectedConcurrencyToken,
            requestContext.Actor,
            requestContext,
            _clock.UtcNow,
            cancellationToken).ConfigureAwait(false);
    }

    private static void Validate(CreateProjectViewModel request) => Validate((object)request);

    private static void Validate(ChangeProjectStatusViewModel request) => Validate((object)request);

    private static void Validate(ArchiveProjectViewModel request) => Validate((object)request);

    private static void Validate(object request)
    {
        var validationContext = new ValidationContext(request);
        var results = new List<ValidationResult>();

        if (!Validator.TryValidateObject(request, validationContext, results, validateAllProperties: true))
        {
            var message = string.Join("; ", results.Select(result => result.ErrorMessage));
            throw new ValidationException(
                string.IsNullOrWhiteSpace(message) ? "Project request failed validation." : message);
        }
    }
}

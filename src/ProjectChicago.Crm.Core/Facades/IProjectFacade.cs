using ProjectChicago.Crm.Contracts.Common;
using ProjectChicago.Crm.Contracts.Projects;

namespace ProjectChicago.Crm.Core.Facades;

// Public application/use-case seam for Project operations (PROJECT-001..002, PROJECT-020..023,
// SEC-010..013; onion-boundaries.md, backend.md). Accepts wire contracts directly and returns
// wire contracts directly - Business owns the entire contract<->domain<->contract translation
// (ProjectContractMappingExtensions), so this Facade only resolves/supplies the actor/correlation
// context and applies authorization/validation before delegating to IProjectBusiness.
public interface IProjectFacade
{
    // Resolves the acting user/correlation context, applies SEC-012/013 authorization check
    // (Projects.Write), and PROJECT-001/002 contextual request validation, before delegating to
    // IProjectBusiness for creating the Project with status/priority defaults, persisting, and
    // mapping into the public ProjectServiceModel. Throws UnauthorizedAccessException when the
    // actor is not authorized to create a Project for the given Client, and ProjectClientNotFoundException
    // (from Business/Data layers) when the Client does not exist.
    Task<ProjectServiceModel> CreateAsync(CreateProjectViewModel request, CancellationToken cancellationToken);

    // Applies SEC-012/013 authorization check (Projects.Read) and PROJECT-020..023 contextual request
    // validation (bounded page/page size, only-defined sort/filter/status values), before delegating
    // to IProjectBusiness for filter translation, retrieval, and mapping. Throws UnauthorizedAccessException
    // when the resolved actor lacks the Projects.Read policy, or System.ComponentModel.DataAnnotations.
    // ValidationException when request fails validation - both already classified by ApiExceptionHandler
    // into the 403/400 ProblemDetails shape (ERROR-003).
    Task<PagedResponse<ProjectServiceModel>> ListAsync(ListProjectsRequest request, CancellationToken cancellationToken);

    // Resolves the acting user/correlation context, applies SEC-012/013 authorization check
    // (Projects.Read), and validates the projectId parameter, before delegating to IProjectBusiness
    // for retrieval and mapping. Returns null when the Project does not exist (allowing the
    // controller to return 404). Throws UnauthorizedAccessException when the actor lacks
    // Projects.Read policy. A future fine-grained authorization check (whether the caller can
    // access this specific Project) is not implemented in this microstep - all authorized callers
    // can see any Project (PROJECT-030).
    Task<ProjectDetailServiceModel?> GetDetailAsync(Guid projectId, CancellationToken cancellationToken);

    // Resolves the acting user/correlation context, applies SEC-012/013 authorization check
    // (Projects.Write), and validates the request before delegating to IProjectBusiness for status
    // transition, completion timestamp capture (PROJECT-012), open-task acknowledgement enforcement
    // (PROJECT-013), and mutation + audit persistence. Returns the updated ProjectServiceModel, or
    // null if the Project does not exist (allowing the controller to return 404). Throws
    // UnauthorizedAccessException when the actor lacks Projects.Write policy,
    // System.ComponentModel.DataAnnotations.ValidationException when request fails validation,
    // InvalidOperationException when the status transition is invalid or open-task acknowledgement
    // is missing (PROJECT-013), or when the expectedConcurrencyToken is stale (DATA-008, 409 Conflict).
    Task<ProjectServiceModel?> TransitionStatusAsync(
        Guid projectId,
        ChangeProjectStatusViewModel request,
        CancellationToken cancellationToken);
}

using ProjectChicago.Crm.Contracts.Common;
using ProjectChicago.Crm.Contracts.Projects;
using ProjectChicago.Shared.Correlation;

namespace ProjectChicago.Crm.Core.Business;

// Business-layer Project use-case seams (PROJECT-001..002, PROJECT-020..023, AUDIT-001..003; backend.md,
// onion-boundaries.md). Accepts wire contracts directly and returns wire contracts directly - Business
// owns the entire contract<->domain<->contract translation (ProjectContractMappingExtensions), so Facade
// only resolves/supplies context a caller must never set itself and ProjectsController does no mapping at all.
public interface IProjectBusiness
{
    // Normalizes business values, assigns identity and the initial status/priority defaults,
    // verifies that the Client exists (DATA-002), builds the AUDIT-001..003 audit fact, persists
    // both the Project and the audit fact through the single IProjectData seam, and maps the result
    // into a ProjectServiceModel.
    Task<ProjectServiceModel> CreateAsync(
        CreateProjectViewModel request,
        ActorContext actor,
        RequestContext requestContext,
        DateTime createdAtUtc,
        CancellationToken cancellationToken);

    // Translates the wire ListProjectsRequest into repository-facing filter values (resolving
    // SortBy/SortDirection defaults and the Status wire<->domain translation - PROJECT-020..023),
    // retrieves one page through IProjectData, and maps the result into the wire
    // PagedResponse<ProjectServiceModel>. Page/PageSize on the returned envelope always mirror what
    // the caller requested, not what IProjectData happened to return.
    Task<PagedResponse<ProjectServiceModel>> ListAsync(
        ListProjectsRequest request,
        CancellationToken cancellationToken);

    // Retrieves the Project detail composite (PROJECT-030) through IProjectData and maps the
    // result into ProjectDetailServiceModel, including Client summary, open/completed tasks, and
    // activity metadata. Returns null if the Project does not exist (404/403 determination is
    // the Facade's authorization responsibility).
    Task<ProjectDetailServiceModel?> GetDetailAsync(
        Guid projectId,
        CancellationToken cancellationToken);

    // Validates transition rules and state, records completion timestamp when moving to Completed
    // (PROJECT-012..013), persists the Project mutation + audit fact through IProjectData, and
    // returns the updated ProjectServiceModel. Throws InvalidOperationException for invalid
    // transitions (e.g. already Completed, unacknowledged open Tasks). Returns null if Project
    // does not exist.
    Task<ProjectServiceModel?> TransitionStatusAsync(
        Guid projectId,
        ProjectStatusContract targetStatus,
        string expectedConcurrencyToken,
        ActorContext actor,
        RequestContext requestContext,
        DateTime transitionedAtUtc,
        bool acknowledgeOpenTasks = false,
        CancellationToken cancellationToken = default);

    // Archives a Project (PROJECT-014), persisting the mutation + audit fact atomically. Only
    // Completed or Cancelled Projects can be archived. Returns the updated ProjectServiceModel
    // or null if Project does not exist.
    Task<ProjectServiceModel?> ArchiveAsync(
        Guid projectId,
        string expectedConcurrencyToken,
        ActorContext actor,
        RequestContext requestContext,
        DateTime archivedAtUtc,
        CancellationToken cancellationToken);
}

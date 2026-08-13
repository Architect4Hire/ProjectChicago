using ProjectChicago.Crm.Core.Models.DataModels.Entities;

namespace ProjectChicago.Crm.Core.Repositories;

public interface IProjectRepository
{
    // Stages a Project for insert into the database. The caller (Data layer) is responsible for
    // calling SaveChangesAsync on the DbContext to commit the insert.
    Task InsertAsync(Project project, CancellationToken cancellationToken);

    // Returns true when a Client with the given Id exists in the Crm database (DATA-002:
    // "A Project shall not exist without a Client"). Used by ProjectData.CreateAsync to validate
    // that the Project's ClientId references an existing Client before persisting (DATA-005).
    Task<bool> ClientExistsAsync(Guid clientId, CancellationToken cancellationToken);

    // Returns one bounded, sorted page of Projects matching filter plus the total matching count
    // (PROJECT-020..023, PERF-001..004). This is the only query-shaping this repository does; page
    // bounds, default sort, and Status-vs-wire-contract translation are resolved by the caller
    // before filter reaches here (see ProjectListFilter).
    Task<ProjectListResult> ListAsync(ProjectListFilter filter, CancellationToken cancellationToken);

    // Returns the Project detail composite including the Project, its owning Client, open and
    // completed TaskItems, and a count of recent audit events (PROJECT-030). Returns null when
    // the Project does not exist or is not accessible to the caller (404/403 determination is
    // the Facade's authorization responsibility).
    Task<ProjectDetailResult?> GetDetailAsync(Guid projectId, CancellationToken cancellationToken);

    // Returns a single Project by Id or null if not found. Used by status-transition and archive
    // operations that need to load and mutate the full aggregate before persisting.
    Task<Project?> GetAsync(Guid projectId, CancellationToken cancellationToken);
}

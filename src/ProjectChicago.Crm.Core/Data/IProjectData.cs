using ProjectChicago.Contracts.Audit;
using ProjectChicago.Crm.Core.Models.DataModels.Entities;
using ProjectChicago.Crm.Core.Repositories;

namespace ProjectChicago.Crm.Core.Data;

// Data-layer seam for Project operations (PROJECT-001..002, PROJECT-020..023, DATA-001..005, AUDIT-001..008,
// OUTBOX-001/002; backend.md, messaging.md, ADR-0016). For mutations, Business has already validated
// input and decided the audit fact; this seam verifies Client existence, persists atomically, and
// handles transactions. For queries, this is a thin passthrough to IProjectRepository.
public interface IProjectData
{
    // Verifies that the Client referenced by project.ClientId exists (DATA-002/DATA-005), then
    // persists both the project and auditFact in the same database transaction, or neither is.
    // Throws ProjectClientNotFoundException if the Client does not exist. Throws
    // DbUpdateException (or subtype) when the database operation fails.
    Task CreateAsync(Project project, EntityMutationAudited auditFact, CancellationToken cancellationToken);

    // Returns one bounded, sorted page of Projects plus the total matching count (PROJECT-020..023).
    // Thin passthrough to IProjectRepository.ListAsync - Business has already translated the wire
    // ListProjectsRequest into filter, resolving defaults/bounds/Status translation before it reaches
    // this seam (onion-boundaries.md: "Business owns ... translation between Facade and Data models").
    Task<ProjectListResult> ListAsync(ProjectListFilter filter, CancellationToken cancellationToken);

    // Returns the Project detail composite (PROJECT-030) including the Project, its owning Client,
    // open and completed TaskItems, and a count of recent audit events, or null if the Project
    // does not exist. Thin passthrough to IProjectRepository.GetDetailAsync.
    Task<ProjectDetailResult?> GetDetailAsync(Guid projectId, CancellationToken cancellationToken);

    // Retrieves a single Project by Id or null if not found. Used by status-transition and archive
    // operations that need the full entity before mutation (backend.md, onion-boundaries.md).
    Task<Project?> GetAsync(Guid projectId, CancellationToken cancellationToken);

    // Transitions a Project's status (PROJECT-010..013). Business has already validated the
    // transition rules; Data transitions the aggregate, persists + audit/outbox atomically, and
    // enforces optimistic concurrency (DATA-008). Throws DbUpdateConcurrencyException if the
    // concurrency token mismatches (optimistic concurrency conflict).
    Task TransitionStatusAsync(
        Project project,
        ProjectStatus newStatus,
        string modifiedBy,
        DateTime modifiedAtUtc,
        DateTime? completionTimestampUtc,
        string expectedConcurrencyToken,
        EntityMutationAudited auditFact,
        CancellationToken cancellationToken);

    // Archives a Project (PROJECT-014). Business has already validated that the Project is in
    // Completed or Cancelled status. Data archives the aggregate, persists + audit/outbox atomically,
    // and enforces optimistic concurrency (DATA-008). Throws DbUpdateConcurrencyException if the
    // concurrency token mismatches.
    Task ArchiveAsync(
        Project project,
        string modifiedBy,
        DateTime modifiedAtUtc,
        string expectedConcurrencyToken,
        EntityMutationAudited auditFact,
        CancellationToken cancellationToken);

    // Edits a Project's ordinary detail fields (PROJECT-002, DATA-008). Business has already
    // normalized input and decided which fields changed; Data persists the mutation + audit fact
    // atomically and enforces optimistic concurrency (DATA-008). Throws DbUpdateConcurrencyException
    // if the concurrency token mismatches.
    Task EditAsync(
        Project project,
        string modifiedBy,
        DateTime modifiedAtUtc,
        string expectedConcurrencyToken,
        EntityMutationAudited auditFact,
        CancellationToken cancellationToken);
}

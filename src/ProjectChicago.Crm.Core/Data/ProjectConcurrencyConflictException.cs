namespace ProjectChicago.Crm.Core.Data;

// DATA-008's typed Data-layer outcome for a Project concurrency conflict (data.md: "Translate known
// unique/concurrency/foreign-key database failures into typed Data outcomes for Business to
// interpret"). Thrown either immediately - when a caller-supplied expectedConcurrencyToken does not
// match the Project's currently persisted RowVersion - or from a DbUpdateConcurrencyException raised
// by SaveChangesAsync when a concurrent write wins the race between that read and this save. Mapping
// this into an HTTP 409 (ApiProblemDetailsFactory.ConcurrencyConflict) is a future Controller/API
// concern; Data only classifies the failure.
public sealed class ProjectConcurrencyConflictException : Exception
{
    public ProjectConcurrencyConflictException(Guid projectId)
        : base($"Project '{projectId}' was changed by another request; the supplied concurrency token is stale.")
    {
        ProjectId = projectId;
    }

    public ProjectConcurrencyConflictException(Guid projectId, Exception innerException)
        : base($"Project '{projectId}' was changed by another request; the supplied concurrency token is stale.", innerException)
    {
        ProjectId = projectId;
    }

    public Guid ProjectId { get; }
}

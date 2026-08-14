namespace ProjectChicago.Crm.Core.Data;

// DATA-003/DATA-005's typed Data-layer outcome for a Task creation attempt referencing a
// non-existent Project (data.md: "Translate known unique/concurrency/foreign-key database failures
// into typed Data outcomes for Business to interpret"). Thrown when TaskData.CreateAsync verifies
// that the Project exists before persisting the Task. Mapping this into an HTTP 404 or 400 is a
// future Controller/API concern; Data only classifies the failure.
public sealed class TaskProjectNotFoundException : Exception
{
    public TaskProjectNotFoundException(Guid projectId)
        : base($"Project '{projectId}' does not exist; a Task cannot be created without an existing Project (DATA-003).")
    {
        ProjectId = projectId;
    }

    public Guid ProjectId { get; }
}

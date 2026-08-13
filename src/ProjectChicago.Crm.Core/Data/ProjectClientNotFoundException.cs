namespace ProjectChicago.Crm.Core.Data;

// DATA-002/DATA-005's typed Data-layer outcome for a Project creation attempt referencing a
// non-existent Client (data.md: "Translate known unique/concurrency/foreign-key database failures
// into typed Data outcomes for Business to interpret"). Thrown when ProjectData.CreateAsync verifies
// that the Client exists before persisting the Project. Mapping this into an HTTP 404 or 400 is a
// future Controller/API concern; Data only classifies the failure.
public sealed class ProjectClientNotFoundException : Exception
{
    public ProjectClientNotFoundException(Guid clientId)
        : base($"Client '{clientId}' does not exist; a Project cannot be created without an existing Client (DATA-002).")
    {
        ClientId = clientId;
    }

    public Guid ClientId { get; }
}

namespace ProjectChicago.Crm.Core.Data;

// DATA-008's typed Data-layer outcome for a Client concurrency conflict (data.md: "Translate known
// unique/concurrency/foreign-key database failures into typed Data outcomes for Business to
// interpret"). Thrown either immediately - when a caller-supplied expectedConcurrencyToken does not
// match the Client's currently persisted RowVersion - or from a DbUpdateConcurrencyException raised
// by SaveChangesAsync when a concurrent write wins the race between that read and this save. Mapping
// this into an HTTP 409 (ApiProblemDetailsFactory.ConcurrencyConflict) is a future Controller/API
// concern; Data only classifies the failure.
public sealed class ClientConcurrencyConflictException : Exception
{
    public ClientConcurrencyConflictException(Guid clientId)
        : base($"Client '{clientId}' was changed by another request; the supplied concurrency token is stale.")
    {
        ClientId = clientId;
    }

    public ClientConcurrencyConflictException(Guid clientId, Exception innerException)
        : base($"Client '{clientId}' was changed by another request; the supplied concurrency token is stale.", innerException)
    {
        ClientId = clientId;
    }

    public Guid ClientId { get; }
}

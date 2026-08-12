using ProjectChicago.Crm.Core.Models.DataModels.Entities;

namespace ProjectChicago.Crm.Core.Repositories;

// Persistence-operation seam for Client (CLIENT-001/CLIENT-004, DATA-004/DATA-005; backend.md
// Repository responsibilities). Scoped to exactly the two operations this microstep needs: staging
// an insert and reading duplicate-detection candidates. Transaction composition, duplicate policy,
// and outbox writes belong to the Data layer, not here.
public interface IClientRepository
{
    // Stages the Client for insert on the owning CrmDbContext without calling SaveChangesAsync -
    // committing the write (alongside any outbox row the same use case must write atomically) is
    // the Data layer's responsibility (backend.md; database.md Transactions).
    Task InsertAsync(Client client, CancellationToken cancellationToken);

    // Returns materialized Client rows whose Name, PrimaryEmail, or PrimaryPhone matches one of the
    // supplied already-normalized values (CLIENT-004). A null/whitespace candidate value is not
    // matched against. This method only retrieves candidates; deciding what counts as a duplicate is
    // a Business-layer policy decision.
    Task<IReadOnlyList<Client>> FindDuplicateCandidatesAsync(
        string? normalizedName,
        string? normalizedEmail,
        string? normalizedPhone,
        CancellationToken cancellationToken);
}

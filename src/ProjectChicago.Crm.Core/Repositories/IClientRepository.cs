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

    // Returns the tracked Client for an in-place lifecycle-status update (CLIENT-010..015,
    // DATA-008), or null when no Client with the requested Id exists. Unlike GetDetailAsync/
    // ListAsync this deliberately does not use AsNoTracking - the caller (ClientData) mutates the
    // returned instance in place and later saves the same DbContext's tracked change, so EF's own
    // optimistic-concurrency check (comparing the RowVersion this read captured against what is
    // still in the database at save time) applies automatically.
    Task<Client?> GetForUpdateAsync(Guid clientId, CancellationToken cancellationToken);

    // Returns materialized Client rows whose Name, PrimaryEmail, or PrimaryPhone matches one of the
    // supplied already-normalized values (CLIENT-004). A null/whitespace candidate value is not
    // matched against. This method only retrieves candidates; deciding what counts as a duplicate is
    // a Business-layer policy decision.
    Task<IReadOnlyList<Client>> FindDuplicateCandidatesAsync(
        string? normalizedName,
        string? normalizedEmail,
        string? normalizedPhone,
        CancellationToken cancellationToken);

    // Returns one bounded, sorted page of Clients matching filter plus the total matching count
    // (CLIENT-020..024, PERF-001..004). This is the only query-shaping this repository does; page
    // bounds, default sort, and LifecycleStatus-vs-wire-contract translation are resolved by the
    // caller before filter reaches here (see ClientListFilter).
    Task<ClientListResult> ListAsync(ClientListFilter filter, CancellationToken cancellationToken);

    // Returns the consolidated Client detail view (CLIENT-030..032, PERF-001..004): the Client
    // itself plus its Projects split into active/historical and its Projects' Tasks split into
    // open/recently-completed, each bounded and sorted at the query. Returns null when no Client
    // with the requested Id exists - unlike ListAsync, this read is never subject to CLIENT-013's
    // archived-exclusion default, because a caller navigating to one specific Client by Id has
    // already identified the record it wants to see, archived or not (DATA-021: archived records
    // remain available for historical relationships).
    Task<ClientDetailQueryResult?> GetDetailAsync(Guid clientId, CancellationToken cancellationToken);

    // Returns true when the Client with the given Id has one or more active Projects
    // (CLIENT-015: archival restriction). Active is determined by ProjectStatus matching one of
    // the same Planned/Active/OnHold statuses used in GetDetailAsync's summary.
    Task<bool> HasActiveProjectsAsync(Guid clientId, CancellationToken cancellationToken);
}

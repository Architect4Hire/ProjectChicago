using ProjectChicago.Contracts.Audit;
using ProjectChicago.Crm.Core.Models.DataModels.Entities;
using ProjectChicago.Crm.Core.Repositories;

namespace ProjectChicago.Crm.Core.Data;

// Data-layer seam for the Client create transaction (CLIENT-001..004, AUDIT-001..008,
// OUTBOX-001/002; backend.md, messaging.md, ADR-0016). Business has already validated input,
// resolved duplicate-warning policy, and decided the audit fact; this seam only persists what it is
// given, atomically.
public interface IClientData
{
    // client is the fully-constructed, already-validated aggregate; auditFact is the one
    // EntityMutationAudited fact Business decided to emit for the Created mutation (AUDIT-003).
    // Both are persisted in the same database transaction, or neither is.
    Task CreateAsync(Client client, EntityMutationAudited auditFact, CancellationToken cancellationToken);

    // Loads the tracked Client for a lifecycle-status transition (CLIENT-010..015) and verifies
    // expectedConcurrencyToken (the caller's last-known Client.ConcurrencyToken) matches the
    // Client's currently persisted RowVersion, rejecting a caller acting on stale data before
    // Business ever evaluates the transition rules (DATA-008). Returns null when no Client with
    // the requested Id exists. Throws ClientConcurrencyConflictException when
    // expectedConcurrencyToken does not match.
    Task<Client?> GetForLifecycleChangeAsync(
        Guid clientId, string expectedConcurrencyToken, CancellationToken cancellationToken);

    // Persists the Client instance GetForLifecycleChangeAsync returned - Business has already
    // called Client.ChangeLifecycleStatus on it - plus the one EntityMutationAudited fact Business
    // decided to emit for the StatusChanged mutation (AUDIT-001..003), atomically with the same
    // CrmDbContext/SaveChangesAsync call CreateAsync uses (OUTBOX-001/002). Throws
    // ClientConcurrencyConflictException if a concurrent write reached the database between the
    // GetForLifecycleChangeAsync read and this save (DATA-008).
    Task SaveLifecycleChangeAsync(Client client, EntityMutationAudited auditFact, CancellationToken cancellationToken);

    // Returns existing Clients whose Name/PrimaryEmail/PrimaryPhone matches one of the supplied,
    // already-normalized values (CLIENT-004). This is a read, not part of the create transaction -
    // Business calls it before building the new Client so the candidate does not match itself.
    // Business decides what counts as a duplicate warning; this seam only retrieves candidates so
    // Business never depends on IClientRepository directly (onion-boundaries.md: "Business depends
    // only on Data interfaces").
    Task<IReadOnlyList<Client>> FindDuplicateCandidatesAsync(
        string? normalizedName,
        string? normalizedEmail,
        string? normalizedPhone,
        CancellationToken cancellationToken);

    // Returns one bounded, sorted page of Clients plus the total matching count (CLIENT-020..024).
    // Thin passthrough to IClientRepository.ListAsync - Business has already translated the wire
    // ListClientsRequest into filter, resolving defaults/bounds/LifecycleStatus translation before
    // it reaches this seam (onion-boundaries.md: "Business owns ... translation between Facade and
    // Data models").
    Task<ClientListResult> ListAsync(ClientListFilter filter, CancellationToken cancellationToken);

    // Returns the consolidated Client detail view (CLIENT-030..032), or null when no Client with
    // the requested Id exists. Thin passthrough to IClientRepository.GetDetailAsync - this is a
    // pure read with no transaction/outbox composition, so Data adds nothing beyond keeping
    // Business repository-agnostic (onion-boundaries.md).
    Task<ClientDetailQueryResult?> GetDetailAsync(Guid clientId, CancellationToken cancellationToken);

    // Loads the tracked Client for archive (CLIENT-013..015) and verifies expectedConcurrencyToken
    // (the caller's last-known Client.RowVersion) matches the Client's currently persisted value,
    // rejecting a caller acting on stale data before Business ever applies the archive operation
    // (DATA-008). Returns null when no Client with the requested Id exists. Throws
    // ClientConcurrencyConflictException when expectedConcurrencyToken does not match.
    Task<Client?> GetForArchiveAsync(
        Guid clientId, string expectedConcurrencyToken, CancellationToken cancellationToken);

    // Persists the Client instance GetForArchiveAsync returned - Business has already called
    // Client.ChangeLifecycleStatus to transition to Archived - plus the one EntityMutationAudited
    // fact Business decided to emit for the Archived mutation (AUDIT-001..003), atomically with the
    // same CrmDbContext/SaveChangesAsync call other mutations use (OUTBOX-001/002). Throws
    // ClientConcurrencyConflictException if a concurrent write reached the database between
    // GetForArchiveAsync's read and this save (DATA-008).
    Task SaveArchiveAsync(Client client, EntityMutationAudited auditFact, CancellationToken cancellationToken);

    // Loads the tracked Client for restore (CLIENT-013..015) and verifies expectedConcurrencyToken
    // (the caller's last-known Client.RowVersion) matches the Client's currently persisted value,
    // rejecting a caller acting on stale data before Business ever applies the restore operation
    // (DATA-008). Returns null when no Client with the requested Id exists. Throws
    // ClientConcurrencyConflictException when expectedConcurrencyToken does not match.
    Task<Client?> GetForRestoreAsync(
        Guid clientId, string expectedConcurrencyToken, CancellationToken cancellationToken);

    // Persists the Client instance GetForRestoreAsync returned - Business has already called
    // Client.ChangeLifecycleStatus to transition from Archived to a new status - plus the one
    // EntityMutationAudited fact Business decided to emit for the Restored mutation (AUDIT-001..003),
    // atomically with the same CrmDbContext/SaveChangesAsync call other mutations use (OUTBOX-001/002).
    // Throws ClientConcurrencyConflictException if a concurrent write reached the database between
    // GetForRestoreAsync's read and this save (DATA-008).
    Task SaveRestoreAsync(Client client, EntityMutationAudited auditFact, CancellationToken cancellationToken);

    // Returns true when the Client with the given Id has one or more active Projects
    // (CLIENT-015: archival restriction). Thin passthrough to IClientRepository.HasActiveProjectsAsync -
    // this is a read with no transaction composition, so Data adds nothing beyond keeping Business
    // repository-agnostic (onion-boundaries.md).
    Task<bool> HasActiveProjectsAsync(Guid clientId, CancellationToken cancellationToken);

    // Loads the tracked Client for profile update (CLIENT-002) and verifies expectedConcurrencyToken
    // (the caller's last-known Client.RowVersion) matches the Client's currently persisted value,
    // rejecting a caller acting on stale data before Business ever applies the update (DATA-008).
    // Returns null when no Client with the requested Id exists. Throws ClientConcurrencyConflictException
    // when expectedConcurrencyToken does not match.
    Task<Client?> GetForUpdateAsync(
        Guid clientId, string expectedConcurrencyToken, CancellationToken cancellationToken);

    // Persists the Client instance GetForUpdateAsync returned - Business has already called
    // Client.UpdateProfile on it - plus the one EntityMutationAudited fact Business decided to emit
    // for the Updated mutation (AUDIT-001..003), atomically with the same CrmDbContext/SaveChangesAsync
    // call other mutations use (OUTBOX-001/002). Throws ClientConcurrencyConflictException if a
    // concurrent write reached the database between GetForUpdateAsync's read and this save (DATA-008).
    Task SaveUpdateAsync(Client client, EntityMutationAudited auditFact, CancellationToken cancellationToken);
}

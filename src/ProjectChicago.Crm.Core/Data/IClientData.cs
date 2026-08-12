using ProjectChicago.Contracts.Audit;
using ProjectChicago.Crm.Core.Models.DataModels.Entities;

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
}

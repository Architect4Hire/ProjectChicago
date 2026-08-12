using ProjectChicago.Contracts.Audit;
using ProjectChicago.Crm.Core.Models.DataModels.Entities;
using ProjectChicago.Crm.Core.Persistence;
using ProjectChicago.Crm.Core.Repositories;
using ProjectChicago.Shared.Messaging;
using ProjectChicago.Shared.Outbox;

namespace ProjectChicago.Crm.Core.Data;

// SQL Server-backed IClientData (CLIENT-001..004, AUDIT-001..008, OUTBOX-001/002; backend.md,
// messaging.md, ADR-0016). Stages the Client insert (via ClientRepository) and one OutboxMessage
// row derived from the prepared EntityMutationAudited fact on the same CrmDbContext, then commits
// both with a single SaveChangesAsync call - EF Core wraps every staged change in one database
// transaction, so a failure on either side rolls back both (database.md Transactions: "Domain state
// + outbox record commit in one database transaction"). This type does not validate the Client,
// decide duplicate-warning policy, decide lifecycle rules, or talk to Service Bus - the relay
// Function dispatches the row later (messaging.md).
public sealed class ClientData : IClientData
{
    // Matches the ContractType convention already established for this contract elsewhere in the
    // codebase (see ProjectChicago.Shared.Tests) - "Audit." prefix plus the CLR record name.
    private const string AuditContractType = "Audit.EntityMutationAudited";

    private readonly CrmDbContext _dbContext;
    private readonly IClientRepository _clientRepository;

    public ClientData(CrmDbContext dbContext, IClientRepository clientRepository)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _clientRepository = clientRepository ?? throw new ArgumentNullException(nameof(clientRepository));
    }

    public async Task CreateAsync(Client client, EntityMutationAudited auditFact, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(auditFact);

        await _clientRepository.InsertAsync(client, cancellationToken).ConfigureAwait(false);
        _dbContext.OutboxMessages.Add(BuildOutboxMessage(auditFact));

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    // Thin passthrough to ClientRepository - no transaction/policy work belongs here, this exists
    // only so Business can stay repository-agnostic (onion-boundaries.md).
    public Task<IReadOnlyList<Client>> FindDuplicateCandidatesAsync(
        string? normalizedName,
        string? normalizedEmail,
        string? normalizedPhone,
        CancellationToken cancellationToken) =>
        _clientRepository.FindDuplicateCandidatesAsync(normalizedName, normalizedEmail, normalizedPhone, cancellationToken);

    private static OutboxMessage BuildOutboxMessage(EntityMutationAudited auditFact)
    {
        // The fact's own EventId becomes both the outbox row's identity and, later, the Service Bus
        // native MessageId the relay sends (OutboxRelay.ToOutboundMessage uses OutboxMessage.Id) - the
        // same value Audit's inbox uses for idempotency, exactly as EntityMutationAudited.EventId
        // documents (OUTBOX-004, ASYNC-005).
        var id = ParseEventId(auditFact.EventId);

        var envelope = new EventEnvelope<EntityMutationAudited>
        {
            EventId = auditFact.EventId,
            ContractType = AuditContractType,
            ContractVersion = auditFact.Version,
            OccurredAtUtc = auditFact.OccurredAtUtc,
            CorrelationId = auditFact.CorrelationId,
            CausationId = auditFact.CausationId,
            TraceId = auditFact.TraceId,
            Payload = auditFact,
        };

        return new OutboxMessage
        {
            Id = id,
            ContractType = AuditContractType,
            ContractVersion = auditFact.Version,
            Payload = EventEnvelopeSerializer.Serialize(envelope),
            CorrelationId = auditFact.CorrelationId,
            CausationId = auditFact.CausationId,
            TraceId = auditFact.TraceId,
            OccurredAtUtc = auditFact.OccurredAtUtc.UtcDateTime,
            CreatedAtUtc = DateTime.UtcNow,
        };
    }

    private static Guid ParseEventId(string eventId) =>
        Guid.TryParse(eventId, out var id)
            ? id
            : throw new ArgumentException(
                $"EntityMutationAudited.EventId '{eventId}' must be a GUID - it becomes the OutboxMessage.Id and the Service Bus native MessageId used for Audit inbox idempotency (OUTBOX-004, ASYNC-005).",
                nameof(eventId));
}

using Microsoft.EntityFrameworkCore;
using ProjectChicago.Contracts.Audit;
using ProjectChicago.Crm.Core.Models.DataModels.Entities;
using ProjectChicago.Crm.Core.Persistence;
using ProjectChicago.Crm.Core.Repositories;
using ProjectChicago.Shared.Messaging;
using ProjectChicago.Shared.Outbox;

namespace ProjectChicago.Crm.Core.Data;

// SQL Server-backed IProjectData (PROJECT-001..002, PROJECT-020..023, DATA-001..005, AUDIT-001..008,
// OUTBOX-001/002; backend.md, messaging.md, ADR-0016). For mutations: Verifies the Project's Client
// exists, stages the Project insert (via ProjectRepository), and one OutboxMessage row derived from
// the prepared EntityMutationAudited fact on the same CrmDbContext, then commits both with a single
// SaveChangesAsync call - EF Core wraps every staged change in one database transaction, so a failure
// on either side rolls back both (database.md Transactions: "Domain state + outbox record commit in
// one database transaction"). For queries: thin passthrough to repository. This type does not
// validate the Project, decide business rules, or talk to Service Bus - the relay Function dispatches
// the row later (messaging.md).
public sealed class ProjectData : IProjectData
{
    // Matches the ContractType convention already established for this contract elsewhere in the
    // codebase (see ProjectChicago.Shared.Tests) - "Audit." prefix plus the CLR record name.
    private const string AuditContractType = "Audit.EntityMutationAudited";

    private readonly CrmDbContext _dbContext;
    private readonly IProjectRepository _projectRepository;

    public ProjectData(CrmDbContext dbContext, IProjectRepository projectRepository)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _projectRepository = projectRepository ?? throw new ArgumentNullException(nameof(projectRepository));
    }

    public async Task CreateAsync(Project project, EntityMutationAudited auditFact, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(auditFact);

        // DATA-002/DATA-005: verify the Client exists before creating the Project.
        if (!await _projectRepository.ClientExistsAsync(project.ClientId, cancellationToken).ConfigureAwait(false))
        {
            throw new ProjectClientNotFoundException(project.ClientId);
        }

        await _projectRepository.InsertAsync(project, cancellationToken).ConfigureAwait(false);
        _dbContext.OutboxMessages.Add(BuildOutboxMessage(auditFact));

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task<ProjectListResult> ListAsync(ProjectListFilter filter, CancellationToken cancellationToken) =>
        _projectRepository.ListAsync(filter, cancellationToken);

    public Task<ProjectDetailResult?> GetDetailAsync(Guid projectId, CancellationToken cancellationToken) =>
        _projectRepository.GetDetailAsync(projectId, cancellationToken);

    public Task<Project?> GetAsync(Guid projectId, CancellationToken cancellationToken) =>
        _projectRepository.GetAsync(projectId, cancellationToken);

    public async Task TransitionStatusAsync(
        Project project,
        ProjectStatus newStatus,
        string modifiedBy,
        DateTime modifiedAtUtc,
        DateTime? completionTimestampUtc,
        string expectedConcurrencyToken,
        EntityMutationAudited auditFact,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(expectedConcurrencyToken);
        ArgumentNullException.ThrowIfNull(auditFact);

        project.TransitionStatus(newStatus, modifiedBy, modifiedAtUtc, completionTimestampUtc);

        // EF Core's optimistic concurrency for rowversion: we pass the expected token as an array
        // into the context's entry to let EF know the original value for comparison on SaveChangesAsync.
        var concurrencyToken = Convert.FromBase64String(expectedConcurrencyToken);
        _dbContext.Entry(project).OriginalValues[nameof(Project.RowVersion)] = concurrencyToken;

        _dbContext.Projects.Update(project);
        _dbContext.OutboxMessages.Add(BuildOutboxMessage(auditFact));

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // DATA-008: a concurrent write reached the database between this method's caller's read
            // and this save - the same conflict manifests as a stale expectedConcurrencyToken, translated
            // the same way for Business.
            throw new ProjectConcurrencyConflictException(project.Id, ex);
        }
    }

    public async Task ArchiveAsync(
        Project project,
        string modifiedBy,
        DateTime modifiedAtUtc,
        string expectedConcurrencyToken,
        EntityMutationAudited auditFact,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(expectedConcurrencyToken);
        ArgumentNullException.ThrowIfNull(auditFact);

        project.Archive(modifiedBy, modifiedAtUtc);

        // EF Core's optimistic concurrency for rowversion: we pass the expected token as an array
        // into the context's entry to let EF know the original value for comparison on SaveChangesAsync.
        var concurrencyToken = Convert.FromBase64String(expectedConcurrencyToken);
        _dbContext.Entry(project).OriginalValues[nameof(Project.RowVersion)] = concurrencyToken;

        _dbContext.Projects.Update(project);
        _dbContext.OutboxMessages.Add(BuildOutboxMessage(auditFact));

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // DATA-008: a concurrent write reached the database between this method's caller's read
            // and this save - the same conflict manifests as a stale expectedConcurrencyToken, translated
            // the same way for Business.
            throw new ProjectConcurrencyConflictException(project.Id, ex);
        }
    }

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

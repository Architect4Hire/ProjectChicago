using ProjectChicago.Contracts.Audit;
using ProjectChicago.Crm.Core.Models.DataModels.Entities;
using ProjectChicago.Crm.Core.Persistence;
using ProjectChicago.Crm.Core.Repositories;
using ProjectChicago.Shared.Messaging;
using ProjectChicago.Shared.Outbox;

namespace ProjectChicago.Crm.Core.Data;

// SQL Server-backed ITaskData (TASK-001..022, DATA-001..005, AUDIT-001..008, OUTBOX-001/002;
// backend.md, messaging.md, ADR-0016). For mutations: Verifies the Task's Project exists,
// stages the Task insert (via TaskRepository), and one OutboxMessage row derived from the
// prepared EntityMutationAudited fact on the same CrmDbContext, then commits both with a
// single SaveChangesAsync call - EF Core wraps every staged change in one database
// transaction, so a failure on either side rolls back both (database.md Transactions:
// "Domain state + outbox record commit in one database transaction"). This type does not
// validate the Task, decide business rules, or talk to Service Bus - the relay Function
// dispatches the row later (messaging.md).
public sealed class TaskData : ITaskData
{
    // Matches the ContractType convention already established for this contract elsewhere in
    // the codebase (see ProjectChicago.Shared.Tests) - "Audit." prefix plus the CLR record name.
    private const string AuditContractType = "Audit.EntityMutationAudited";

    private readonly CrmDbContext _dbContext;
    private readonly ITaskRepository _taskRepository;

    public TaskData(CrmDbContext dbContext, ITaskRepository taskRepository)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _taskRepository = taskRepository ?? throw new ArgumentNullException(nameof(taskRepository));
    }

    public async Task CreateAsync(TaskItem task, EntityMutationAudited auditFact, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(auditFact);

        // DATA-003/DATA-005: verify the Project exists before creating the Task.
        if (!await _taskRepository.ProjectExistsAsync(task.ProjectId, cancellationToken).ConfigureAwait(false))
        {
            throw new TaskProjectNotFoundException(task.ProjectId);
        }

        await _taskRepository.InsertAsync(task, cancellationToken).ConfigureAwait(false);
        _dbContext.OutboxMessages.Add(BuildOutboxMessage(auditFact));

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task<TaskItem?> GetByIdAsync(Guid taskId, CancellationToken cancellationToken) =>
        _taskRepository.GetByIdAsync(taskId, cancellationToken);

    public async Task AssignAsync(TaskItem task, EntityMutationAudited auditFact, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(auditFact);

        // Task has already been mutated by Business via SetAssigned/SetReassigned. Attach it to the
        // DbContext so EF Core can track the changes and check RowVersion concurrency (DATA-008).
        // The tracked entity will be updated on SaveChangesAsync; if RowVersion has changed since
        // fetch, DbUpdateConcurrencyException is thrown (optimistic locking).
        _dbContext.Tasks.Update(task);
        _dbContext.OutboxMessages.Add(BuildOutboxMessage(auditFact));

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task ChangePriorityAsync(TaskItem task, EntityMutationAudited auditFact, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(auditFact);

        // Task has already been mutated by Business via SetPriority. Attach it to the
        // DbContext so EF Core can track the changes and check RowVersion concurrency (DATA-008).
        // The tracked entity will be updated on SaveChangesAsync; if RowVersion has changed since
        // fetch, DbUpdateConcurrencyException is thrown (optimistic locking).
        _dbContext.Tasks.Update(task);
        _dbContext.OutboxMessages.Add(BuildOutboxMessage(auditFact));

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task<TaskListResult> ListAsync(TaskListFilter filter, CancellationToken cancellationToken) =>
        _taskRepository.ListAsync(filter, cancellationToken);

    public async Task ChangeStatusAsync(TaskItem task, EntityMutationAudited auditFact, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(auditFact);

        // Task has already been mutated by Business via SetStatus. Attach it to the DbContext
        // so EF Core can track the changes and check RowVersion concurrency (DATA-008). The
        // tracked entity will be updated on SaveChangesAsync; if RowVersion has changed since
        // fetch, DbUpdateConcurrencyException is thrown (optimistic locking).
        _dbContext.Tasks.Update(task);
        _dbContext.OutboxMessages.Add(BuildOutboxMessage(auditFact));

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task ReopenAsync(TaskItem task, EntityMutationAudited auditFact, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(auditFact);

        // Task has already been mutated by Business via SetReopen. Attach it to the DbContext
        // so EF Core can track the changes and check RowVersion concurrency (DATA-008). The
        // tracked entity will be updated on SaveChangesAsync; if RowVersion has changed since
        // fetch, DbUpdateConcurrencyException is thrown (optimistic locking).
        _dbContext.Tasks.Update(task);
        _dbContext.OutboxMessages.Add(BuildOutboxMessage(auditFact));

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task EditAsync(TaskItem task, EntityMutationAudited auditFact, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(auditFact);

        // Task has already been mutated by Business via Edit. Attach it to the DbContext
        // so EF Core can track the changes and check RowVersion concurrency (DATA-008). The
        // tracked entity will be updated on SaveChangesAsync; if RowVersion has changed since
        // fetch, DbUpdateConcurrencyException is thrown (optimistic locking).
        _dbContext.Tasks.Update(task);
        _dbContext.OutboxMessages.Add(BuildOutboxMessage(auditFact));

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static OutboxMessage BuildOutboxMessage(EntityMutationAudited auditFact)
    {
        // The fact's own EventId becomes both the outbox row's identity and, later, the Service
        // Bus native MessageId the relay sends (OutboxRelay.ToOutboundMessage uses
        // OutboxMessage.Id) - the same value Audit's inbox uses for idempotency, exactly as
        // EntityMutationAudited.EventId documents (OUTBOX-004, ASYNC-005).
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

using Microsoft.EntityFrameworkCore;
using ProjectChicago.Audit.Core.Models;
using ProjectChicago.Audit.Core.Persistence;
using ProjectChicago.Audit.Core.Repositories;
using ProjectChicago.Shared.Inbox;

namespace ProjectChicago.Audit.Core.Data;

/// <summary>
/// Audit Service Data layer implementation for idempotent audit entry append and inbox processing (ADR-0016, ASYNC-005..008).
/// Handles atomic persistence of AuditEntry and InboxMessage state transitions within a single database transaction.
/// Leverages the EventId unique constraint on AuditEntry to detect and safely handle duplicate Service Bus deliveries.
/// Also provides read-only audit entry queries through the AuditRepository layer.
/// </summary>
public class AuditData : IAuditData
{
    private readonly AuditDbContext _context;
    private readonly IAuditRepository _repository;

    public AuditData(AuditDbContext context, IAuditRepository repository)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <summary>
    /// Idempotently appends an AuditEntry and marks an InboxMessage as Completed in a single atomic transaction.
    ///
    /// Transaction flow:
    /// 1. Check if InboxMessage with this MessageId already exists and its status.
    /// 2. If Completed, return success (duplicate already processed).
    /// 3. Prepare the AuditEntry for insertion and update InboxMessage state to Completed in memory.
    /// 4. Attempt SaveChangesAsync with all state changes atomically.
    /// 5. If AuditEntry insert fails with unique constraint on EventId (caught as DbUpdateException):
    ///    - Reload and check if the Inbox is already Completed.
    ///    - If yes: duplicate already processed, return success (safe no-op).
    ///    - If no: inbox processing failed on prior attempt, rollback and rethrow (caller retries via Function).
    /// 6. Either AuditEntry and InboxMessage both persist, or the entire transaction rolls back.
    ///
    /// Satisfies:
    /// - ASYNC-005: Duplicate message delivery handling.
    /// - ASYNC-006: Idempotent business operation (duplicate appends are detected and no-op).
    /// - AUDIT-004: Append-only immutable entries (INSERT only, no UPDATE).
    /// - OUTBOX-001/002: Atomic transaction of domain state and audit facts.
    /// </summary>
    public async Task AppendAuditEntryIdempotentlyAsync(
        AuditEntry auditEntry,
        InboxMessage inboxMessage,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(auditEntry);
        ArgumentNullException.ThrowIfNull(inboxMessage);

        // Start a database transaction to ensure atomicity of AuditEntry + InboxMessage state.
        using (var transaction = await _context.Database.BeginTransactionAsync(cancellationToken))
        {
            try
            {
                // Step 1: Check if this message has already been processed.
                var existingInboxMessage = await _context.InboxMessages
                    .FirstOrDefaultAsync(m => m.MessageId == inboxMessage.MessageId, cancellationToken);

                // Step 2: If already Completed, return success (safe no-op for duplicate).
                if (existingInboxMessage?.Status == InboxMessageStatus.Completed)
                {
                    // Duplicate delivery and processing is complete; no error.
                    await transaction.CommitAsync(cancellationToken);
                    return;
                }

                // Step 3: Prepare all state changes atomically.
                // Add the AuditEntry for insertion.
                _context.AuditEntries.Add(auditEntry);

                // Prepare InboxMessage: register if new, or update for retry attempt.
                if (existingInboxMessage == null)
                {
                    // First delivery: register inbox message and set final state to Completed.
                    inboxMessage.Status = InboxMessageStatus.Received;
                    inboxMessage.ProcessingStartedAtUtc = DateTime.UtcNow;
                    inboxMessage.ProcessingCompletedAtUtc = DateTime.UtcNow;
                    inboxMessage.Status = InboxMessageStatus.Completed;
                    inboxMessage.AttemptCount = 1;
                    inboxMessage.LastAttemptAtUtc = DateTime.UtcNow;
                    _context.InboxMessages.Add(inboxMessage);
                }
                else
                {
                    // Existing inbox entry from a prior failed attempt.
                    // Update it to mark retry processing and completion.
                    existingInboxMessage.ProcessingStartedAtUtc = DateTime.UtcNow;
                    existingInboxMessage.ProcessingCompletedAtUtc = DateTime.UtcNow;
                    existingInboxMessage.Status = InboxMessageStatus.Completed;
                    existingInboxMessage.AttemptCount++;
                    existingInboxMessage.LastAttemptAtUtc = DateTime.UtcNow;
                    _context.InboxMessages.Update(existingInboxMessage);
                }

                // Step 4: Persist AuditEntry and InboxMessage state atomically.
                // If the EventId unique constraint on AuditEntry is violated, SaveChangesAsync throws
                // a DbUpdateException. We catch this and check if the inbox is already Completed
                // (indicating a safe duplicate that fully processed).
                try
                {
                    await _context.SaveChangesAsync(cancellationToken);
                }
                catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("AK_AuditEntries_EventId") ?? false)
                {
                    // The EventId unique constraint was violated: an AuditEntry with this EventId already exists.
                    // This can occur if a prior successful delivery created the entry, or if concurrent
                    // deliveries race (Function platform handles concurrent delivery retry at a higher level).

                    // Step 5a: Check if the inbox is already Completed (full idempotent success).
                    var maybeCompletedInbox = await _context.InboxMessages
                        .FirstOrDefaultAsync(m => m.MessageId == inboxMessage.MessageId, cancellationToken);

                    if (maybeCompletedInbox?.Status == InboxMessageStatus.Completed)
                    {
                        // Duplicate delivery: both AuditEntry and InboxMessage already fully processed.
                        // This is a safe no-op: the operation is idempotent.
                        await transaction.CommitAsync(cancellationToken);
                        return;
                    }

                    // Step 5b: Inbox exists but is not Completed (prior processing failed).
                    // Rollback to allow Function trigger to retry or dead-letter the message.
                    await transaction.RollbackAsync(cancellationToken);
                    throw;
                }

                // Step 6: Both AuditEntry and InboxMessage persisted atomically.
                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                // On any error, rollback is automatic if not explicitly committed.
                // The Function trigger will fail the invocation, allowing Service Bus to retry or dead-letter.
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }
    }

    /// <summary>
    /// Query audit entries by entity type and ID with pagination.
    /// Delegates to the repository layer for query execution.
    /// </summary>
    public async Task<AuditListResult> QueryByEntityAsync(
        string entityType,
        Guid entityId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        return await _repository.QueryByEntityAsync(entityType, entityId, pageNumber, pageSize, cancellationToken);
    }

    /// <summary>
    /// Query audit entries by trace ID or correlation ID with pagination.
    /// Delegates to the repository layer for query execution.
    /// </summary>
    public async Task<AuditListResult> QueryByTraceOrCorrelationIdAsync(
        string? traceId,
        string? correlationId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        return await _repository.QueryByTraceOrCorrelationIdAsync(traceId, correlationId, pageNumber, pageSize, cancellationToken);
    }
}

using Microsoft.EntityFrameworkCore;
using ProjectChicago.Shared.Messaging;
using ProjectChicago.Shared.Outbox;

namespace ProjectChicago.Crm.Core.Persistence;

// CRM's SQL Server-backed IOutboxStore (OUTBOX-003..006, DATA-006/008; database.md, messaging.md).
// Claims a bounded batch of due Pending rows and leases each one individually, guarded by
// OutboxMessage.RowVersion (EF Core optimistic concurrency) - two concurrent relay instances can
// never both believe they claimed the same row. A losing claim surfaces as
// DbUpdateConcurrencyException and is simply skipped, leaving that row for a later run
// (messaging.md: "Relay selection/lease must prevent uncontrolled duplicate concurrent dispatch").
public sealed class CrmOutboxStore : IOutboxStore
{
    private readonly CrmDbContext _dbContext;

    public CrmOutboxStore(CrmDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<IReadOnlyList<OutboxMessage>> ClaimPendingBatchAsync(
        int batchSize, string leaseOwner, TimeSpan leaseDuration, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var candidates = await _dbContext.OutboxMessages
            .Where(m => m.Status == OutboxMessageStatus.Pending && (m.LeasedUntilUtc == null || m.LeasedUntilUtc < now))
            .OrderBy(m => m.CreatedAtUtc)
            .Take(batchSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (candidates.Count == 0)
        {
            return [];
        }

        var leasedUntil = now.Add(leaseDuration);
        var claimed = new List<OutboxMessage>(candidates.Count);

        foreach (var message in candidates)
        {
            message.LeaseOwner = leaseOwner;
            message.LeasedUntilUtc = leasedUntil;

            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                claimed.Add(message);
            }
            catch (DbUpdateConcurrencyException)
            {
                // Another relay instance already claimed this row - its RowVersion changed
                // underneath us. Detach so this context's stale copy doesn't affect later
                // SaveChanges calls in this batch, and leave the row for a future run.
                _dbContext.Entry(message).State = EntityState.Detached;
            }
        }

        return claimed;
    }

    public async Task MarkDispatchedAsync(Guid messageId, CancellationToken cancellationToken)
    {
        var message = await _dbContext.OutboxMessages.FindAsync([messageId], cancellationToken).ConfigureAwait(false);
        if (message is null)
        {
            return;
        }

        message.Status = OutboxMessageStatus.Dispatched;
        message.DispatchedAtUtc = DateTime.UtcNow;
        message.LeaseOwner = null;
        message.LeasedUntilUtc = null;

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task RecordFailedAttemptAsync(Guid messageId, string error, CancellationToken cancellationToken)
    {
        var message = await _dbContext.OutboxMessages.FindAsync([messageId], cancellationToken).ConfigureAwait(false);
        if (message is null)
        {
            return;
        }

        message.AttemptCount++;
        message.LastAttemptAtUtc = DateTime.UtcNow;
        // LastError is nvarchar(1000) (OutboxMessageConfiguration) - truncate defensively so a long
        // exception message can never turn a failed-publish record into a second, SQL-level failure.
        message.LastError = error.Length > 1000 ? error[..1000] : error;
        message.LeaseOwner = null;
        message.LeasedUntilUtc = null;

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

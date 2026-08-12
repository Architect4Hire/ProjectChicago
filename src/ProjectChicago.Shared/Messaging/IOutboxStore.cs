using ProjectChicago.Shared.Outbox;

namespace ProjectChicago.Shared.Messaging;

// Implemented by each publishing service's Data layer against its own DbContext/database.
// OutboxRelay depends only on this abstraction - Shared never owns a service-specific DbContext, and
// the relay never queries outbox rows directly. The lease/concurrency strategy (preventing
// uncontrolled duplicate concurrent dispatch across relay instances, per messaging.md) is this
// store's responsibility to implement atomically; OutboxRelay trusts whatever it returns as already
// claimed and does no selection or locking of its own.
public interface IOutboxStore
{
    Task<IReadOnlyList<OutboxMessage>> ClaimPendingBatchAsync(
        int batchSize,
        string leaseOwner,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken);

    // Called only after the shared publisher confirms the broker accepted the message.
    Task MarkDispatchedAsync(Guid messageId, CancellationToken cancellationToken);

    // Leaves the message Pending (see OutboxMessageStatus) so the next relay run retries it.
    Task RecordFailedAttemptAsync(Guid messageId, string error, CancellationToken cancellationToken);
}

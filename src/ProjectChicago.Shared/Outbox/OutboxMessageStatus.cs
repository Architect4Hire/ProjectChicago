namespace ProjectChicago.Shared.Outbox;

// A failed publish attempt leaves a message Pending so the relay retries it (see messaging.md);
// there is no terminal "Failed" status here - AttemptCount/LastError/LastAttemptAtUtc on
// OutboxMessage carry the retry/failure metrics OUTBOX-006 requires.
public enum OutboxMessageStatus
{
    Pending = 0,
    Dispatched = 1,
}

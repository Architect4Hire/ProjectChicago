namespace ProjectChicago.Shared.Inbox;

// Received/started/completed lifecycle plus a terminal Failed state for exhausted local recovery
// (ASYNC-007: poison messages are not retried indefinitely). Failed here is distinct from Service
// Bus's own dead-letter mechanism - it records that the owning service's recovery policy gave up on
// this row; the Function trigger still decides delivery-level retry/dead-letter by failing or
// completing the invocation (messaging.md failure semantics).
public enum InboxMessageStatus
{
    Received = 0,
    Processing = 1,
    Completed = 2,
    Failed = 3,
}

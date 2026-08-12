# Transactional Outbox and Inbox

## Producer
A mutation writes state and an OutboxMessage in one local SQL transaction.

```text
Data transaction:
  UPDATE/INSERT domain state
  INSERT outbox envelope
  COMMIT
```

A TimerTrigger Function later leases a bounded batch and publishes. Mark dispatched only after confirmed send.

## Consumer
A ServiceBusTrigger Function delegates to Core. Core/Data uses persistent InboxMessage state keyed by stable EventId. Duplicate completed deliveries no-op.

## Guarantees
- avoids local DB + broker dual-write gap,
- supports at-least-once delivery,
- does not claim global exactly-once behavior,
- allows retries/replay with idempotent business effects.

## Observability
Track pending count, oldest age, publish failures/retries, consumer outcomes and DLQ. Preserve correlation/causation/event IDs.

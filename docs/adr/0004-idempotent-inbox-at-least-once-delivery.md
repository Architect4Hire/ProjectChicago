# ADR-0004 — Persistent Inbox for At-Least-Once Delivery

- **Status:** Accepted
- **Requirements:** ASYNC-005..008, TEST-005

## Context
Azure Service Bus can redeliver messages. Correctness cannot depend on exactly-once transport behavior.

## Decision
Each consuming service persists inbox state in its own database keyed by the stable integration event/message ID. Consumer processing is idempotent: an already completed message produces no duplicate business effect.

The inbox completion state participates in the consumer's local transaction where required.

## Consequences
- Every integration event needs a stable ID.
- Duplicate delivery is normal, not exceptional.
- Failed processing must not be recorded as completed.
- Inbox retention/purge requires an operational policy.

## Alternatives considered
- In-memory deduplication: not durable across restarts/scaling.
- Broker duplicate detection alone: not sufficient as the application correctness boundary.

## Validation
Consumer integration tests replay the same message and prove one business/audit effect.

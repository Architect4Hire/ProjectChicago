# ADR-0016 — Durable Audit Bounded Context and Retention

- **Status:** Proposed
- **Requirements:** AUDIT-001..008, PRIV-001..005, DATA-020..023

## Context
The requirements explicitly separate business audit history from operational logging. Audit records need append-only behavior, actor/change metadata and longer governance than diagnostic logs.

## Proposed decision
If ADR-0015 is accepted, the Audit bounded context owns durable `AuditEntry` persistence. Business mutations create an audit integration event in the owning service's transactional outbox. Service Bus delivers it to Audit.Functions; Audit persists it idempotently through its inbox.

Audit records include entity/action/time/actor/source/TraceId/CorrelationId/CausationId and approved previous/new changed-field representation. Secrets, credentials and tokens are never captured.

Retention duration, purge authority and privacy/legal exceptions must be explicitly approved before production.

## Consequences
- Technical logs can have independent retention.
- Audit survives source-service log expiration.
- Audit is eventually consistent with the originating committed mutation.
- UI/API audit querying is read-only and role-restricted.

## Open items
- Exact retention duration.
- Legal/privacy purge process.
- Field-level redaction policy for Client PII.
- Whether audit payloads store complete before/after values or changed-field projections.

## Validation
Prompt sequence must stop for approval before treating this ADR as Accepted. End-to-end test proves exactly one AuditEntry after message replay.

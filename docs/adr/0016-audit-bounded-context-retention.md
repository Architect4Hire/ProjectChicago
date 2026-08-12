# ADR-0016 — Durable Audit Bounded Context and Retention

- **Status:** Accepted
- **Date approved:** 2026-08-12
- **Requirements:** AUDIT-001..008, PRIV-001..005, DATA-020..023

## Context
The requirements explicitly separate business audit history from operational logging. Audit records need append-only behavior, actor/change metadata and longer governance than diagnostic logs.

## Decision
The Audit bounded context (ADR-0015) owns durable `AuditEntry` persistence exclusively. Owning services publish one shared, versioned cross-service audit fact — `ProjectChicago.Contracts.Audit.EntityMutationAudited` — through their normal transactional outbox for every Client/Project/Task mutation. Service Bus delivers it to `ProjectChicago.Audit.Functions`; Audit persists it idempotently through its inbox into its own `AuditEntry` storage model.

The contract is generic across entities (one event type, not one per entity/action) and carries the minimum durable business-audit data required by AUDIT-002: owning service, entity type/ID, action, actor ID/type, occurred-at UTC, changed-field names, and previous/new values only for fields approved as safe to disclose. It carries the standard envelope trace/correlation/causation identifiers (ADR-0010) so an operator can move between the audit record and its distributed trace. Secrets, credentials, tokens, and cryptographic material are never captured (AUDIT-008) — see the `AuditSensitiveFieldNames` guard co-located with the contract.

Retention duration, purge authority and privacy/legal exceptions must still be explicitly approved before production; this ADR only accepts the ingestion contract and mechanism, not final retention policy (tracked as an open item below).

## Relationship to docs/design/adr-0016-audit-event-driven-architecture.md
That document (also Accepted) describes Audit subscribing broadly to the existing per-entity business events (e.g. `ClientCreated`, `ProjectStatusChanged`) already published for other consumers (Notification, Search), and mapping each into `AuditEntry` itself. This ADR supersedes that specific mechanism for the audit-ingestion contract: owning services publish the dedicated `EntityMutationAudited` fact instead of requiring Audit to understand every per-entity business event shape. The rest of that document — Audit's exclusive database ownership, Service Bus ingestion via outbox/inbox, redaction rules, `AuditEntry` storage fields, ordering/idempotency guarantees, and the retention/purge governance sections — remains in force and complementary to this decision.

## Consequences
- Technical logs can have independent retention.
- Audit survives source-service log expiration.
- Audit is eventually consistent with the originating committed mutation.
- UI/API audit querying is read-only and role-restricted.
- Owning services do not need to keep Audit in sync with every per-entity event shape they define; they populate one stable contract instead.

## Open items
- Exact retention duration.
- Legal/privacy purge process.
- Field-level redaction policy for Client PII.

## Validation
Contract tests (`ProjectChicago.Contracts.Tests`) prove required metadata, versioning, serialization, and the redaction boundary. End-to-end test proves exactly one AuditEntry after message replay (tracked with the Audit consumer implementation).

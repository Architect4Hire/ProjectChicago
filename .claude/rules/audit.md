---
paths:
  - "src/services/ProjectChicago.Audit*/**"
  - "src/ProjectChicago.Contracts/**"
  - "docs/**audit**"
---
# Support audit trail rules — conditional

This rule is retained from the JobBoard toolkit because a CRM often benefits from a durable support/audit trail, but **Project Chicago has not yet approved an Audit bounded context**. Do not scaffold one solely because this file exists.

If Audit is approved:

- Audit is its own bounded context with its own SQL Server database.
- Owning services do not write the audit database directly.
- Business mutations emit appropriate past-tense integration events through their normal transactional outbox.
- Audit consumes those events using Azure Service Bus-triggered Functions, not hosted consumers.
- Audit writes append-only audit entries idempotently using its inbox.
- Preserve CorrelationId, CausationId, event/message ID, actor identifier (when appropriate), entity identifiers, event type/version and occurred-at UTC.
- Store the event/audit payload in a SQL Server-compatible representation; do not carry over PostgreSQL `jsonb` assumptions.
- Support queries go through an Audit API/gateway route or approved observability/read surface, never direct DB access from another service.
- Minimize sensitive CRM data in audit payloads. Prefer identifiers + changed-field metadata where possible; do not turn the audit store into an uncontrolled customer-data duplicate.
- Define retention/legal requirements explicitly before treating the audit store as permanent.

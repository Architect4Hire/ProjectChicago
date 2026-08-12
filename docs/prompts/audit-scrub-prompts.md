# Project Chicago — Audit-Focused SCRUB Micro-Prompts

This is a focused extraction/review sequence for the durable audit capability. The canonical full-system prompt file remains `project-chicago-scrub-microprompts.md`; do **not** run both plans independently and create duplicate artifacts.

## A00 — Confirm Audit architecture decision

```text
REQUIREMENTS: AUDIT-001..008; PRIV-001..005; DATA-020..023

SCOPE: Review ADR-0015 and ADR-0016 and determine whether the Audit bounded-context/retention design is approved enough to implement.

CONSTRAINT: Business audit is distinct from logs; audit is append-only; source services do not write AuditDb; secrets/tokens are prohibited.

RESTRICTION: Do not create projects, database schema, Service Bus topology, or code.

USAGE: Read requirements, CLAUDE.md, docs/adr/0015*, docs/adr/0016*, .claude/rules/audit.md and messaging.md.

BEHAVIOR: Report approved decisions and unresolved items. If ADRs are Proposed, stop for human approval. STOP.
```

## A01 — Confirm the audit integration-event contract

```text
REQUIREMENTS: AUDIT-002..008; TRACE-003..007; OUTBOX-005

SCOPE: Validate or add exactly the audit integration-event payload/envelope contract needed for business mutations.

CONSTRAINT: Include event ID/type/version/time, entity/action, actor type/ID, source service, TraceId/CorrelationId/CausationId and approved changed-field representation.

RESTRICTION: Do not add Audit persistence, Functions, Service Bus topology, or business-specific mutation code. Do not include secrets/tokens.

USAGE: Use add-audit-event/add-integration-event skills and Contracts conventions.

BEHAVIOR: Add/adjust contract plus round-trip/redaction tests, run focused tests, report and STOP.
```

## A02 — Scaffold Audit bounded service only

```text
REQUIREMENTS: AUDIT-001..008; DATA-031..034

SCOPE: Create only Audit HTTP host, Audit.Core and Audit.Functions plus allowed project references/tests.

CONSTRAINT: One Audit SQL database; HTTP host is read-only support surface; Functions are async entry points.

RESTRICTION: Do not create schema, triggers, gateway routes or ingestion logic.

USAGE: Follow backend.md, functions.md and accepted ADRs.

BEHAVIOR: Build the new projects, inspect reference graph, prove no Crm.Core/Identity.Core reference and STOP.
```

## A03 — Add append-only Audit persistence

```text
REQUIREMENTS: AUDIT-001..008; ASYNC-005..008

SCOPE: Add only AuditEntry + InboxMessage persistence and the idempotent append Data/Repository transaction.

CONSTRAINT: SQL Server; unique EventId; entity/time/trace/correlation indexes; duplicate completed EventId no-ops; normal update/delete path does not exist.

RESTRICTION: Do not add Function trigger, query API or cross-service database access.

USAGE: Follow audit.md/database.md/messaging.md.

BEHAVIOR: Add SQL integration tests for first delivery, duplicate, failure rollback and immutability; run them and STOP.
```

## A04 — Add Audit ingestion Core behavior

```text
REQUIREMENTS: AUDIT-001..008; PRIV-001..005

SCOPE: Add only Facade/Business ingestion translation, contract validation and redaction.

CONSTRAINT: Delegate persistence to Audit Data; preserve actor/trace/correlation/causation; reject unsupported contract according to policy.

RESTRICTION: Do not add Service Bus trigger or read API.

USAGE: Use add-audit-event skill.

BEHAVIOR: Add unit tests for valid event, redaction, malformed/unsupported version and duplicate result mapping; run and STOP.
```

## A05 — Add one Audit Service Bus trigger

```text
REQUIREMENTS: AUDIT-001..008; ASYNC-001..008; TRACE-003..007

SCOPE: Add exactly one ServiceBusTrigger Function for the approved audit subscription.

CONSTRAINT: Function is transport-only; deserialize/extract trace context/call Facade; unexpected failure fails invocation.

RESTRICTION: No Repository/DbContext in Function. No catch-and-return-success. No HTTP trigger.

USAGE: Use add-function-trigger skill.

BEHAVIOR: Test valid delegation, correlation, cancellation and failure propagation; run function-boundary review and STOP.
```

## A06 — Add privileged Audit queries

```text
REQUIREMENTS: AUDIT-001..008; ACTIVITY-001..003; SEC-010..013

SCOPE: Add read-only entity and Trace/Correlation Audit query use cases and their minimal HTTP actions.

CONSTRAINT: Newest-first/paginated, authorized roles only, safe/redacted public response.

RESTRICTION: No audit mutation API. No Crm/Audit cross-database query.

USAGE: Use add-endpoint skill.

BEHAVIOR: Run SQL/API tests for filters, pagination, 401/403 and absence of mutation routes; STOP.
```

## A07 — Prove mutation-to-audit durability

```text
REQUIREMENTS: TRACE-001..007; AUDIT-001..008; OUTBOX-001..006

SCOPE: Trace exactly one Client create from YARP through Crm SQL/outbox, timer Function, Service Bus, Audit Function and AuditDb.

CONSTRAINT: Preserve TraceId/CorrelationId/CausationId/EventId/actor metadata. Redelivery must remain idempotent.

RESTRICTION: Do not add new product features or broad refactors.

USAGE: Use trace-a-request skill, Aspire Dashboard and Audit query API.

BEHAVIOR: Produce concrete evidence for each hop plus exactly one AuditEntry before/after redelivery; STOP.
```

## A08 — Audit completeness review

```text
REQUIREMENTS: AUDIT-001..008

SCOPE: Review every implemented Client/Project/Task mutation and identify whether it creates the correct audit fact through transactional outbox.

CONSTRAINT: Review only; distinguish operational logging from durable audit.

RESTRICTION: Do not edit code in this prompt.

USAGE: Use read-only code reviewer/test-gap analyzer.

BEHAVIOR: Produce a mutation-by-mutation PASS/FAIL matrix with file/test references and requirement IDs; verify git status unchanged; STOP.
```

---
name: add-audit-event
description: If Project Chicago has an approved Audit bounded context, make a business mutation land in its durable support trail through the normal integration event/outbox, Service Bus-triggered Audit Function, inbox idempotency and SQL Server append. Never creates Audit automatically.
---
# Add an audit event — conditional

## Gate: confirm Audit exists

First inspect the solution/architecture. If there is no approved `ProjectChicago.Audit` (or explicit equivalent), stop and report:

> Audit support is intentionally conditional in the Claude toolkit. No Audit bounded context is approved/present, so this skill will not create one implicitly.

Do not scaffold a new service from this skill.

If Audit exists, read `.claude/rules/audit.md` and `.claude/rules/messaging.md`.

## 1. Reuse the business event when possible

A support audit trail should usually record the same past-tense fact already emitted for business integration.

Ask:

- Does this mutation already produce a suitable event?
- Does that event carry stable entity/actor/thread identifiers needed for support?
- Would adding a second audit-only event create duplicate semantic facts?

Prefer extending the standard event envelope/thread metadata additively over publishing redundant "AuditSomething" events.

## 2. Publish side

In the owning service:

- Business decides the fact.
- Data writes state + outbox atomically.
- Timer-triggered outbox relay publishes it.

Never write Audit's DB directly.

## 3. Audit consume side

In `ProjectChicago.Audit.Functions`:

- add/reuse Service Bus trigger/subscription;
- deserialize Contracts event;
- establish correlation/message context;
- delegate to Audit Facade;
- allow processing failure to fail invocation.

Audit `.Core`:

- inbox dedupe;
- map event envelope + support-safe payload to append-only entry;
- write Audit SQL row and inbox completion transactionally as designed;
- no callback into publisher to fill missing data.

## 4. Payload minimization

Record enough to answer support questions without duplicating full customer records unnecessarily:

- event type/version;
- event/message ID;
- CorrelationId/CausationId;
- actor ID/category if policy permits;
- owning service;
- entity type + stable ID(s);
- occurred-at/recorded-at UTC;
- changed-field names/status transition where useful;
- compact event payload only when justified.

Avoid credentials, tokens, secrets and unnecessary sensitive customer fields.

Use SQL Server-compatible storage; no `jsonb` assumption.

## 5. Query path

Support/audit reads go through approved Audit API/gateway/observability surfaces. Do not teach other services to query `auditdb` directly.

## 6. Tests

- owning mutation produces event via outbox;
- Audit first delivery appends once;
- duplicate delivery appends zero additional rows;
- failed append does not complete inbox;
- correlation/causation/actor/entity fields retained;
- sensitive fields not duplicated unintentionally;
- query route returns ordered trace as designed.

Run `audit-coverage-checker` after changes.

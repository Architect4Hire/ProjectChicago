# Project Chicago — Ongoing Architecture Plan

This is the forward-looking architecture backlog. It does not silently convert open questions into decisions.

## Phase 0 — Decision gates

Before broad scaffolding:

1. Accept/revise ADR-0015 bounded-context catalog.
2. Accept/revise ADR-0018 browser auth/session transport.
3. Accept/revise ADR-0016 Audit ownership/retention.
4. Accept/revise ADR-0017 Service Bus topology.
5. Update `CLAUDE.md` only after each architecture choice is approved.

## Phase 1 — Shared platform

Establish:

- solution/build/package management,
- Aspire AppHost and ServiceDefaults,
- Contracts and Shared,
- YARP,
- React/Vite/Tailwind/PCDS,
- OpenTelemetry/Azure Monitor exporter configuration,
- correlation/error/event envelope primitives,
- outbox/inbox and reusable relay,
- local SQL/Service Bus resources.

Success condition: shared spine builds and telemetry/resource topology can be inspected without a feature service.

## Phase 2 — CRM vertical slice

Scaffold the approved CRM service and prove one Client create slice:

- Crm host/Core/Functions,
- CrmDb,
- Client entity/persistence,
- Controller → Facade → Business → Data → Repository,
- state + outbox atomic commit,
- timer Function publication.

Do not fan out to Projects/Tasks until the slice proves the architecture.

## Phase 3 — Business completeness

Implement Client, Project and Task requirements atomically, including:

- lists/search/filter/pagination,
- detail views,
- lifecycle/status transitions,
- assignment/priority,
- archival,
- concurrency,
- dashboard/global search.

## Phase 4 — Identity

Implement the approved Identity context after auth/session ADR acceptance:

- IdentityDb,
- roles,
- login/logout/current user,
- user administration required by product,
- gateway route,
- CRM authorization policies.

## Phase 5 — Audit

Implement durable Audit only after ADR-0016/0017:

- AuditDb + append-only model,
- inbox,
- Service Bus trigger,
- redaction/validation,
- privileged queries,
- activity timeline.

## Phase 6 — User experience

Build React features through the local PCDS:

- login/protected routing,
- Clients,
- Projects,
- Tasks,
- dashboard,
- search,
- audit/activity where authorized.

## Phase 7 — Operational proof

Prove:

- one cradle-to-grave Client trace,
- Service Bus duplicate/idempotency behavior,
- outbox failure recovery,
- SQL concurrency,
- route/security matrix,
- accessibility,
- p95 guardrails,
- architecture constraints.

## Production architecture still to decide

### Production SQL

Resolve ADR-0019 including HA/backup/private networking/cost.

### IaC

Resolve ADR-0020; provision Flex Consumption, SQL, Service Bus, identity/RBAC, observability and networking reproducibly.

### Alerting/SLOs

Requirements define signals, but production thresholds/action groups should be tuned against observed workloads rather than guessed during scaffolding.

### Retention/privacy

Audit and telemetry retention need explicit business/legal/operational ownership.

## Evolution rule

New bounded services require a real domain boundary, independent lifecycle/deployment/data ownership need, and an ADR. Do not create a service merely because an entity has its own table.

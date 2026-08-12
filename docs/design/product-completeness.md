# Project Chicago — Product Completeness

This document deliberately separates **requirements**, **prompt coverage**, and **implementation evidence**.

A feature is not implemented merely because it appears in a design document or SCRUB prompt.

## Status vocabulary

- **Specified** — present in requirements.
- **Prompt-covered** — canonical SCRUB sequence contains implementation/verification work.
- **Implemented** — source/runtime evidence exists and tests pass.
- **Accepted architecture** — recorded as an Accepted ADR/current constitution.
- **Proposed architecture** — recommended but still a human decision gate.

## Product capability matrix

| Capability | Specified | Prompt-covered | Implementation evidence |
|---|---:|---:|---|
| Client create/list/detail/search/filter | Yes | Yes | Verify in current `src/` |
| Client lifecycle/archive | Yes | Yes | Verify in current `src/` |
| Project create/list/detail/status/archive | Yes | Yes | Verify in current `src/` |
| Task create/list/assign/priority/status/reopen | Yes | Yes | Verify in current `src/` |
| Dashboard | Yes | Yes | Verify in current `src/` |
| Global search | Yes | Yes | Verify in current `src/` |
| ASP.NET Core Identity | Yes | Yes | Verify in current `src/` |
| Role authorization | Yes | Yes | Verify in current `src/` |
| Business audit history | Yes | Yes | Proposed Audit service decision must be accepted first |
| Transactional outbox/inbox | Yes | Yes | Verify in current `src/` |
| Service Bus Functions | Yes | Yes | Verify in current `src/` |
| OpenTelemetry | Yes | Yes | Verify in current `src/` |
| Azure Monitor/App Insights dashboards | Yes | Yes | Verify IaC/config/runtime |
| PCDS React UX | Yes | Yes | Verify in current `src/web` |
| Accessibility target | Yes | Yes | Verify automated/manual evidence |

## Architecture gates that prevent false completeness

The canonical prompts intentionally stop for approval on:
- bounded-context catalog,
- browser auth/session transport,
- Audit retention/design,
- Service Bus topology,
- production SQL topology,
- infrastructure as code.

Until those decisions are Accepted, dependent items should not be labeled production-complete.

## Definition of done per API feature

A mutation is not done unless applicable:
- auth/authz,
- validation,
- stable API contract,
- safe error shape,
- optimistic concurrency,
- SQL transaction,
- audit event,
- outbox atomicity,
- OTel/structured logging,
- focused unit/SQL/API tests,
- architecture boundaries.

A read feature is not done unless applicable:
- auth/authz and scope trimming,
- bounded pagination,
- deterministic sort,
- SQL-translatable query,
- safe contract,
- error states,
- focused SQL/API tests.

## Definition of done per Function

A Function is not done unless:
- trigger is transport-only,
- delegates to Facade,
- trace/correlation context is propagated,
- retries/failure behavior are deliberate,
- idempotency exists for consumers,
- cancellation is honored,
- no sensitive logging,
- Function tests cover boundary behavior.

## Release evidence

The release gate should archive or report:
- `.NET` build/test results,
- SQL integration test results,
- Function reliability matrix,
- API security matrix,
- React lint/test/build,
- accessibility checks,
- architecture guardrail review,
- end-to-end TraceId/CorrelationId proof,
- known/deferred requirement IDs.

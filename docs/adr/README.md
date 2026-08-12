# Project Chicago Architecture Decision Records

ADRs record decisions, not aspirations. This index distinguishes constraints already established by Project Chicago from decisions still intentionally open.

## Status rules

- **Accepted** — established by the requirements and/or current Project Chicago architecture constitution.
- **Proposed** — recommended design awaiting explicit human approval.
- **Superseded** — retained for history; a later ADR replaces it.
- **Rejected** — considered and deliberately not selected.

Once an ADR is **Accepted**, do not rewrite history. Supersede it with a new ADR if the decision changes.

## Index

| ADR | Decision | Status |
|---|---|---|
| [0001](0001-database-per-service-sql-server.md) | SQL Server/Azure SQL database per bounded service | Accepted |
| [0002](0002-event-driven-integration-over-service-bus.md) | Azure Service Bus for durable asynchronous integration | Accepted |
| [0003](0003-transactional-outbox-function-relay.md) | Transactional outbox drained by timer-triggered Azure Functions | Accepted |
| [0004](0004-idempotent-inbox-at-least-once-delivery.md) | Persistent inbox/idempotency for at-least-once consumers | Accepted |
| [0005](0005-thin-host-core-functions-layering.md) | Thin HTTP/Functions hosts with Core layering | Accepted |
| [0006](0006-single-api-gateway-yarp.md) | YARP is the only browser-facing backend edge | Accepted |
| [0007](0007-aspnet-core-identity-baseline.md) | ASP.NET Core Identity is the identity framework | Accepted |
| [0008](0008-aspire-local-first-sql-servicebus.md) | Aspire local-first orchestration | Accepted |
| [0009](0009-contracts-leaf-versioned-event-envelope.md) | Leaf integration contracts and versioned event envelope | Accepted |
| [0010](0010-w3c-trace-correlation-causation.md) | W3C trace context plus correlation/causation metadata | Accepted |
| [0011](0011-open-telemetry-azure-monitor-observability.md) | OpenTelemetry with Azure Monitor/Application Insights | Accepted |
| [0012](0012-local-pcds-react-design-system.md) | Local PCDS is the React design-system source of truth | Accepted |
| [0013](0013-azure-functions-flex-consumption-async-boundary.md) | Azure Functions isolated/Flex Consumption for async entry points | Accepted |
| [0014](0014-archival-and-optimistic-concurrency.md) | Archival over normal deletion and optimistic concurrency | Accepted |
| [0015](0015-initial-bounded-context-catalog.md) | Initial Crm / Identity / Audit bounded-context catalog | Proposed |
| [0016](0016-audit-bounded-context-retention.md) | Durable Audit bounded context and retention ownership | Proposed |
| [0017](0017-service-bus-topology.md) | Initial Service Bus topics/subscriptions/permissions | Proposed |
| [0018](0018-browser-authentication-session-transport.md) | Browser auth/session transport | Proposed |
| [0019](0019-production-sql-hosting.md) | Production SQL hosting/topology | Proposed |
| [0020](0020-infrastructure-as-code-deployment-ownership.md) | IaC and deployment ownership | Proposed |

## ADR template

```markdown
# ADR-NNNN — Title

- Status: Proposed | Accepted | Superseded | Rejected
- Date: YYYY-MM-DD
- Deciders: <roles/names>
- Requirements: <IDs>

## Context
What forces the decision?

## Decision
What is being chosen?

## Consequences
What becomes easier/harder?

## Alternatives considered
What was rejected and why?

## Validation
How will we prove the decision in code/runtime?
```

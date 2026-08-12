# Project Chicago Implementation Patterns

These documents explain recurring architecture mechanics. They are deeper than an ADR and less procedural than a SCRUB prompt.

| Pattern | Why it exists |
|---|---|
| [Layered service architecture](layered-service-architecture.md) | Keep transport, business rules, transactions and persistence separated |
| [Database per service](database-per-service-and-data-ownership.md) | Enforce bounded-context ownership |
| [Transactional outbox and inbox](transactional-outbox-and-inbox.md) | Durable publication + idempotent consumption |
| [Azure Functions async entry points](azure-functions-asynchronous-entry-points.md) | Replace hosted workers with thin Functions |
| [Integration event contracts](integration-event-contracts.md) | Stable versioned wire boundary |
| [Correlation, causation and audit](correlation-causation-and-audit-trail.md) | Explain one logical operation across async boundaries |
| [OpenTelemetry observability](open-telemetry-observability.md) | One instrumentation model and operational view |
| [YARP API gateway edge](api-gateway-edge.md) | One browser-facing backend door |
| [Authentication and identity propagation](authentication-and-identity-propagation.md) | Keep identity trusted across gateway/services |
| [Concurrency control](concurrency-control.md) | Prevent silent overwrite |
| [Exception handling/error shape](exception-handling-and-error-shape.md) | Stable safe public errors |
| [Frontend gateway integration](frontend-gateway-integration.md) | React never targets internal services |
| [Aspire orchestration](aspire-orchestration.md) | Reproducible local topology |
| [Project Chicago Design System](project-chicago-design-system.md) | Reuse PCDS instead of repeated Tailwind recipes |

Unlike JobBoard's pattern set, Project Chicago does **not** adopt read-through caching as a baseline. The lightweight CRM requirements do not currently justify cache complexity; introduce caching only after measured need and a separate decision.

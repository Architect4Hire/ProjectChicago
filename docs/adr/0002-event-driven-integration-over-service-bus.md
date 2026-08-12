# ADR-0002 — Event-Driven Integration over Azure Service Bus

- **Status:** Accepted
- **Requirements:** ASYNC-001..008, DATA-033

## Context
Project Chicago needs durable asynchronous processing with observable retries and dead-letter behavior.

## Decision
Use Azure Service Bus for durable asynchronous integration between bounded services. Producers publish versioned integration events through the transactional outbox path. Consumers are Azure Functions with Service Bus triggers.

Synchronous APIs are reserved for user-facing request/response behavior when a direct answer is required; they are not the default mechanism for propagating business changes.

## Consequences
- Delivery is at least once; consumers must be idempotent.
- Contract evolution is explicit.
- Broker entity names/configuration stay outside domain logic.
- Failed poison messages eventually surface through dead-letter handling.
- Exact topology is intentionally deferred to ADR-0017.

## Alternatives considered
- In-process event bus: insufficient across independently deployed services.
- Direct database reads: violates ownership.
- Fire-and-forget HTTP: lacks durable broker semantics.

## Validation
Messaging integration tests cover publish, duplicate delivery, transient failure and dead-letter/retry expectations.

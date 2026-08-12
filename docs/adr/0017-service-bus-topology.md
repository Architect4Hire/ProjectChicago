# ADR-0017 — Initial Azure Service Bus Topology

- **Status:** Proposed
- **Requirements:** ASYNC-001..008, OUTBOX-001..006

## Context
Azure Service Bus is fixed, but topic/queue/subscription names, filters and least-privilege identities should not be invented inside feature code.

## Proposed decision
Adopt the smallest topology that supports approved publishers/consumers. A recommended starting point after ADR-0015:

- a business/integration-events topic (logical name configured, not hard-coded),
- an Audit subscription receiving audit event contracts,
- additional subscriptions only when another bounded context has an actual requirement.

Publishing identities receive Send only. Consumer Function identities receive Listen only for their subscription. HTTP hosts do not receive broker credentials unless a concrete use case requires them; outbox relay Functions perform sends.

Use Service Bus dead-letter queues and platform retry policy; do not implement unbounded application retry loops.

## Consequences
- Broker topology is infrastructure/configuration, not domain code.
- Filters stay simple and contract-oriented.
- New event consumer implies topology + permission change.

## Validation
Prompt 008 produces/ratifies exact entities; Prompt 009 records the approved result; AppHost/IaC tests compare resources to the ADR.

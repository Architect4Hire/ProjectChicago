# ADR-0009 — Leaf Integration Contracts and Versioned Event Envelope

- **Status:** Accepted
- **Requirements:** ASYNC-004, OUTBOX-005, TRACE-003..007

## Context
Services need shared wire contracts without creating a shared business-domain assembly or circular service references.

## Decision
`ProjectChicago.Contracts` is a leaf assembly containing integration-event contracts and envelope primitives only. Events are versioned and carry stable event/message ID, type/version, occurred-at UTC, CorrelationId, CausationId and approved actor metadata.

Domain/EF/service-internal models do not cross this boundary.

## Consequences
- Contract changes require compatibility discipline.
- Consumers can reject/route unsupported versions deterministically.
- The envelope is safe for durable serialization and replay.
- Shared contracts cannot depend on service Core projects.

## Validation
Contract tests round-trip known versions and reject unsupported/malformed contracts as defined by policy.

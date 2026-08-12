# ADR-0010 — W3C Trace Context plus Correlation and Causation

- **Status:** Accepted
- **Requirements:** TRACE-001..007, LOG-003, AUDIT-002..007

## Context
A single user request can cross HTTP, SQL, an outbox delay, Service Bus and multiple Functions. Operators and auditors need both technical parentage and durable business correlation.

## Decision
Use W3C `traceparent`/OpenTelemetry Activity context for distributed traces. Also carry durable `CorrelationId` and `CausationId` metadata across integration events.

- **Trace ID**: technical distributed trace identity.
- **Correlation ID**: stable identifier tying a logical operation/business flow together.
- **Causation ID**: identifier of the immediate event/operation that caused a new event.
- **Event/Message ID**: stable delivery/idempotency identity.

When an async boundary breaks normal parent/child trace lifetime, use the appropriate extracted context or trace link while preserving correlation/causation explicitly.

## Consequences
- Logs and audit entries can link back to technical execution.
- Header/message validation must prevent arbitrary oversized/untrusted values.
- IDs are metadata; payload/PII must not be copied into telemetry.

## Validation
Telemetry tests assert Activity parentage/tags; end-to-end proof follows one Client create across every hop.

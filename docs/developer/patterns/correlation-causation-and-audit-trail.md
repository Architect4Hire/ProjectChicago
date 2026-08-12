# Correlation, Causation and Audit Trail

A distributed trace answers **how the software executed**. An audit trail answers **what business state changed and who caused it**. Project Chicago links them but does not conflate them.

## Identifiers
- TraceId: OTel/W3C execution.
- CorrelationId: logical flow.
- CausationId: immediate cause.
- EventId: durable message identity.
- ActorId/type: authenticated user or system process.

## Mutation
Business creates an audit fact from the actual state change. Data commits it as an outbox event with the mutation.

## Audit consumer
The proposed Audit context receives the event, uses inbox idempotency and appends immutable AuditEntry.

## Async tracing
A delayed consumer may start a new technical trace or linked span depending on propagation/runtime. Durable correlation/causation keeps the business chain reconstructable regardless.

## Redaction
Passwords/tokens/secrets never appear. Before/after data is minimized/redacted according to the approved Audit ADR.

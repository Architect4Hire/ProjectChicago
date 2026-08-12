---
name: trace-a-request
description: Reconstruct a Project Chicago request/event flow end to end using correlation identifiers, structured telemetry and the approved Audit support API when present. Read-only. Never bypasses service boundaries by opening multiple service databases.
---
# Trace a Project Chicago request or CRM entity

This is an investigation skill, not a repair procedure.

## 1. Pick the strongest identifier

Prefer in this order when available:

1. CorrelationId / trace ID;
2. Service Bus message/event ID;
3. stable CRM entity ID + narrow time window;
4. actor/user ID + narrow time window;
5. timestamp + route/function name.

Do not start by connecting to every service database.

## 2. Reconstruct the HTTP edge

Find:

- gateway request start/end;
- public route/method;
- authentication/actor context (without exposing token);
- correlation/trace ID;
- destination service;
- status/duration/error classification.

## 3. Reconstruct owning service request

Follow the same trace/correlation through:

```text
Controller -> Facade -> Business -> Data -> SQL transaction
```

For mutations, identify:

- entity/state change;
- outbox event ID/type recorded;
- transaction outcome.

## 4. Reconstruct outbox relay

Using telemetry/audit support surface:

- timer Function invocation;
- outbox event selected;
- Service Bus publish success/failure;
- dispatch mark;
- retries if present.

A committed outbox row with no publish evidence is different from a publish that no consumer handled; state that distinction clearly.

## 5. Reconstruct Service Bus consume

For each subscriber:

- consuming Function name/service;
- message ID/event type;
- delivery/retry evidence;
- correlation/causation;
- inbox duplicate/completion result;
- service side effect;
- follow-on outbox event if any.

## 6. Audit bounded context when enabled

If an approved Audit support API exists, use it as the durable business-event chronology. Do not query its SQL DB directly from another service or bypass the gateway merely for convenience.

If Audit is not enabled, rely on structured telemetry/logs/traces and service-owned support endpoints that are explicitly allowed.

## 7. Produce a causal timeline

Output ordered entries like:

```text
12:00:00.123  Gateway POST /api/...         correlation C
12:00:00.180  Customers API committed       entity E, outbox event M1
12:00:01.010  Customers outbox relay        M1 published
12:00:01.090  Lifecycle Function received   M1, causation M1
12:00:01.145  Lifecycle DB committed         inbox M1 + state change + outbox M2
12:00:02.003  Lifecycle outbox relay         M2 published
...
```

Clearly label missing evidence, retries and inferred causal links.

## 8. Never mutate while tracing

Do not:

- replay a message;
- mark outbox/inbox rows;
- repair data;
- dead-letter/complete a message;
- edit a customer record;
- change retry configuration.

If repair is needed, finish the investigation first and propose a separate controlled operation.

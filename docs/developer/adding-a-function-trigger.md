# Adding an Azure Function Trigger

Project Chicago Functions are asynchronous transport adapters. They are not mini-services.

## Allowed trigger categories

- `TimerTrigger` — outbox relay scheduling.
- `ServiceBusTrigger` — integration-event consumption.

HTTP-triggered Functions are outside the architecture.

## Service Bus consumer shape

```text
ServiceBusTrigger
 → deserialize/version check
 → establish/extract trace + correlation context
 → owning Facade
 → Business
 → Data
 → Repository/DbContext
 → inbox/idempotent commit
```

The Function must not inject Repository/DbContext and implement the use case itself.

## Failure behavior

Unexpected/transient failures should fail the Function invocation so Functions/Service Bus retry/dead-letter behavior remains visible. Do not catch an exception, log it, and return success.

## Timer relay shape

```text
TimerTrigger
 → IOutboxRelay
 → lease bounded pending batch
 → publish via configured Service Bus client
 → mark each confirmed send dispatched
```

No event-type business switch belongs in the trigger.

## Trace propagation

For Service Bus:
- extract W3C trace context when available,
- preserve CorrelationId/CausationId/EventId,
- create/link current Activity according to OTel integration,
- never copy message body into telemetry tags.

## Tests

Function adapter tests should prove:
- valid binding delegates once,
- cancellation propagates,
- correlation context propagates,
- unexpected exception propagates,
- unsupported/malformed contract follows approved poison policy.

Core/SQL integration tests separately prove idempotency and local transaction behavior.

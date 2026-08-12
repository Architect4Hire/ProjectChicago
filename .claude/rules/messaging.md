---
paths:
  - "src/ProjectChicago.Contracts/**"
  - "src/ProjectChicago.Shared/**"
  - "src/services/ProjectChicago.*.Functions/**"
  - "src/services/ProjectChicago.*.Core/**"
---
# Messaging, outbox and inbox rules

Azure Service Bus connects bounded services. Message processing is Functions-based; reliability remains transactional outbox + idempotent inbox.

## Integration events

- Event contracts live in `ProjectChicago.Contracts`.
- Name facts in past tense: `<Entity><ActionPastTense>` or another stable fact-oriented convention.
- Events carry a stable event/message ID.
- Carry correlation and causation metadata in the shared event envelope/contract convention.
- Include occurred-at UTC and contract version when the envelope standard requires it.
- Include only data needed by consumers. Do not serialize EF entities or leak entire service-owned aggregates "just in case."
- Treat event contracts as externally consumed APIs: additive changes are preferred; breaking changes require versioning/migration.

## Transactional outbox — publish side

For a mutation that emits an event:

```text
Controller/Function
  -> Facade
    -> Business decides event fact
      -> Data transaction
         -> repository/domain persistence
         -> OutboxMessages insert
         -> COMMIT

TimerTrigger
  -> OutboxRelay
    -> Service Bus
    -> mark dispatched after publish succeeds
```

Rules:

- Business can construct/return the event fact but cannot send it.
- Data persists event + state atomically.
- The API response may complete after the DB commit; Service Bus publish is asynchronous through the relay.
- A failed Service Bus publish must not roll back already committed domain data; it remains a pending outbox item.
- Relay selection/lease must prevent uncontrolled duplicate concurrent dispatch. Even with relay protections, consumers remain idempotent.
- Store enough metadata to serialize/publish a versioned contract deterministically.

## Timer-triggered relay

- There is no generic hosted `OutboxDispatcher : BackgroundService`.
- A shared `IOutboxRelay`/equivalent mechanism may encapsulate batch selection, lease/lock, publish and dispatch marking.
- Each publishing service's Functions project owns the timer entry point and resolves the relay against **that service's** DbContext/store.
- Schedule is configuration, not a magic string copied across Function classes.
- Batch size, lease timeout and retry policy are operational settings with safe defaults and telemetry.

## Inbox — consume side

For Service Bus-triggered consumption:

```text
ServiceBusTrigger
  -> event envelope/correlation adapter
    -> owning service Facade -> Business -> Data
       -> detect/register Inbox message
       -> apply side effects
       -> mark Inbox complete in the required transaction
```

- Duplicate `MessageId/EventId` already completed => safe no-op.
- In-progress/stale handling must have a defined lease/recovery strategy if concurrent delivery is possible.
- Inbox rows are service-owned SQL data, not a central cross-service inbox database.
- A downstream event emitted by a consumed event uses the consumer service's own outbox in the same transaction as its new side effects where atomicity is required.

## Service Bus topology

The precise Project Chicago topic/subscription naming scheme is an open architecture decision. Until selected:

- Do not hardcode a topology in domain code.
- Keep entity names in AppHost/deployment configuration and Function binding settings.
- Consumer subscriptions belong to the consumer's deployment/infrastructure definition.
- Use subscription filters only when they simplify a deliberate topology; do not encode core domain rules as broker filters that are invisible to application tests.

## Failure semantics

Classify failures:

- **Transient infrastructure**: throw/fail invocation; platform retries.
- **Duplicate already completed**: no-op/complete with telemetry.
- **Contract cannot deserialize/version unsupported**: follow explicit poison/dead-letter policy; do not retry forever silently.
- **Permanent domain rejection**: explicit policy required; log structured reason and decide complete/dead-letter/compensate based on use case.
- **Unexpected bug**: fail; do not swallow.

## Correlation

- Originating HTTP request creates/accepts a correlation ID according to the project's edge policy.
- Event caused by request: same CorrelationId, CausationId points to request/current message identifier as defined by the envelope convention.
- Event caused by an event: preserve CorrelationId and set CausationId to the incoming event/message ID.
- Logs and traces use the same values.

## Security and privacy

- No secrets in event payloads.
- Avoid unnecessary PII; send stable identifiers when the consumer can operate without a full customer snapshot.
- Do not log raw payloads by default.
- Broker authorization should be least privilege: Function apps need only the entities/actions required by their triggers/publish relay.

## Test matrix for every event seam

Publish side:

- [ ] state + outbox commit together
- [ ] rollback removes both
- [ ] contract fields/version correct
- [ ] relay successful publish marks dispatched
- [ ] relay failed publish remains retryable

Consume side:

- [ ] valid event applies expected side effect
- [ ] duplicate delivery does not repeat side effect
- [ ] failure does not mark inbox complete
- [ ] emitted follow-on event uses consumer's outbox if transactional
- [ ] correlation/causation preserved
- [ ] unsupported/bad contract follows defined poison policy

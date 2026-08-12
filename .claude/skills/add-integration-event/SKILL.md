---
name: add-integration-event
description: Add or change a Project Chicago cross-service integration event end to end: Contracts record, publisher business decision, SQL Server transactional outbox, timer-triggered relay, Service Bus topology/config, consumer ServiceBusTrigger Function, inbox idempotency, correlation, tests and contract review.
---
# Add an integration event end to end

An integration event is a cross-service contract, not a shortcut around service ownership. Implement both sides deliberately.

Read:

- `.claude/rules/messaging.md`
- `.claude/rules/functions.md`
- `.claude/rules/backend.md`
- `.claude/rules/database.md`
- `.claude/rules/aspire.md`

## 1. Confirm the event is the right boundary

Write down:

- publishing service/owner;
- committed business fact;
- consuming service(s);
- why async/eventual consistency is acceptable;
- what consumer actually needs to act.

Do not create an event just to let one service perform a synchronous validation against another service's data. Do not include a whole aggregate when a stable ID + small fact is enough.

## 2. Name/version the contract

Use a past-tense fact name. Put the record/interface in `ProjectChicago.Contracts`.

Typical envelope concerns:

- Event/Message Id
- Event Type
- Version
- OccurredAtUtc
- CorrelationId
- CausationId
- Actor/Tenant metadata only if standardized/needed
- fact-specific identifiers/data

Do not serialize EF entities, navigation properties or internal repository models.

For a breaking change, introduce a versioning/migration strategy; do not silently reinterpret old payloads.

## 3. Publisher business decision

In the publishing service Business layer:

- create the event fact only after business rules determine the mutation is valid;
- populate fact-specific fields from service-owned data;
- thread correlation/causation through the established envelope/context;
- return/attach event(s) to the Data mutation contract.

Do not inject Service Bus SDK into Business.

## 4. Publisher Data transaction + outbox

Persist:

```text
BEGIN
  domain/service data mutation
  OutboxMessages(event envelope/payload)
COMMIT
```

Validate:

- same DbContext/transaction;
- rollback removes both;
- outbox EventId is stable;
- payload is deterministic/versioned;
- SQL Server schema/types are valid.

## 5. Outbox relay path

Ensure the publishing service has a timer-triggered outbox relay Function. If not, use `add-function-trigger`.

The relay:

- publishes pending event to Service Bus;
- marks dispatched only after publish succeeds;
- leaves failure retryable;
- handles duplicate relay attempts safely;
- emits structured telemetry.

There is no `BackgroundService` dispatcher.

## 6. Service Bus infrastructure/config

Using `add-aspire-resource` as needed:

- declare/configure the topic/queue/subscription according to the approved Project Chicago topology;
- configure consumer subscription/filter only when deliberate;
- wire publishing Functions app to send and consuming Functions app to receive;
- do not grant the React app/gateway/service DB access to Service Bus;
- keep names/config in infrastructure/app settings, not domain code.

If topology convention is still undecided, do not invent a permanent naming scheme as part of an unrelated event.

## 7. Consumer Function

In each consuming service's `.Functions` project:

- bind Service Bus message;
- deserialize the Contracts type/envelope;
- establish correlation/message context;
- call the service Facade;
- fail invocation when processing fails unexpectedly/transiently.

The Function must not contain consumer business logic or repositories.

## 8. Consumer inbox + side effects

Within the service-owned `.Core` path:

1. detect message ID already completed -> safe no-op;
2. otherwise process Business/Data action;
3. commit inbox completion with side effects using the service's defined transaction strategy;
4. if processing emits another integration event, write it to **consumer service's** outbox atomically with its side effects;
5. on failure, do not record completion.

Test duplicate delivery explicitly.

## 9. Correlation and causation

- Preserve original CorrelationId across the event chain.
- Set CausationId according to Project Chicago's envelope rule (normally incoming request/message/event ID that caused this event).
- Ensure logs on publish, relay, trigger and service processing can be queried by the same correlation ID.

## 10. Failure policy

Document/implement for this event seam:

- transient SQL/broker failure -> retry by platform;
- duplicate completed -> no-op;
- unsupported version/deserialization poison -> configured dead-letter/manual investigation path;
- permanent domain rejection -> explicit complete/dead-letter/compensation policy;
- bug -> fail and surface.

Do not catch everything and complete.

## 11. Tests

Publisher:

- event emitted only for correct business outcome;
- event fields/thread correct;
- state + outbox commit/rollback atomic.

Relay:

- publishes correct envelope/entity;
- marks on success only;
- failed publish retryable.

Consumer Function:

- binds expected event;
- delegates correct context;
- propagates failure.

Consumer Core:

- first delivery side effect;
- duplicate delivery no duplicate side effect;
- failed processing no inbox completion;
- follow-on event transaction if any.

Contract:

- run `api-contract-checker`.

## 12. Review summary

When done, summarize the entire seam in one compact chain:

```text
<Publisher mutation>
 -> <Event vN> in outbox
 -> <Publisher>.Functions timer relay
 -> <Service Bus entity>
 -> <Consumer>.Functions ServiceBusTrigger
 -> <Consumer facade/business/data>
 -> <Consumer SQL/inbox + optional outbox>
```

## Completion checklist

- [ ] Event is a real cross-service fact.
- [ ] Contract is in Contracts, versionable, minimal.
- [ ] Publisher state + outbox atomic.
- [ ] Timer relay exists; no hosted dispatcher.
- [ ] Service Bus configuration centralized.
- [ ] Consumer is Function trigger, not API-host processor.
- [ ] Consumer uses persistent inbox/idempotency.
- [ ] Correlation/causation preserved.
- [ ] Failure/dead-letter semantics explicit.
- [ ] Both sides tested.

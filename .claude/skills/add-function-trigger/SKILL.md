---
name: add-function-trigger
description: Add an Azure Function entry point to an existing Project Chicago bounded service. Supports Service Bus triggers for integration events and timer triggers for outbox/recurring asynchronous work. Keeps Function classes thin and delegates to the service `.Core`; never creates hosted workers.
---
# Add an Azure Function trigger

Use this skill when async work belongs in `ProjectChicago.<Service>.Functions`.

Read:

- `.claude/rules/functions.md`
- `.claude/rules/messaging.md` for Service Bus/outbox work
- `.claude/rules/backend.md`
- `.claude/rules/database.md`
- `.claude/rules/aspire.md` when project/resource wiring changes

## 1. Decide trigger type and owner

Choose the service that owns the resulting state/decision.

### Service Bus trigger

Use when another bounded context publishes a fact Project Chicago must react to asynchronously.

### Timer trigger

Use for scheduler-driven infrastructure/application work such as draining a service outbox. A timer is not permission to put arbitrary cron business logic in the Function class; the owning `.Core` still implements the use case.

If the trigger needs to mutate two service databases, ownership is unresolved — stop and surface the architectural problem.

## 2. Verify/create `.Functions` project

Expected sibling:

```text
ProjectChicago.<Service>/
ProjectChicago.<Service>.Core/
ProjectChicago.<Service>.Functions/
```

The Functions project should be .NET isolated worker and reference only:

- its own `.Core`;
- Contracts;
- Shared mechanisms needed for binding/envelopes/telemetry.

Do not reference another service Core.

Before changing package versions or Aspire Functions APIs, verify current official Microsoft/Aspire guidance.

## 3A. Service Bus trigger recipe

### Contract

Use an existing versioned integration event in `ProjectChicago.Contracts`. If the event does not exist, use `add-integration-event` rather than inventing a local deserialization type.

### Binding/config

- Entity/topic/subscription names are app settings/configuration.
- Connection/resource name is injected through Aspire/Azure config.
- Do not commit credentials to `local.settings.json`.
- Keep the Function attribute/configuration consistent with the project's topology/naming convention.

### Trigger body

The Function body should look conceptually like:

```text
bind message
 -> deserialize/validate event envelope
 -> establish message/correlation context
 -> call owning facade
 -> return success only after service work succeeds
```

No repository/DbContext access in the Function.

### Idempotency

Idempotency must survive process restarts and duplicate broker delivery. It belongs in persistent inbox/service processing, not a static in-memory set.

Ensure:

- event/message ID is passed to the `.Core` processing path;
- duplicate completed message is a no-op;
- failed side effects do not mark it complete;
- follow-on transactional events use this service's outbox.

### Errors

Do not write:

```csharp
try { await Handle(); }
catch (Exception ex) { logger.LogError(ex, "failed"); }
```

if returning from that catch tells Functions/Service Bus the message succeeded. Unexpected/transient failures must fail invocation unless explicit settlement/dead-letter behavior says otherwise.

## 3B. Timer-triggered outbox relay recipe

Use a thin trigger around shared relay infrastructure.

```text
TimerTrigger
 -> IOutboxRelay.DrainAsync(serviceStore, cancellation)
```

The relay (not Function class) owns:

- selecting a bounded batch of pending rows;
- lease/concurrency strategy;
- deserializing event envelope;
- publishing through Service Bus SDK;
- marking successfully sent rows dispatched;
- leaving failed rows retryable;
- metrics/logging for batch outcome.

The Function owns only schedule + delegation.

Requirements:

- schedule is configurable;
- no infinite polling loop;
- no domain-specific event switch in the timer Function;
- no mark-dispatched-before-send;
- batch size avoids monopolizing one Function invocation;
- cancellation is honored.

## 4. Facade/Core handler

For a Service Bus event, add/reuse an event-specific Facade method or application handler that enters the same `.Core` stack:

```text
Function -> Facade -> Business -> Data -> Repository -> SQL Server
```

Avoid creating a `Consumers/` business layer in `.Functions`. The Functions directory is transport, not implementation.

## 5. AppHost resource wiring

Add the Functions project to AppHost using the current Aspire Functions integration.

Give it only:

- its service DB resource;
- Service Bus resource/entity configuration;
- cache/other resource if the specific service handler truly uses it.

The API sibling does not automatically receive these references.

Use `WaitFor`/current ordering semantics where the Functions project depends on local emulator/database readiness.

## 6. `host.json` / local configuration

- Keep global Functions tuning intentional and documented.
- Do not solve one slow handler by globally increasing retries/concurrency.
- Check Service Bus prefetch/concurrency/auto-complete/manual settlement settings against the chosen extension version before changing them.
- Never commit real Service Bus/SQL secrets.

## 7. Tests

### Service Bus Function adapter

- correct contract deserializes;
- facade called with event/message/correlation context;
- invalid contract follows policy;
- facade exception is not swallowed;
- cancellation propagates.

### Core processing

- first delivery applies side effect;
- duplicate delivery does not repeat it;
- failed processing does not complete inbox;
- transaction semantics correct;
- follow-on outbox event atomic if emitted.

### Timer/relay

- timer delegates;
- empty batch;
- successful publish -> dispatched;
- failed publish -> not dispatched/retryable;
- partial batch;
- concurrency/lease if implemented.

## 8. Review

Run:

- `function-boundary-checker`
- `test-gap-analyzer`
- `code-reviewer`
- `api-contract-checker` for event consumers

## Completion checklist

- [ ] Correct owning service selected.
- [ ] Trigger lives in sibling `.Functions` project.
- [ ] .NET isolated worker.
- [ ] Function is transport-only.
- [ ] Same `.Core` Facade → Business → Data path used.
- [ ] Persistent idempotency for Service Bus consumer.
- [ ] Failure semantics preserve retries/dead-letter behavior.
- [ ] Config/credentials injected.
- [ ] AppHost resource references are least privilege.
- [ ] No hosted worker was added.

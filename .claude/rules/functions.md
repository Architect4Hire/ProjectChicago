---
paths:
  - "src/services/ProjectChicago.*.Functions/**"
  - "tests/ProjectChicago.*.Functions.Tests/**"
---
# Azure Functions rules

Project Chicago uses Azure Functions for asynchronous entry points that were previously hosted background workers/processors. Functions are transport adapters around the owning service `.Core`, not a new layer of domain behavior.

## Project model

- Use .NET 10-compatible Azure Functions 4.x isolated worker packages/current supported versions.
- Keep exactly one `.Functions` project per bounded service. Each is deployed as its own Azure Function App on **Flex Consumption** unless a future ADR explicitly changes the architecture.
- The Functions project references:
  - its own `ProjectChicago.<Service>.Core`;
  - `ProjectChicago.Contracts` for event contracts;
  - Shared mechanisms needed for event envelopes, telemetry, outbox relay, etc.
- It never references another service `.Core`.

## Flex Consumption deployment constraints

- Flex Consumption is the confirmed production hosting plan. Keep Functions stateless between invocations and assume instances can scale independently.
- Do not design deployment automation around Function App deployment slots; Flex Consumption does not currently support deployment slots.
- Prefer identity-based Azure connections/managed identity for production Service Bus and host-storage access when supported by the selected binding/infrastructure implementation; do not commit connection strings.
- Tune concurrency against SQL capacity and downstream limits. Flex scaling is not permission to make every handler maximally concurrent.
- Function projects in Project Chicago are asynchronous adapters. Do not add HTTP triggers for application APIs; YARP + service HTTP hosts own HTTP traffic.

## Function categories

### Service Bus trigger

Use for incoming integration events.

Trigger class responsibilities:

1. Receive/bind the Service Bus message.
2. Deserialize a versioned event contract using the shared serializer/envelope convention.
3. Populate correlation/causation/message context.
4. Delegate to the owning service Facade/use case.
5. Return/settle according to successful processing semantics.

Do not put CRM decision logic, EF queries, cache policy or cross-service calls directly in the Function class.

Pseudocode shape:

```csharp
public sealed class CustomerChangedFunction
{
    private readonly ICustomerEventFacade _facade;

    [Function(nameof(CustomerChangedFunction))]
    public async Task RunAsync(
        [ServiceBusTrigger("%CustomerEventsTopic%", "%LifecycleSubscription%", Connection = "messaging")]
        ServiceBusReceivedMessage message,
        CancellationToken cancellationToken)
    {
        // transport/envelope adaptation only
        var evt = ...;
        await _facade.HandleAsync(evt, ..., cancellationToken);
    }
}
```

Exact binding types/options must be verified against current official Functions Service Bus extension documentation when implementation occurs.

### Timer trigger — outbox relay

Use to replace the hosted background outbox dispatcher.

The Function:

- wakes on a configuration-controlled schedule;
- calls a reusable relay service for **its own service database**;
- does not contain polling SQL or event-specific business logic;
- publishes pending outbox messages through the shared Service Bus publisher;
- marks a message dispatched only after successful broker publish;
- leaves/retries failed records according to the relay's retry/lease policy;
- emits structured relay metrics/logs.

Do not implement an infinite loop inside the Function. The timer invocation is the scheduling unit.

## Idempotency and inbox

- Assume Service Bus delivery is at least once from the application's perspective; duplicate processing must be harmless.
- Use message/event ID as an inbox idempotency key within the owning service database.
- A duplicate already completed should become a safe no-op with useful telemetry.
- Do not mark inbox completion until the service-owned side effects commit.
- If business processing fails, fail the invocation; do not mark it completed merely to suppress retries.
- Decide poison-message/manual settlement behavior at the messaging configuration layer, not ad hoc per Function unless a use case requires special handling.

## Concurrency and retries

- Function concurrency must respect SQL transaction/connection capacity and downstream rate limits; do not maximize concurrency blindly.
- Do not add application retry loops that multiply Service Bus/Functions retries without a reason.
- Retriable broker/database failures should preserve the message for platform retry.
- Non-retriable domain rejections require an explicit handling policy (record/complete/dead-letter) so they do not spin forever.

## Configuration

- Binding entity names and connection settings come from app settings/Aspire/Azure configuration.
- No `local.settings.json` secrets are committed.
- Prefer managed identity/identity-based production connections for Service Bus and host storage. Exact resource identity/RBAC wiring belongs to infrastructure configuration.
- `host.json` tuning is centralized and documented. Do not tweak global retry/concurrency settings to solve a single handler bug.

## Observability

Each invocation should be traceable by:

- Function name
- owning service
- event type/version
- Service Bus message ID
- correlation ID
- causation ID
- attempt/delivery metadata when available
- outcome and duration

Do not log raw connection strings, access tokens, full message bodies by default, or customer-sensitive payloads not needed for diagnostics.

## Tests

Test:

- valid event delegates exactly once to the facade;
- invalid/unrecognized contract behavior;
- duplicate delivery is idempotent at the service persistence seam;
- facade failure does not get converted to success;
- correlation/causation propagation;
- timer Function delegates to relay and respects cancellation;
- relay marks dispatched only after successful publish;
- relay leaves a failed publish retryable;
- event processing and inbox side effects are transactionally correct where required.

## Forbidden patterns

- `BackgroundService` or `IHostedService` for Service Bus/outbox work.
- HTTP-triggered Functions used as a browser/application API bypassing YARP/service hosts.
- deployment workflows that require Function deployment slots on Flex Consumption.
- Function -> Repository or Function -> DbContext direct calls for business processing.
- Function -> another service `.Core`.
- Function class containing lifecycle state-machine logic.
- direct event publish from a consumer handler that should be part of a transaction without using its own outbox.
- catch-all exception blocks that log and return success.

# Azure Functions as Asynchronous Entry Points

Project Chicago intentionally uses Functions instead of Aspire-hosted background workers.

## Hosting
- .NET isolated worker.
- Flex Consumption production target.
- sibling `.Functions` project per bounded service.

## Trigger design
`TimerTrigger` schedules outbox relay. `ServiceBusTrigger` consumes integration events.

Function classes do not own business rules or persistence. They adapt trigger data to Facade calls.

## Credentials
Grant only the resource references/RBAC needed by that Functions project. A publisher relay may need DB + Service Bus Send; a consumer may need Service Bus Listen + owning DB.

## Error handling
Let unexpected failure fail the invocation. Platform retry/dead-letter behavior must see it. Do not swallow failures to make a green Function execution.

## Anti-patterns
- HTTP-trigger Function as a second public API.
- `BackgroundService` for Service Bus.
- Function switching on many domain event types and implementing rules inline.
- direct cross-service Core/DbContext references.

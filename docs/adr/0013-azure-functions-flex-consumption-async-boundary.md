# ADR-0013 — Azure Functions for Asynchronous Entry Points

- **Status:** Accepted
- **Requirements:** ASYNC-001..008, DEPLOY-004

## Context
The architecture requires event/timer processing without Aspire-hosted background workers and targets Azure Functions Flex Consumption in production.

## Decision
Use .NET isolated Azure Functions. Each bounded service has a sibling `.Functions` project. Service Bus consumers use `ServiceBusTrigger`; outbox relays use `TimerTrigger`.

HTTP-triggered Functions are not part of the service API surface. Functions remain transport adapters and delegate into the owning service Core/Facade.

## Consequences
- Function code must be thin and independently testable.
- Scaling/retry/dead-letter behavior aligns with Azure Functions + Service Bus.
- Dependency injection and OpenTelemetry must work in isolated worker.
- Service credentials can be scoped separately from HTTP hosts.

## Validation
Function-boundary tests reject direct Repository/DbContext business logic; deployment configuration is Flex Consumption compatible.

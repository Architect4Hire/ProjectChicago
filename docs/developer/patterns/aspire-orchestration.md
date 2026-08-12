# Aspire Orchestration

AppHost declares local resources and dependencies; it is not an application service.

## Resources
As implementation proceeds:
- SQL Server + one database per service,
- Azure Service Bus local/emulator topology,
- service HTTP hosts,
- Functions projects,
- YARP,
- React/Vite app.

## ServiceDefaults
Centralizes standard health/OpenTelemetry setup.

## Rules
- no business logic in AppHost,
- no hard-coded ports in feature code,
- resource references reflect least privilege,
- verify current Aspire APIs during implementation because versions evolve.

## Production
Aspire local topology is not the production IaC decision. See ADR-0020.

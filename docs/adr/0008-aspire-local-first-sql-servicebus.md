# ADR-0008 — Aspire Local-First Orchestration

- **Status:** Accepted
- **Requirements:** DEPLOY-001..005, OPS-001..004

## Context
Developers need a reproducible local topology that resembles production dependencies while retaining a single orchestration entry point and local telemetry.

## Decision
Use .NET Aspire AppHost for local orchestration and ServiceDefaults for standard health/OpenTelemetry configuration. AppHost declares SQL Server databases, Azure Service Bus local/emulator resources, service hosts, Function projects, gateway and React app as the implementation matures.

AppHost remains declarative orchestration; business logic does not live there.

## Consequences
- Local dependency wiring is visible in the Aspire Dashboard.
- Service discovery/configuration replaces hard-coded ports.
- Current Aspire APIs must be verified when prompts execute because the product evolves quickly.
- Production infrastructure is not automatically implied by AppHost; IaC is separate (ADR-0020).

## Validation
`aspire run`/AppHost model inspection proves resources and references; services expose health and telemetry.

# Lifecycle CRM Claude Toolkit

This toolkit translates the JobBoard Claude workflow into a controller-based modular API with a secondary
Domain project and a strict onion call chain.

## Required architecture

```text
<api-project> (MVC Controllers + HTTP contracts + composition)
        |
        v
<domain-project>
  Facade   -> validation, record access, cache check/invalidation, orchestration
     |
     v
  Business -> CRM rules and Facade/Data model translation
     |
     v
  Data     -> EF Core, SQL Server, queries, commands, transactions, migrations
```

Every call moves one layer downward only: Controller -> Facade -> Business -> Data. Models are distinct at
each seam. No controller calls Business/Data; no Facade calls Data; no Business uses EF; no Data calls up.

## Skills

- `add-controller-endpoint` — detailed MVC controller and full onion-path playbook.
- `add-crm-module` — creates one area with Facade/Business/Data boundaries and architecture tests.
- `trace-request` — traces Angular -> Controller -> Facade -> Business -> Data -> SQL Server.
- `plan-microstep` — separates work into one-action prompts.
- `add-lifecycle-stage`, `add-audit-event`, `add-database-migration`, `add-dashboard-metric`,
  `add-angular-feature`, `update-angular-api-client`, and `run-quality-gate` — focused supporting playbooks.

## Rules

`backend.md`, `onion-boundaries.md`, and `data.md` are the authoritative server-side constraints. The API
project is HTTP-only; the Domain project owns all three lower layers.

## Architectural substitutions from JobBoard

Removed: microservices, YARP, Service Bus, outbox/inbox, service-to-service HTTP, minimal APIs, and separate
service databases.

Replaced with: MVC controllers, one API host, one Domain project, one SQL Server/Azure SQL Database (SQLDB), strict
Controller -> Facade -> Business -> Data dependencies, cache coordination in Facade, translation/rules in
Business, and persistence in Data.


## Required orchestration

.NET Aspire is required. The AppHost is the supported local entry point and must orchestrate the API, Angular application, SQL Server database resource, health checks, telemetry, and dependency ordering. Do not add direct developer connection strings as a substitute.

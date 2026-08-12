# Project Chicago — Proposed Solution Structure

This document translates the architecture into a concrete repository layout. It separates **fixed structural rules** from the **recommended but not yet accepted** initial bounded-context catalog.

## Fixed repository shape

```text
/
├── CLAUDE.md
├── .claude/
├── README.md
├── docs/
│   ├── README.md
│   ├── adr/
│   ├── design/
│   ├── developer/
│   ├── prompts/
│   └── requirements/
├── src/
│   ├── ProjectChicago.AppHost/
│   ├── ProjectChicago.ServiceDefaults/
│   ├── ProjectChicago.Gateway/
│   ├── ProjectChicago.Contracts/
│   ├── ProjectChicago.Shared/
│   ├── services/
│   │   └── ProjectChicago.<Service>/
│   │       ├── ProjectChicago.<Service>/
│   │       ├── ProjectChicago.<Service>.Core/
│   │       └── ProjectChicago.<Service>.Functions/
│   └── web/
│       ├── package.json
│       └── src/
│           ├── api/
│           ├── app/
│           ├── design-system/
│           ├── features/
│           └── index.css
└── tests/
```

## Recommended initial bounded contexts

ADR-0015 proposes:

```text
src/services/
├── ProjectChicago.Crm/
│   ├── ProjectChicago.Crm/
│   ├── ProjectChicago.Crm.Core/
│   └── ProjectChicago.Crm.Functions/
├── ProjectChicago.Identity/
│   ├── ProjectChicago.Identity/
│   ├── ProjectChicago.Identity.Core/
│   └── ProjectChicago.Identity.Functions/
└── ProjectChicago.Audit/
    ├── ProjectChicago.Audit/
    ├── ProjectChicago.Audit.Core/
    └── ProjectChicago.Audit.Functions/
```

Until ADR-0015 is accepted, treat those names as a recommendation, not an established fact.

## Core project layout

Each `.Core` library keeps the architecture arrow explicit:

```text
ProjectChicago.<Service>.Core/
├── Facades/
├── Business/
├── Data/
├── Repositories/
├── Persistence/
│   ├── <Service>DbContext.cs
│   ├── Configurations/
│   └── Migrations/
├── Models/
├── Contracts/              # service-local internal models, not shared integration events
├── Mapping/
└── Validation/
```

Runtime call direction:

```text
Facade → Business → Data → Repository → DbContext
```

Rules:

1. Controller calls Facade only.
2. Function calls Facade only.
3. Facade does not access EF or Service Bus.
4. Business owns business rules and model translation.
5. Data owns transaction boundaries.
6. Repository owns persistence/query mechanics.
7. `DbContext` belongs to the owning bounded service.
8. No cross-service `.Core` reference.
9. No cross-service database access.

## Shared and Contracts

`ProjectChicago.Contracts` is the leaf for stable integration-event contracts and envelope primitives.

`ProjectChicago.Shared` contains cross-cutting **mechanisms**, such as correlation context, ProblemDetails helpers, outbox/inbox persistence primitives, serialization and reusable relay infrastructure. It must not become a shared business-domain project.

Reference direction:

```text
Contracts ← Shared ← <Service>.Core ← Host / Functions ← AppHost
```

## Web

The React application talks only to YARP.

```text
src/web/src/
├── api/                    # typed gateway-facing API modules
├── app/                    # routing, composition, auth/session state
├── design-system/          # copied local PCDS source of truth
└── features/
    ├── clients/
    ├── projects/
    ├── tasks/
    ├── dashboard/
    └── search/
```

Feature code consumes PCDS; it does not recreate shared tokens or recipes.

## Tests

Recommended organization:

```text
tests/
├── ProjectChicago.Architecture.Tests/
├── services/
│   ├── ProjectChicago.Crm.Core.Tests/
│   ├── ProjectChicago.Crm.Api.Tests/
│   ├── ProjectChicago.Crm.Functions.Tests/
│   ├── ProjectChicago.Identity.Core.Tests/
│   ├── ProjectChicago.Identity.Api.Tests/
│   ├── ProjectChicago.Identity.Functions.Tests/
│   ├── ProjectChicago.Audit.Core.Tests/
│   ├── ProjectChicago.Audit.Api.Tests/
│   └── ProjectChicago.Audit.Functions.Tests/
└── web/                    # tests normally colocated/configured by the React project
```

SQL behavior is proven against SQL Server-compatible infrastructure, not EF Core InMemory.

## Async boundary

No service contains a request-path `BackgroundService` for outbox or Service Bus work.

```text
<Service>.Functions
├── <Service>OutboxTimerFunction.cs
└── <Event>ServiceBusFunction.cs
```

Only publishing services need an outbox timer. Only consuming services need corresponding Service Bus triggers. Empty Functions projects are acceptable until a bounded service has async responsibilities.

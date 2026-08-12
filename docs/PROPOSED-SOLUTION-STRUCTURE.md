# Proposed Solution Structure

This is structural guidance, not a request to scaffold unnamed CRM services.

```text
/
├── CLAUDE.md
├── .claude/
├── src/
│   ├── ProjectChicago.AppHost/
│   ├── ProjectChicago.ServiceDefaults/
│   ├── ProjectChicago.Gateway/
│   ├── ProjectChicago.Contracts/
│   ├── ProjectChicago.Shared/
│   ├── services/
│   │   ├── ProjectChicago.<Service>/
│   │   │   ├── Controllers/
│   │   │   └── Program.cs
│   │   ├── ProjectChicago.<Service>.Core/
│   │   │   ├── Facades/
│   │   │   ├── Business/
│   │   │   ├── Data/
│   │   │   ├── Repositories/
│   │   │   ├── Persistence/
│   │   │   ├── Models/
│   │   │   ├── Mapping/
│   │   │   └── Validation/
│   │   └── ProjectChicago.<Service>.Functions/   # deploys as Flex Consumption Function App
│   │       ├── Functions/
│   │       │   ├── ServiceBus/
│   │       │   └── Timers/
│   │       ├── Program.cs
│   │       └── host.json
│   └── web/
│       ├── src/
│       │   ├── design-system/   # copied local PCDS source (authoritative)
│       │   ├── features/
│       │   ├── api/
│       │   ├── app/
│       │   └── index.css        # local PCDS token/theme layer
│       └── package.json
└── tests/
    ├── ProjectChicago.<Service>.Core.Tests/
    ├── ProjectChicago.<Service>.Api.Tests/
    ├── ProjectChicago.<Service>.Functions.Tests/
    └── web/ (or colocated frontend tests, depending chosen tooling)
```

## Request path

```text
React 19
  -> YARP Gateway
    -> Service HTTP Controller
      -> Facade
        -> Business
          -> Data
            -> Repository
              -> Service-owned SQL Server database
```

## Event publish path

```text
Controller or ServiceBus Function
  -> Facade -> Business -> Data
    -> SQL transaction
       - domain persistence
       - OutboxMessages insert

TimerTrigger Function
  -> reusable OutboxRelay
    -> Azure Service Bus publish
    -> mark outbox dispatched only after successful publish
```

## Event consume path

```text
Azure Service Bus
  -> ServiceBusTrigger Function
     -> correlation/event-envelope adapter
     -> service Facade -> Business -> Data -> Repository
        -> InboxMessages idempotency + domain side effects in service-owned SQL
```

The Function is a transport adapter, not a parallel service implementation.

## Identity placement

ASP.NET Core Identity is confirmed, but its bounded-service/database owner is intentionally not shown above because the service catalog is not defined. When ownership is decided, Identity tables/migrations belong only to that service's Microsoft SQL database. Browser authentication/account traffic still enters through YARP.

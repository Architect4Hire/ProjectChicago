---
paths:
  - "src/ProjectChicago.AppHost/**"
  - "src/ProjectChicago.ServiceDefaults/**"
---
# Aspire orchestration rules

Project Chicago uses Aspire as the source of truth for local distributed-application composition. AppHost declares resources and relationships; it does not host business behavior.

## Required resource model

- Model Microsoft SQL Server with the Aspire SQL Server hosting integration. Use one local SQL Server resource and add one database resource per bounded service unless the architecture explicitly selects separate server resources.
- Model Azure Service Bus using the Aspire Azure Service Bus integration/emulator appropriate to the current Aspire version.
- Add each HTTP API host as a project resource.
- Add each `ProjectChicago.<Service>.Functions` project as an Azure Functions project resource using the current Aspire Azure Functions hosting integration. Each project maps to its own production Flex Consumption Function App.
- Add YARP gateway as a project resource.
- Add the client-side React app as a JavaScript/Vite resource and give it only the gateway reference/configuration it needs.
- Add cache/other resources only for an explicit use case.

## Dependency wiring

- HTTP API host: reference its own service database and only infrastructure it directly uses. Do **not** give it Service Bus credentials merely because the sibling Functions app publishes/consumes events.
- Service Functions app: reference its own service database, Service Bus, and any service-owned infrastructure required by its triggers.
- Gateway: reference API hosts it routes to; it must not reference service databases.
- React app: reference/expose only the gateway address/config. It must never receive service database, Service Bus or internal service endpoints.
- Use `WithReference`/current equivalent and `WaitFor` where startup ordering matters. Do not hardcode ports or connection strings.

## Azure Functions integration

- Verify current official Aspire + Azure Functions APIs before scaffolding because package names and APIs can change quickly.
- Functions are .NET 10 isolated worker projects. Production hosting is Flex Consumption; verify current Aspire/Azure deployment APIs before emitting version-sensitive deployment code.
- AppHost may orchestrate Functions locally; this does not turn them into API-host background workers.
- No `BackgroundService` is added to AppHost or API projects to simulate Functions behavior.
- Prefer narrow per-service Function project references over a single Function project that can reach every database.

## SQL Server

Typical intent, not a copy/paste guarantee for future package versions:

```csharp
var sql = builder.AddSqlServer("sql")
    .WithLifetime(ContainerLifetime.Persistent);

var customerDb = sql.AddDatabase("customerdb");
```

A service project then references only its database resource. Use official current Aspire APIs when implementation begins.

## Service Bus

- Entity topology must be declared centrally and named consistently.
- Topic/subscription/queue names are infrastructure configuration, not domain constants scattered through trigger classes.
- If the local Service Bus emulator has limitations relative to Azure, document those gaps and cover critical behavior with an Azure integration environment rather than hiding the difference.

## React app

- Run the Vite app through Aspire's JavaScript app/resource integration when available/current.
- Use the package manager already established by the copied local PCDS/web source. Do not switch package managers casually.
- The app's backend base URL is the gateway resource/configuration, never a service URL.

## AppHost restrictions

Do not place any of the following in AppHost:

- CRM validation or lifecycle rules
- EF Core queries or migrations
- outbox polling loops
- Service Bus processor loops
- event handlers
- user/customer authorization logic
- UI build logic beyond resource commands/configuration

## Done checklist

- [ ] Resource is declared once in AppHost.
- [ ] Only workloads that need the resource receive a reference.
- [ ] Startup dependencies are modeled rather than delayed with sleeps/retries in app code.
- [ ] No hardcoded credentials, hostnames or ports were added.
- [ ] SQL resource is SQL Server, not Postgres/Npgsql.
- [ ] Functions projects use the supported Aspire Functions integration.
- [ ] React receives only gateway-facing configuration.

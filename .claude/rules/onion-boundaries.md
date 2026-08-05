---
paths:
  - "src/**/*.cs"
---
# Controller and Onion Architecture Rules

## Mandatory flow

Every product API operation follows exactly:

```text
Controller -> Facade -> Business -> Data
```

A layer may call only the next layer. No shortcuts and no upward calls.

## Controllers

- Use ASP.NET Core MVC controllers and `ControllerBase`; do not add minimal API routes.
- Controllers depend only on Facade interfaces plus HTTP/framework services.
- Controllers bind input, apply coarse authorization, call one Facade operation, and map its result to `ActionResult<T>`.
- Controllers contain no FluentValidation execution, cache access, business decisions, EF Core, mapping to persistence models, or transaction handling.
- Keep route names, action names, response types, and Problem Details metadata stable for OpenAPI generation.

## Facade

- Facades live in `<domain-project>/<Area>/Facade`.
- Facades are the only application entry point callable by controllers.
- Facades own contextual validation, record-level authorization, cache lookup/invalidation, idempotency coordination, and orchestration.
- Facades depend only on Business interfaces and abstractions such as current user, clock, cache, and correlation context.
- Facades never depend on `DbContext`, `DbSet`, EF entities, Data interfaces, repositories, SQL, or provider exceptions.

## Business

- Business components live in `<domain-project>/<Area>/Business`.
- Business is callable only by Facade.
- Business owns domain rules, lifecycle invariants, and translation between Facade and Data models.
- Business depends only on Data interfaces and cross-cutting domain abstractions.
- Business has no HTTP types, API DTOs, claims parsing, controller attributes, cache implementation, EF Core, or SQL.

## Data

- Data components live in `<domain-project>/<Area>/Data`.
- Data is callable only by Business.
- Data owns EF Core, SQL Server, transactions, persistence entities/configurations, query projection, pagination, concurrency, and database exception translation.
- Data interfaces express purposeful operations, not generic CRUD mirroring `DbSet`.
- Data returns Data result models. It never exposes EF entities or `IQueryable`.

## Composition

- `<api-project>` references `<domain-project>`; Domain never references API.
- Register controllers in API composition root and register Domain onion layers through Domain extension methods.
- Do not use service locator patterns.
- Add architecture tests that reject Controller->Business/Data, Facade->Data/EF, Business->Facade/API/cache-provider, and Data->upper-layer references.


## Repository evidence

Do not assume literal project names, paths, namespaces, DbContext names, connection names, or package versions. Resolve them from the solution, project files, AppHost, ServiceDefaults, and existing source before editing. Examples in the toolkit are role labels only.

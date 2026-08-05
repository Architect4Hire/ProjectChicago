---
paths:
  - "src/**/Data/**/*.cs"
  - "src/**/Migrations/**/*.cs"
---
# Data Layer Rules

- Data is the innermost infrastructure layer and is callable only by Business.
- Own `<db-context>`, EF entities, configurations, migrations, seed mechanics, transactions, SQL projections, pagination, and concurrency writes here.
- Define feature-focused Data interfaces and Data request/result models. Do not expose `DbSet`, `IQueryable`, EF entities, tracked graphs, or provider exceptions.
- Do not reference controllers, API contracts, Facade types, Business implementations, Angular types, cache providers, or HTTP abstractions.
- Translate known unique/concurrency/foreign-key database failures into typed Data outcomes for Business to interpret.
- Use UTC persistence, explicit lengths/nullability/indexes/delete behavior, stable ordering before pagination, and relational integration tests against a real SQL Server test resource orchestrated consistently with Aspire. Do not use EF InMemory for relational behavior.
- State changes, lifecycle history, audit facts, and timeline facts representing one business action commit in one transaction.
- Migration generation and migration application are always separate microsteps.


## Repository evidence

Do not assume literal project names, paths, namespaces, DbContext names, connection names, or package versions. Resolve them from the solution, project files, AppHost, ServiceDefaults, and existing source before editing. Examples in the toolkit are role labels only.

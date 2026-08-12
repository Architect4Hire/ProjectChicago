---
paths:
  - "src/services/ProjectChicago.*.Core/**"
  - "src/ProjectChicago.AppHost/**"
  - "**/Migrations/**"
---
# Microsoft SQL Server rules

Project Chicago uses Microsoft SQL Server. PostgreSQL assumptions from the source repository are intentionally removed.

## Provider

- Use EF Core SQL Server: `Microsoft.EntityFrameworkCore.SqlServer` and/or the current Aspire EF Core SQL Server client integration selected by the solution.
- Do not reference `Npgsql.*`, `UseNpgsql`, PostgreSQL-specific migrations, `jsonb`, `uuid` SQL syntax, sequences/extensions, or pg-specific indexing.
- Prefer Aspire-injected named database connection configuration instead of literal connection strings.

## Ownership

- One database per bounded service.
- Local development may share a SQL Server instance/container; service database ownership remains exclusive.
- Shared infrastructure may provide base DbContext/outbox/inbox types but does not combine service schemas into one shared DbContext.
- Reporting does not earn an exception to cross-service database boundaries. Build an explicit read model/reporting architecture.

## Type guidance

- IDs: `uniqueidentifier` when using GUIDs.
- UTC timestamps: `datetime2` with consistent precision.
- text: `nvarchar` with bounded lengths where practical; `nvarchar(max)` only when genuinely unbounded/document-like.
- money: explicit `decimal(p,s)` appropriate to domain; never floating point for financial values.
- booleans: `bit`.
- optimistic concurrency: `rowversion` where needed.
- JSON/document envelopes: use a SQL Server-compatible representation selected by the architecture; no `jsonb` carryover.

## Indexing

- Index for measured/query-defined access paths, not every foreign key/property automatically.
- For lifecycle/customer searches, inspect actual predicates/sort order and design composite/filtered indexes intentionally.
- Avoid unbounded wildcard search on large text columns as a substitute for a search architecture.
- Unique business constraints belong in the database when they are true persistent invariants, with domain-friendly error translation.

## Migrations

- Migrations live in the owning service `.Core`.
- Generate against SQL Server provider.
- Review generated SQL-sensitive changes (column narrowing, defaults, index rebuilds, nullable transitions) before accepting.
- Production migration execution strategy is an explicit deployment concern. Function invocations do not auto-migrate.
- Never delete/recreate a production database to resolve migration drift.

## Transactions

- The Data layer owns transaction composition.
- Domain state + outbox record commit in one database transaction.
- Consumed-event inbox state and side effects use a transaction strategy that prevents "marked processed but side effect failed."
- Do not use distributed transactions across bounded-service databases.

## Connection resilience

- Use provider/Aspire resilience conventions appropriate to the current integration.
- Do not add retry loops around an entire non-idempotent business operation without understanding transaction semantics.
- Functions concurrency should be tuned with SQL connection capacity in mind.

## Tests

Use real SQL Server-compatible integration infrastructure for:

- migrations;
- transaction rollback;
- rowversion/concurrency;
- unique constraints/index behavior;
- outbox/inbox transaction semantics;
- SQL-specific query translations.

EF InMemory is not proof that SQL Server persistence works.

# ADR-0001 — SQL Server/Azure SQL Database per Bounded Service

- **Status:** Accepted
- **Requirements:** DATA-030..034

## Context
Project Chicago requires relational storage and service ownership boundaries. Shared databases would make service autonomy cosmetic because one service could couple to another service's tables and schema lifecycle.

## Decision
Use Microsoft SQL Server locally/for compatibility and Azure SQL-compatible EF Core behavior. Each independently deployed bounded service owns exactly one database. A service never queries another service's database.

Cross-service information moves through approved APIs or integration events.

## Consequences
- Schema and migrations stay with the owning service.
- Cross-service joins are prohibited.
- Read models that need foreign data must be composed through contracts/events rather than SQL.
- Integration tests must exercise SQL Server-compatible behavior.
- The production Azure SQL hosting topology remains a separate decision (ADR-0019).

## Alternatives considered
- **One shared CRM database:** rejected because it defeats service ownership.
- **PostgreSQL:** rejected by explicit Project Chicago platform requirements.
- **EF InMemory as integration proof:** rejected because it does not prove SQL translation/constraints.

## Validation
Architecture tests reject cross-service DbContext references; SQL integration tests run against SQL Server-compatible infrastructure.

# Database per Service and Data Ownership

Each bounded service owns one SQL Server/Azure SQL database. Ownership means schema, migrations, read/write access and data lifecycle are private to that service.

## Rules
- No cross-service foreign keys.
- No cross-service EF navigation.
- No direct SQL against another service database.
- No shared `DbContext`.
- Integration through APIs/events.
- Migrations reside with the owning service Core.
- Data-layer tests use SQL Server-compatible infrastructure.

## Cross-service reads
If a future feature needs foreign data:
1. first ask whether the feature belongs in the owning context,
2. use a synchronous API only when fresh request-time data is truly required,
3. otherwise maintain an event-fed local read model,
4. record a new ADR for material cross-context coupling.

Do not solve a dashboard by creating cross-database joins.

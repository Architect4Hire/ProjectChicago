---
paths:
  - "**/*AppHost*/**/*.cs"
  - "**/*ServiceDefaults*/**/*.cs"
  - "**/Program.cs"
  - "**/*.csproj"
---

# Required .NET Aspire rules

- Aspire is mandatory and the AppHost is the supported local developer entry point.
- Discover the installed Aspire version and existing APIs before editing; do not copy syntax from an unrelated version.
- AppHost must model the API, Angular application, SQL Server resource, application database, dependency references, and readiness ordering.
- Use `Aspire.Hosting.SqlServer`; model local SQLDB with `AddSqlServer(...).AddDatabase(...)`, then pass the database resource to the API with `WithReference` and `WaitFor` according to the installed version.
- Prefer persistent SQL Server container lifetime/data volume for local development when consistent with repository policy.
- ServiceDefaults must provide the repository-standard health checks, OpenTelemetry, service discovery, and resilience defaults.
- The API consumes the connection name injected by Aspire. Do not add a developer connection string to source-controlled settings as a substitute.
- Production Azure SQL configuration must come from deployment configuration/managed identity or an approved secret source, not AppHost-local credentials.
- Angular must be orchestrated by AppHost using the installed JavaScript hosting integration and the repository's actual package script.
- Verify AppHost build and resource graph after each resource change.

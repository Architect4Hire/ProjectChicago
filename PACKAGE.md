# Lifecycle CRM Claude Toolkit Package

Controller-based CRM implementation toolkit derived from the JobBoard workflow.

Architecture: `<api-project>` MVC controllers call `<domain-project>` Facades; Facades call Business;
Business calls Data; Data owns EF Core/SQL Server. Validation and cache coordination live in Facade, business
rules and model translation in Business, and persistence in Data. No layer may be skipped.

Includes root `CLAUDE.md`, deep `.claude` rules/skills/agents/hooks, SCRUB microstep prompts, and CRM design assets.


## Required orchestration

.NET Aspire is required. The AppHost is the supported local entry point and must orchestrate the API, Angular application, SQL Server database resource, health checks, telemetry, and dependency ordering. Do not add direct developer connection strings as a substitute.

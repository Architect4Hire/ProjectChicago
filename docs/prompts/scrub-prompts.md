# Lifecycle CRM — SCRUB Microstep Prompts

These prompts adapt the JobBoard SCRUB sequence to the Lifecycle CRM modular-monolith architecture: one ASP.NET Core MVC API, one secondary Domain project, one SQL Server/Azure SQL Database (SQLDB), one required .NET Aspire AppHost, one required ServiceDefaults project, and one Angular SPA.

The source JobBoard prompts deliberately optimized for microservices, multiple databases, YARP, Service Bus, outbox/inbox delivery, and cross-service events. This version removes those distributed-system requirements and replaces them with module boundaries, one transactional database, Facade -> Business -> Data calls, append-only lifecycle history, OpenAPI, and a typed Angular client.

## Mandatory implementation architecture

```text
MVC Controller (<api-project>)
  -> Facade (<domain-project>: validation, record authorization, cache check/invalidation)
  -> Business (<domain-project>: business rules and Facade/Data model translation)
  -> Data (<domain-project>: EF Core/SQL Server)
```

Each server-side prompt changes one layer or one adjacent seam only. Calls may move only one layer downward.
Use distinct request/result models at every seam. Controllers never call Business/Data; Facades never call
Data; Business never uses HTTP/cache providers/EF; Data never calls upward.

## Microstep rule

Each prompt performs **one primary action**. A prompt may inspect or verify work necessary for that action, but it must not begin the next implementation action.

Every prompt must:

1. Read `CLAUDE.md` and the applicable `.claude/rules/` files.
2. State the single intended change.
3. List the exact files expected to change.
4. Stop if the requested action requires an unapproved architectural or contract decision.
5. Make only that change.
6. Run the smallest relevant verification.
7. Report results and stop.

Do not paste the entire sequence into Claude Code. Run one prompt at a time. Use `/clear` between major phases.

## Mandatory discovery gate for every prompt

Before executing any prompt, Claude must resolve these values from repository evidence and print them:

- solution and repository root;
- API, Domain, AppHost, ServiceDefaults, Angular, and relevant test project paths;
- namespaces and target frameworks;
- installed Aspire and EF Core versions;
- SQLDB resource name, database resource name, and injected connection-string name;
- DbContext and migrations assembly;
- package-management strategy;
- existing module/feature location and naming conventions;
- smallest verification command already used by the repository.

Do not use a value merely because it appears in an example. Angle-bracket values are unresolved placeholders. If a value cannot be proven from the repository, report the missing decision and stop without editing. If the requested artifact already exists, inspect it and perform only the requested delta instead of recreating it.

Aspire is mandatory: do not introduce a non-Aspire startup path, a hand-authored local connection string, or a separate database bootstrap mechanism. Local SQLDB must be modeled by AppHost and referenced by the API through Aspire.

## Reusable SCRUB skeleton

```text
SCOPE: One concrete action and its exact boundary.
CONSTRAINT: Existing architecture, stack, naming, and quality rules to preserve.
RESTRICTION: Explicitly name adjacent work that must not be started.
USAGE: Skills, agents, commands, and files Claude should use.
BEHAVIOR: Inspect, plan, perform one action, verify, report, and stop.
```

---

# Part 1 — Repository and solution foundation

## Prompt 00 — Inspect repository state

```text
SCOPE: Inspect the repository and produce a current-state inventory only.

CONSTRAINT: Read CLAUDE.md, .claude/, solution files, project files, package manifests, and existing documentation.

RESTRICTION: Do not create, edit, rename, or delete any file. Do not install packages. Do not run migrations.

USAGE: Use read-only repository tools.

BEHAVIOR: Report the current projects, frameworks, package-management approach, test projects, Angular status, database configuration, and conflicts with the target Lifecycle CRM architecture. Stop after the inventory.
```

## Prompt 01 — Propose the target folder tree

```text
SCOPE: Propose only the missing or changed repository roles needed for the target CRM architecture, based on the current-state inventory.

CONSTRAINT: One ASP.NET Core MVC API project, one secondary <domain-project> project containing Facade/Business/Data folders, one SQL Server/Azure SQL Database (SQLDB), one Angular SPA, required Aspire AppHost, and the test projects defined in CLAUDE.md.

RESTRICTION: Do not create folders or files. Do not modify the solution.

USAGE: Follow CLAUDE.md and .claude/rules/backend.md plus frontend.md.

BEHAVIOR: Show the discovered current tree first, then the minimum proposed delta, project references, and purpose of each changed role. Mark every unresolved value explicitly and stop without implementation.
```

## Prompt 02 — Create the solution file

```text
SCOPE: Create a solution file only when repository inspection proves that no solution file exists; otherwise report the existing solution and stop unchanged.

CONSTRAINT: Use the repository SDK version and naming conventions. Preserve central package management when present.

RESTRICTION: Do not create projects. Do not add solution folders. Do not edit Directory.Build.props or Directory.Packages.props.

USAGE: Use the dotnet CLI.

BEHAVIOR: Create the solution, verify it can be listed by dotnet sln, report the created file, and stop.
```

## Prompt 03 — Create the API project

```text
SCOPE: Create the API project only when repository inspection proves that the required API role does not already exist.

CONSTRAINT: Target .NET 10, inherit repository-wide build settings, and use central package management when configured.

RESTRICTION: Do not add EF Core, authentication, modules, controller actions, database code, Aspire wiring, or tests.

USAGE: Use the dotnet CLI and .claude/rules/backend.md.

BEHAVIOR: Create the project, add it to the solution, run dotnet build for this project, report, and stop.
```

## Prompt 03A — Create the Domain project

```text
SCOPE: Create the secondary Domain project only when repository inspection proves that the required Domain role does not already exist.

CONSTRAINT: Target .NET 10 and preserve repository-wide build/package settings. This project will contain Facade, Business, Data, <db-context>, and migrations.

RESTRICTION: Do not add folders, packages, entities, interfaces, registrations, or project references.

USAGE: Use the dotnet CLI and CLAUDE.md.

BEHAVIOR: Create the project, add it to the solution, build it, report, and stop.
```

## Prompt 03B — Reference Domain from API

```text
SCOPE: Add the <api-project> -> <domain-project> project reference only.

CONSTRAINT: Dependency direction is API to Domain only.

RESTRICTION: Do not add a reverse reference, DI registration, controllers, packages, or feature code.

USAGE: Use the dotnet CLI.

BEHAVIOR: Add the reference, verify the solution builds, report the reference graph, and stop.
```

## Prompt 04 — Create the API unit-test project

```text
SCOPE: Create <api-unit-tests-project> only.

CONSTRAINT: Use the repository test framework and reference <api-project>.

RESTRICTION: Do not add test cases, fixtures, integration infrastructure, or frontend tests.

USAGE: Use the dotnet CLI.

BEHAVIOR: Create the project, add it to the solution, add the API project reference, build the test project, report, and stop.
```

## Prompt 05 — Create the API integration-test project

```text
SCOPE: Create <api-integration-tests-project> only.

CONSTRAINT: Use the repository test framework and reference <api-project>.

RESTRICTION: Do not add WebApplicationFactory, containers, database fixtures, or test cases.

USAGE: Use the dotnet CLI.

BEHAVIOR: Create the project, add it to the solution, add the API project reference, build the test project, report, and stop.
```

## Prompt 05A — Create the Domain unit-test project

```text
SCOPE: Create <domain-unit-tests-project> only.

CONSTRAINT: Use the repository test framework and reference <domain-project>.

RESTRICTION: Do not add tests, fixtures, database infrastructure, or API references.

USAGE: Use the dotnet CLI.

BEHAVIOR: Create the project, add it to the solution, add the Domain reference, build, report, and stop.
```

## Prompt 05B — Create the Domain integration-test project

```text
SCOPE: Create <domain-integration-tests-project> only.

CONSTRAINT: Use the repository test framework and reference <domain-project>.

RESTRICTION: Do not add SQL Server test resources, fixtures, test cases, or API references.

USAGE: Use the dotnet CLI.

BEHAVIOR: Create the project, add it to the solution, add the Domain reference, build, report, and stop.
```

## Prompt 06 — Create the Angular workspace

```text
SCOPE: Create the Angular application under <angular-workspace-path> only.

CONSTRAINT: Use the current stable Angular CLI already approved by the repository, standalone APIs, routing, strict TypeScript, and SCSS.

RESTRICTION: Do not add features, layout, authentication, API services, component libraries, or custom styling.

USAGE: Verify the exact Angular CLI command against official Angular documentation before executing it.

BEHAVIOR: Create the workspace, run its default build, report generated versions and files, and stop.
```

## Prompt 07 — Create the required Aspire AppHost

```text
SCOPE: Create the required Aspire AppHost only when repository inspection proves that no AppHost exists.

CONSTRAINT: Use the repository-approved Aspire version and central package management.

RESTRICTION: Do not register the API, Angular app, SQL Server, or any other resource yet.

USAGE: Verify the exact Aspire template and command against official Aspire documentation.

BEHAVIOR: Create the project, add it to the solution, build it, report, and stop.
```

## Prompt 08 — Create ServiceDefaults

```text
SCOPE: Create the required ServiceDefaults project only when repository inspection proves that no ServiceDefaults project exists.

CONSTRAINT: Use the repository-approved Aspire version and standard health, telemetry, and resilience defaults.

RESTRICTION: Do not reference application modules or add product-specific behavior.

USAGE: Verify the exact Aspire template and command against official Aspire documentation.

BEHAVIOR: Create the project, add it to the solution, build it, report, and stop.
```

## Prompt 09 — Reference ServiceDefaults from the API

```text
SCOPE: Add the ServiceDefaults project reference and standard registration to <api-project> only.

CONSTRAINT: Keep Program.cs as a composition root and use the generated extension methods.

RESTRICTION: Do not add database, auth, modules, controller actions, or AppHost registration.

USAGE: Follow .claude/rules/backend.md.

BEHAVIOR: Add the reference and registration, build the API, report changed lines, and stop.
```

## Prompt 10 — Register SQLDB in AppHost

```text
SCOPE: Add one local SQL Server resource and one application SQLDB database to <apphost-project> only.

CONSTRAINT: Resource names must come from configuration conventions; no hardcoded connection string.

RESTRICTION: Do not wire the API or Angular app. Do not create EF Core code. Do not apply schema changes.

USAGE: Verify the installed Aspire SQL Server hosting API (`Aspire.Hosting.SqlServer`) against official Aspire documentation.

BEHAVIOR: Make the AppHost resource change, build AppHost, report the resource names, and stop.
```

## Prompt 11 — Register the API in AppHost

```text
SCOPE: Register <api-project> in AppHost and give it a reference to the SQLDB database only.

CONSTRAINT: Use WithReference and WaitFor patterns supported by the installed Aspire version.

RESTRICTION: Do not register Angular. Do not edit the API beyond connection-name plumbing required for the reference.

USAGE: Follow official Aspire documentation and .claude/rules/backend.md.

BEHAVIOR: Add the API resource, run the smallest AppHost validation available, report, and stop.
```

## Prompt 12 — Register Angular in AppHost

```text
SCOPE: Register the Angular application as the web resource in AppHost only.

CONSTRAINT: Use the current Aspire JavaScript application API and the existing Angular start script.

RESTRICTION: Do not add API proxies, environment files, authentication, or UI features.

USAGE: Verify the exact JavaScript-app API against official Aspire documentation.

BEHAVIOR: Add the Angular resource, start AppHost, confirm only that API, SQLDB, and web resources appear, report, and stop.
```

---

# Part 2 — API platform foundation

## Prompt 13 — Add EF Core packages

```text
SCOPE: Add the approved EF Core SQL Server provider packages to the project that owns the DbContext only.

CONSTRAINT: Use central package management and pin versions in Directory.Packages.props when present.

RESTRICTION: Do not create DbContext, entities, configurations, migrations, or connection strings.

USAGE: Verify `Microsoft.EntityFrameworkCore.SqlServer` and compatible EF Core versions in official Microsoft documentation.

BEHAVIOR: Add package references, restore, build the owning project and its immediate consumer, report exact versions, and stop.
```

## Prompt 14 — Create <db-context>

```text
SCOPE: Create the empty <db-context> and its dependency-injection registration only.

CONSTRAINT: The context location must be discovered from the repository; it must remain in the Data layer or its established persistence location and uses the Aspire-provided SQLDB connection name discovered from AppHost.

RESTRICTION: Do not add DbSets, entities, interceptors, seed data, migrations, Data operations, Business, Facade, or controllers.

USAGE: Follow .claude/rules/data.md and backend.md.

BEHAVIOR: Add the context and registration, build the API, report files changed, and stop.
```

## Prompt 15 — Add API error handling

```text
SCOPE: Add the global API exception handler and ProblemDetails response shape only.

CONSTRAINT: Map validation, not-found, conflict, forbidden, and unexpected errors consistently without leaking internals.

RESTRICTION: Do not add validators, domain entities, controller actions, logging enrichers, or frontend handling.

USAGE: Follow .claude/rules/backend.md and use official ASP.NET Core exception-handling APIs.

BEHAVIOR: Implement error handling, add focused unit tests for mappings, run those tests, report, and stop.
```

## Prompt 16 — Add request correlation

```text
SCOPE: Add correlation-ID handling to API requests and responses only.

CONSTRAINT: Accept a valid incoming correlation ID or create one; include it in logs and response headers.

RESTRICTION: Do not add audit records, distributed tracing exporters, domain behavior, or Angular support.

USAGE: Follow .claude/rules/audit.md and backend.md.

BEHAVIOR: Implement correlation handling, add focused tests, run them, report, and stop.
```

## Prompt 17 — Add current-user abstraction

```text
SCOPE: Add ICurrentUser and its HTTP implementation only.

CONSTRAINT: Expose authenticated user ID, display name when available, and authorization claims without coupling modules to HttpContext.

RESTRICTION: Do not configure authentication, create users, add policies, or use the abstraction in modules yet.

USAGE: Follow .claude/rules/backend.md and security.md.

BEHAVIOR: Add the abstraction and registration, add unit tests for claim mapping, run them, report, and stop.
```

## Prompt 18 — Configure OpenAPI

```text
SCOPE: Configure OpenAPI generation for <api-project> only.

CONSTRAINT: Include stable operation IDs, schemas, ProblemDetails, and authorization metadata when available.

RESTRICTION: Do not generate the Angular client, create controller actions, or add UI code.

USAGE: Use official ASP.NET Core OpenAPI documentation and .claude/rules/api-contracts.md.

BEHAVIOR: Add OpenAPI configuration, build the API, generate or inspect the document, report its path, and stop.
```

---

# Part 3 — CRM persistence model

## Prompt 19 — Create lifecycle-stage entity

```text
SCOPE: Create the LifecycleStage entity only.

CONSTRAINT: Include stable ID, name, description, display order, color token, enabled flag, created timestamp, and updated timestamp. Protect invariants in the model.

RESTRICTION: Do not create EF configuration, DbSet, seed data, transitions, controller actions, DTOs, or migrations.

USAGE: Use .claude/skills/add-lifecycle-stage and .claude/rules/lifecycle.md.

BEHAVIOR: Add the entity and focused unit tests for its invariants, run those tests, report, and stop.
```

## Prompt 20 — Configure lifecycle-stage persistence

```text
SCOPE: Add EF Core configuration and the DbSet for LifecycleStage only.

CONSTRAINT: Enforce stable unique name rules, display-order indexing, length limits, and UTC timestamp persistence.

RESTRICTION: Do not seed stages, generate migrations, create controller actions, or add other entities.

USAGE: Follow .claude/rules/data.md and lifecycle.md.

BEHAVIOR: Add configuration and DbSet, build the API, report the relational mapping, and stop.
```

## Prompt 21 — Seed canonical lifecycle stages

```text
SCOPE: Add seed data for Awareness, Interest, Consideration, Decision, Onboarding, Loyalty, and Advocacy only.

CONSTRAINT: Use deterministic stable IDs and the lifecycle colors defined in CLAUDE.md.

RESTRICTION: Do not generate or apply a migration. Do not add administrator editing or endpoints.

USAGE: Follow .claude/rules/lifecycle.md.

BEHAVIOR: Add seed configuration, build the API, list IDs/order/colors, and stop.
```

## Prompt 22 — Create Account entity

```text
SCOPE: Create the Account entity only.

CONSTRAINT: Include the minimum fields needed for an organization or household customer, lifecycle-stage reference, ownership, status, and audit timestamps. Protect invariants.

RESTRICTION: Do not create Contact, EF configuration, DbSet, DTO, controller action, migration, or Angular code.

USAGE: Use .claude/skills/add-crm-module and .claude/rules/backend.md.

BEHAVIOR: Add the entity and focused unit tests, run them, report, and stop.
```

## Prompt 23 — Configure Account persistence

```text
SCOPE: Add EF Core configuration and the DbSet for Account only.

CONSTRAINT: Configure lifecycle-stage foreign key, indexes for name/owner/status/stage, concurrency token, and soft-delete query behavior if the project uses it.

RESTRICTION: Do not add Contact, migrations, controller actions, repositories, or seed accounts.

USAGE: Follow .claude/rules/data.md.

BEHAVIOR: Add configuration and DbSet, build the API, report mapping decisions, and stop.
```

## Prompt 24 — Create Contact entity

```text
SCOPE: Create the Contact entity only.

CONSTRAINT: Include account association, name, communication fields, role/title, lifecycle-stage reference when independently tracked, status, owner, and audit timestamps. Protect invariants.

RESTRICTION: Do not add EF configuration, DbSet, DTOs, controller actions, migration, or Angular code.

USAGE: Use .claude/skills/add-crm-module and .claude/rules/backend.md.

BEHAVIOR: Add the entity and focused unit tests, run them, report, and stop.
```

## Prompt 25 — Configure Contact persistence

```text
SCOPE: Add EF Core configuration and the DbSet for Contact only.

CONSTRAINT: Configure the Account relationship, optional lifecycle relationship according to the domain model, unique/index rules, concurrency, and soft deletion.

RESTRICTION: Do not create migrations, controller actions, search behavior, or UI.

USAGE: Follow .claude/rules/data.md.

BEHAVIOR: Add configuration and DbSet, build the API, report mapping decisions, and stop.
```

## Prompt 26 — Create lifecycle-transition entity

```text
SCOPE: Create the append-only LifecycleTransition entity only.

CONSTRAINT: Record entity type, entity ID, previous stage ID, new stage ID, reason, effective UTC timestamp, actor, source, and correlation ID.

RESTRICTION: Do not create transition services, EF configuration, DbSet, controller actions, audit events, or migrations.

USAGE: Follow .claude/rules/lifecycle.md and audit.md.

BEHAVIOR: Add the entity and invariant tests, run them, report, and stop.
```

## Prompt 27 — Configure lifecycle-transition persistence

```text
SCOPE: Add EF Core configuration and the DbSet for LifecycleTransition only.

CONSTRAINT: Treat records as append-only, index entity/type/time and stage/time reporting paths, and prevent cascade deletion from stages or customers.

RESTRICTION: Do not add transition behavior, migration, controller actions, or reporting queries.

USAGE: Follow .claude/rules/data.md and lifecycle.md.

BEHAVIOR: Add configuration and DbSet, build the API, report indexes and delete behavior, and stop.
```

## Prompt 28 — Create Activity entity

```text
SCOPE: Create the Activity timeline entity only.

CONSTRAINT: Support note, email, call, meeting, task, stage transition, opportunity event, system event, and custom activity types with actor, subject entity, timestamp, summary, and structured metadata.

RESTRICTION: Do not create EF configuration, DTOs, controller actions, integrations, or migrations.

USAGE: Follow .claude/rules/audit.md and backend.md.

BEHAVIOR: Add the entity and focused invariant tests, run them, report, and stop.
```

## Prompt 29 — Configure Activity persistence

```text
SCOPE: Add EF Core configuration and the DbSet for Activity only.

CONSTRAINT: Add indexes for timeline retrieval and preserve history when related records are soft-deleted.

RESTRICTION: Do not create migration, timeline controller action, audit interceptor, or UI.

USAGE: Follow .claude/rules/data.md.

BEHAVIOR: Add configuration and DbSet, build the API, report indexes, and stop.
```

## Prompt 30 — Generate the initial CRM migration

```text
SCOPE: Generate one initial EF Core migration for the model currently present.

CONSTRAINT: Use <db-context> and the canonical command in CLAUDE.md. Review generated SQL intent and destructive operations.

RESTRICTION: Do not apply the migration. Do not hand-edit generated migration files. Do not add entities to make the migration larger.

USAGE: Use .claude/skills/add-database-migration.

BEHAVIOR: Generate the migration, summarize tables, constraints, indexes, and seed operations, flag concerns, and stop.
```

## Prompt 31 — Apply the initial CRM migration

```text
SCOPE: Apply the already-reviewed initial migration to the local development database only.

CONSTRAINT: Confirm the target is the local Aspire-managed SQLDB database before execution.

RESTRICTION: Do not modify the migration, create a new migration, seed noncanonical business data, or touch shared environments.

USAGE: Use the canonical database-update command from CLAUDE.md.

BEHAVIOR: Apply the migration, verify the applied migration list, report success or the exact failure, and stop.
```

---

# Part 4 — Lifecycle behavior through the onion

## Prompt 32 — Define lifecycle API contracts

```text
SCOPE: Define the HTTP request and response contracts for one lifecycle transition only.

CONSTRAINT: Contracts live in <api-project> and contain HTTP-facing fields only.

RESTRICTION: Do not create Facade, Business, Data, controller, EF, cache, or Angular code.

USAGE: Read CLAUDE.md and api-contracts.md.

BEHAVIOR: Add contracts, verify API compilation, report contract semantics, and stop.
```

## Prompt 33 — Define lifecycle Facade models and interface

```text
SCOPE: Define the lifecycle-transition Facade request/result models and Facade interface only.

CONSTRAINT: Facade models live in <domain-project>/Lifecycle/Facade and are distinct from API contracts.

RESTRICTION: Do not implement validation, cache behavior, Business, Data, controller, or EF changes.

USAGE: Use add-controller-endpoint seam-model rules.

BEHAVIOR: Add the Facade seam types, build Domain, report mappings still required, and stop.
```

## Prompt 34 — Define lifecycle Business models and interface

```text
SCOPE: Define the lifecycle-transition Business request/result models and Business interface only.

CONSTRAINT: Business models are distinct from Facade and Data models.

RESTRICTION: Do not implement rules, Data, Facade, controller, or persistence.

USAGE: Read onion-boundaries.md.

BEHAVIOR: Add Business seam types, build Domain, and stop.
```

## Prompt 35 — Implement lifecycle Data operation

```text
SCOPE: Implement the Data-layer lifecycle transition operation only.

CONSTRAINT: Data owns EF Core, expected-stage/version concurrency, transaction, current-stage update, append-only history, audit, and timeline atomicity.

RESTRICTION: Do not implement Business rules, Facade validation/cache, controller, migration, or Angular code.

USAGE: Use data.md and add-lifecycle-stage.

BEHAVIOR: Add Data models/interface/implementation and relational tests, run focused Domain integration tests, report, and stop.
```

## Prompt 36 — Implement lifecycle Business operation

```text
SCOPE: Implement lifecycle-transition Business rules and Facade/Data model translation only.

CONSTRAINT: Business calls only the lifecycle Data interface and supplies expected state/version plus audit/timeline facts.

RESTRICTION: Do not access EF/cache/HTTP, implement Facade, controller, migration, or UI.

USAGE: Use add-lifecycle-stage and onion-boundaries.md.

BEHAVIOR: Implement Business and unit tests, run focused tests, report, and stop.
```

## Prompt 37 — Implement lifecycle Facade operation

```text
SCOPE: Implement lifecycle-transition Facade validation, record authorization, idempotency coordination, and cache invalidation only.

CONSTRAINT: Facade calls only Business and approved context/cache abstractions.

RESTRICTION: Do not access Data/EF, modify Business rules, add controller, migration, or UI.

USAGE: Use add-controller-endpoint Facade rules.

BEHAVIOR: Implement Facade and unit tests, verify Business is called only after validation/authorization, report, and stop.
```

## Prompt 38 — Add lifecycle controller action

```text
SCOPE: Add one MVC controller action for lifecycle transition only.

CONSTRAINT: Controller maps API contracts to Facade models, applies coarse authorization, calls only Facade, and maps typed outcomes to ActionResult/ProblemDetails.

RESTRICTION: Do not inject Business/Data, add cache/validation/business logic, modify schema, generate Angular client, or add another action.

USAGE: Use add-controller-endpoint.

BEHAVIOR: Add controller action and HTTP integration tests, verify OpenAPI operation, report client-regeneration follow-up, and stop.
```

## Prompt 39 — Implement lifecycle-history Data query

```text
SCOPE: Implement the Data-layer lifecycle-history query only.

CONSTRAINT: Project in SQL, use stable descending effective-time ordering with a deterministic tie-breaker, and paginate.

RESTRICTION: Do not add Business, Facade, controller, cache, export, or UI.

USAGE: Use data.md.

BEHAVIOR: Implement Data query and SQL Server integration tests, report, and stop.
```

## Prompt 40 — Implement lifecycle-history Business query

```text
SCOPE: Implement Business translation for lifecycle history only.

CONSTRAINT: Business calls only the history Data interface and translates Data results into Business results.

RESTRICTION: Do not add Facade, controller, cache, EF, or UI.

USAGE: Use onion-boundaries.md.

BEHAVIOR: Implement and unit test Business translation, report, and stop.
```

## Prompt 41 — Implement lifecycle-history Facade query

```text
SCOPE: Implement Facade validation, record authorization, and approved cache behavior for lifecycle history only.

CONSTRAINT: Facade calls only Business and scopes cache keys to all authorization-sensitive dimensions.

RESTRICTION: Do not access Data/EF, add controller, or add UI.

USAGE: Use add-controller-endpoint.

BEHAVIOR: Implement and test hit/miss/authorization behavior, report, and stop.
```

## Prompt 42 — Add lifecycle-history controller action

```text
SCOPE: Add one MVC controller action for paged lifecycle history only.

CONSTRAINT: Controller calls only Facade and exposes typed pagination/OpenAPI contracts.

RESTRICTION: Do not alter query behavior, add UI/export, or add another action.

USAGE: Use add-controller-endpoint.

BEHAVIOR: Add action and HTTP integration tests, verify OpenAPI, report, and stop.
```

# Part 5 — Account and contact controller slices

## Prompt 43 — Define create-account API contracts

```text
SCOPE: Define create-account HTTP request/response contracts only.
CONSTRAINT: Keep them in API and separate from Domain models.
RESTRICTION: Do not add Facade, Business, Data, controller, persistence, or Angular code.
USAGE: Read api-contracts.md.
BEHAVIOR: Add contracts, build API, report, and stop.
```

## Prompt 44 — Implement create-account Data command

```text
SCOPE: Implement the create-account Data operation only.
CONSTRAINT: Data owns EF mapping interaction, unique constraints, transaction, and typed persistence outcomes.
RESTRICTION: Do not add Business, Facade, controller, cache, migration, or UI.
USAGE: Use data.md.
BEHAVIOR: Implement Data models/interface/implementation and SQL Server integration tests, report, and stop.
```

## Prompt 45 — Implement create-account Business operation

```text
SCOPE: Implement create-account business rules and Facade/Data translation only.
CONSTRAINT: Business calls only Data.
RESTRICTION: Do not access HTTP/cache/EF, add Facade/controller, migration, or UI.
USAGE: Use onion-boundaries.md.
BEHAVIOR: Implement and unit test Business, report, and stop.
```

## Prompt 46 — Implement create-account Facade operation

```text
SCOPE: Implement create-account validation, authorization context, idempotency, and cache invalidation only.
CONSTRAINT: Facade calls only Business.
RESTRICTION: Do not access Data/EF, add controller, migration, import, or UI.
USAGE: Use add-controller-endpoint.
BEHAVIOR: Implement and unit test Facade, report, and stop.
```

## Prompt 47 — Add create-account controller action

```text
SCOPE: Add one MVC controller action for create account only.
CONSTRAINT: Controller calls only Facade and maps typed results to 201/ProblemDetails.
RESTRICTION: Do not inject Business/Data, add another action, generate client, or add UI.
USAGE: Use add-controller-endpoint.
BEHAVIOR: Add action and HTTP tests, verify OpenAPI, report, and stop.
```

## Prompt 48 — Implement account-list Data query

```text
SCOPE: Implement paged account-list Data query only.
CONSTRAINT: Filter/project/order/page in SQL and return Data result models.
RESTRICTION: Do not add Business, Facade, controller, cache, export, metric, or UI.
USAGE: Use data.md.
BEHAVIOR: Implement and SQL Server integration-test Data query, report, and stop.
```

## Prompt 49 — Implement account-list Business query

```text
SCOPE: Implement account-list Business translation only.
CONSTRAINT: Business calls only Data.
RESTRICTION: Do not add Facade/controller/cache/EF/UI.
USAGE: Use onion-boundaries.md.
BEHAVIOR: Implement and unit test, report, and stop.
```

## Prompt 50 — Implement account-list Facade query

```text
SCOPE: Implement account-list validation, authorization, and cache behavior only.
CONSTRAINT: Facade calls only Business and includes all normalized filters and access scope in cache keys.
RESTRICTION: Do not access Data/EF or add controller/UI.
USAGE: Use add-controller-endpoint.
BEHAVIOR: Implement and unit test cache hit/miss and authorization, report, and stop.
```

## Prompt 51 — Add account-list controller action

```text
SCOPE: Add one MVC controller action for paged account listing only.
CONSTRAINT: Controller calls only Facade and returns typed pagination contracts.
RESTRICTION: Do not add export, metric, UI, or another action.
USAGE: Use add-controller-endpoint.
BEHAVIOR: Add action and HTTP tests, verify OpenAPI, report, and stop.
```

## Prompt 52 — Implement account-detail onion path

Run four separate prompts, in order, each as its own Claude Code invocation:

```text
A. Implement account-detail Data query only; SQL Server integration-test and stop.
B. Implement account-detail Business translation only; unit-test and stop.
C. Implement account-detail Facade authorization/cache only; unit-test and stop.
D. Add account-detail MVC controller action only; HTTP-test/OpenAPI verify and stop.
```

Do not combine these four actions in one invocation.

## Prompt 53 — Define create-contact API contracts

```text
SCOPE: Define create-contact HTTP request/response contracts only.
CONSTRAINT: API contracts stay separate from Facade/Business/Data models.
RESTRICTION: Do not add Domain operations, controller, persistence, duplicate resolution, or UI.
USAGE: Read api-contracts.md.
BEHAVIOR: Add contracts, build API, report, and stop.
```

## Prompt 54 — Implement create-contact Data command

```text
SCOPE: Implement create-contact Data operation only.
CONSTRAINT: Enforce relational ownership/uniqueness/concurrency in SQL Server and return typed Data outcomes.
RESTRICTION: Do not add Business, Facade, controller, cache, migration, or UI.
USAGE: Use data.md.
BEHAVIOR: Implement and SQL Server integration-test Data, report, and stop.
```

## Prompt 55 — Implement create-contact Business operation

```text
SCOPE: Implement create-contact rules and Facade/Data translation only.
CONSTRAINT: Business calls only Data.
RESTRICTION: Do not access HTTP/cache/EF or add Facade/controller/UI.
USAGE: Use onion-boundaries.md.
BEHAVIOR: Implement and unit test, report, and stop.
```

## Prompt 56 — Implement create-contact Facade operation

```text
SCOPE: Implement create-contact validation, record authorization, idempotency, and cache invalidation only.
CONSTRAINT: Facade calls only Business.
RESTRICTION: Do not access Data/EF or add controller/import/UI.
USAGE: Use add-controller-endpoint.
BEHAVIOR: Implement and unit test, report, and stop.
```

## Prompt 57 — Add create-contact controller action

```text
SCOPE: Add one MVC controller action for create contact only.
CONSTRAINT: Controller calls only Facade and maps typed results to 201/ProblemDetails.
RESTRICTION: Do not inject Business/Data, add another action, generate client, or add UI.
USAGE: Use add-controller-endpoint.
BEHAVIOR: Add action and HTTP tests, verify OpenAPI, report, and stop.
```

# Part 6 — Angular platform and shell

## Prompt 47 — Add design tokens

```text
SCOPE: Add the CRM color, spacing, typography, radius, elevation, and motion tokens to the Angular application only.

CONSTRAINT: Use the palette in CLAUDE.md and CSS custom properties with accessible light-theme defaults.

RESTRICTION: Do not build layout, components, pages, dark mode, or feature styling.

USAGE: Use docs/design/color-tokens.css and .claude/rules/frontend.md.

BEHAVIOR: Add tokens, run the frontend build, report the token file and any contrast concerns, and stop.
```

## Prompt 48 — Add application routes

```text
SCOPE: Define the top-level lazy route structure only.

CONSTRAINT: Include dashboard, accounts, contacts, lifecycle, tasks, opportunities, engagement, reports, administration, and authentication route boundaries.

RESTRICTION: Do not create feature components, guards, resolvers, API calls, or navigation UI.

USAGE: Follow .claude/rules/frontend.md.

BEHAVIOR: Add route definitions with placeholders only as necessary for compilation, run the build, report, and stop.
```

## Prompt 49 — Build the application shell

```text
SCOPE: Build the reusable application shell only: sidebar region, top bar region, content outlet, responsive collapse behavior, and skip link.

CONSTRAINT: Standalone components, semantic landmarks, keyboard accessibility, design tokens, and no business data.

RESTRICTION: Do not add dashboard widgets, account pages, authentication behavior, API calls, or hardcoded user data.

USAGE: Use .claude/skills/add-angular-feature and delegate review to accessibility-reviewer.

BEHAVIOR: Implement shell tests, run frontend tests/build, run accessibility review, report, and stop.
```

## Prompt 50 — Add static primary navigation

```text
SCOPE: Add the primary CRM navigation items to the existing shell only.

CONSTRAINT: Use one icon family, route-aware active state, accessible labels, and the route structure already defined.

RESTRICTION: Do not add permissions filtering, badges, API data, profile menu, or feature pages.

USAGE: Follow .claude/rules/frontend.md.

BEHAVIOR: Implement navigation and tests, run them, report, and stop.
```

## Prompt 51 — Generate the Angular API client

```text
SCOPE: Generate the typed Angular API client from the current OpenAPI document only.

CONSTRAINT: Generated code must live in the approved generated-client folder and be reproducible by one package script.

RESTRICTION: Do not hand-edit generated files, create facades, call endpoints from components, or modify the API contract.

USAGE: Use .claude/skills/update-angular-api-client and .claude/rules/api-contracts.md.

BEHAVIOR: Add or run the generation command, build the Angular app, report generated operations and any contract errors, and stop.
```

## Prompt 52 — Add API error interceptor

```text
SCOPE: Add one Angular HTTP interceptor that normalizes API ProblemDetails responses only.

CONSTRAINT: Preserve correlation ID, status, title, detail, validation errors, and retry guidance for consumers.

RESTRICTION: Do not add toast UI, authentication tokens, logging services, or feature-specific handling.

USAGE: Follow .claude/rules/frontend.md and api-contracts.md.

BEHAVIOR: Implement interceptor tests, run them, report, and stop.
```

---

# Part 7 — Angular account lifecycle slice

## Prompt 53 — Create account-list facade

```text
SCOPE: Create the Angular Account list facade only.

CONSTRAINT: Wrap the generated API client, expose typed loading/error/data state, filters, paging, and refresh behavior using signals and RxJS appropriately.

RESTRICTION: Do not create components, templates, routes, styling, or call HttpClient directly.

USAGE: Use .claude/skills/add-angular-feature and .claude/rules/frontend.md.

BEHAVIOR: Add facade tests, run them, report, and stop.
```

## Prompt 54 — Create account-list page

```text
SCOPE: Create the Account list page component only.

CONSTRAINT: Consume the existing facade, render accessible table/card views, loading, empty, and error states, and support existing filters/paging.

RESTRICTION: Do not add account detail, create-account modal, inline edit, bulk actions, or new API behavior.

USAGE: Use .claude/skills/add-angular-feature and accessibility-reviewer.

BEHAVIOR: Add component tests, run tests/build, run accessibility review, report, and stop.
```

## Prompt 55 — Create account-detail facade

```text
SCOPE: Create the Angular Account detail facade only.

CONSTRAINT: Wrap the generated account-detail, lifecycle-history, and lifecycle-transition operations with typed state.

RESTRICTION: Do not create components, templates, routes, styling, or business rules in the facade.

USAGE: Follow .claude/rules/frontend.md.

BEHAVIOR: Add facade tests, run them, report, and stop.
```

## Prompt 56 — Create account-summary page

```text
SCOPE: Create the Account summary page component only.

CONSTRAINT: Render existing account detail data, current lifecycle stage, key counts, and recent activity references using the design system.

RESTRICTION: Do not add stage-change controls, full timeline, editing, tasks, opportunity management, or new API calls.

USAGE: Use .claude/skills/add-angular-feature and accessibility-reviewer.

BEHAVIOR: Add component tests, run tests/build, run accessibility review, report, and stop.
```

## Prompt 57 — Create lifecycle stepper component

```text
SCOPE: Create a reusable lifecycle stepper presentation component only.

CONSTRAINT: Accept typed stage data and current-stage ID as inputs; emit a requested stage ID without performing business logic or API calls.

RESTRICTION: Do not integrate it into a page, call the API, open dialogs, or hardcode stage order/colors.

USAGE: Follow .claude/rules/frontend.md and lifecycle.md.

BEHAVIOR: Add accessibility and interaction tests, run them, report, and stop.
```

## Prompt 58 — Add stage-change dialog

```text
SCOPE: Create the stage-change dialog component only.

CONSTRAINT: Collect target stage, reason, and effective timestamp as allowed by the API contract; validate client input for usability only.

RESTRICTION: Do not call the API, mutate facade state, integrate into a page, or duplicate server business rules.

USAGE: Follow .claude/rules/frontend.md.

BEHAVIOR: Add component tests, run them, report, and stop.
```

## Prompt 59 — Integrate lifecycle stepper into account page

```text
SCOPE: Add the existing lifecycle stepper to the Account summary page only.

CONSTRAINT: Read stage data from the existing facade and open the existing stage-change dialog on a permitted user action.

RESTRICTION: Do not submit the transition yet, change backend code, add history timeline, or alter stepper internals.

USAGE: Follow .claude/rules/frontend.md.

BEHAVIOR: Integrate and update component tests, run them, report, and stop.
```

## Prompt 60 — Submit account lifecycle transition

```text
SCOPE: Wire the stage-change dialog result to the existing lifecycle-transition API through the Account detail facade only.

CONSTRAINT: Preserve loading/error state, display the server correlation ID on failure, and refresh account detail after success.

RESTRICTION: Do not add optimistic updates, toast framework, new API controller actions, or contact lifecycle behavior.

USAGE: Follow .claude/rules/frontend.md and lifecycle.md.

BEHAVIOR: Implement facade/page integration tests, run tests/build, report, and stop.
```

## Prompt 61 — Create lifecycle-history timeline component

```text
SCOPE: Create a reusable lifecycle-history timeline component only.

CONSTRAINT: Accept immutable history DTOs as input and render stage, actor, reason, source, and localized time accessibly.

RESTRICTION: Do not fetch data, integrate into a page, support editing, or combine unrelated activity types.

USAGE: Follow .claude/rules/frontend.md and accessibility rules.

BEHAVIOR: Add component tests, run them, report, and stop.
```

## Prompt 62 — Integrate lifecycle history into account page

```text
SCOPE: Add the existing lifecycle-history timeline to the Account summary page only.

CONSTRAINT: Load history through the existing facade and preserve loading, empty, error, and pagination states.

RESTRICTION: Do not add full activity timeline, exports, filters beyond the existing API, or backend changes.

USAGE: Follow .claude/rules/frontend.md.

BEHAVIOR: Integrate and update tests, run tests/build, report, and stop.
```

---

# Part 8 — Dashboard microsteps

## Prompt 63 — Define total-accounts metric

```text
SCOPE: Define the metric contract and calculation specification for Total Accounts only.

CONSTRAINT: State date window, timezone, inclusion/exclusion rules, filters, comparison period, and response shape.

RESTRICTION: Do not implement SQL, controller action, Angular card, or any second metric.

USAGE: Use .claude/skills/add-dashboard-metric and .claude/rules/reporting.md.

BEHAVIOR: Add or update the metric specification document, report decisions, and stop.
```

## Prompt 64 — Implement total-accounts metric query

```text
SCOPE: Implement the backend query for Total Accounts only.

CONSTRAINT: Follow the approved metric specification exactly and use a projection suitable for SQL Server.

RESTRICTION: Do not add controller action, cache, Angular UI, or another metric.

USAGE: Use .claude/skills/add-dashboard-metric.

BEHAVIOR: Add integration tests for boundaries and filters, run them, report generated query characteristics, and stop.
```

## Prompt 65 — Add total-accounts metric endpoint

```text
SCOPE: Add one endpoint for the existing Total Accounts metric query.

CONSTRAINT: Use stable filter parameters and operation ID.

RESTRICTION: Do not add dashboard composition controller action, Angular code, or another metric.

USAGE: Use .claude/skills/add-controller-endpoint and api-contract-checker.

BEHAVIOR: Add endpoint tests, run them, inspect OpenAPI, report, and stop.
```

## Prompt 66 — Create dashboard KPI card component

```text
SCOPE: Create one reusable KPI card component only.

CONSTRAINT: Support title, value, comparison delta, period label, loading, error, and accessible trend semantics.

RESTRICTION: Do not fetch data, hardcode Total Accounts, create a dashboard page, or add charts.

USAGE: Follow .claude/rules/frontend.md and accessibility-reviewer.

BEHAVIOR: Add component tests, run them, report, and stop.
```

## Prompt 67 — Create dashboard facade for total accounts

```text
SCOPE: Add Total Accounts retrieval to a dashboard facade only.

CONSTRAINT: Use the generated API client and typed loading/error/data state.

RESTRICTION: Do not create dashboard page, add other metrics, or call HttpClient directly.

USAGE: Follow .claude/rules/frontend.md.

BEHAVIOR: Add facade tests, run them, report, and stop.
```

## Prompt 68 — Render total-accounts card on dashboard

```text
SCOPE: Create or update the Dashboard page to render only the Total Accounts KPI card.

CONSTRAINT: Consume the existing dashboard facade and KPI component.

RESTRICTION: Do not add another KPI, chart, activity panel, layout redesign, or backend change.

USAGE: Follow .claude/rules/frontend.md.

BEHAVIOR: Add page tests, run tests/build, report, and stop.
```

---

# Part 9 — Verification microsteps

## Prompt 69 — Run backend quality gate

```text
SCOPE: Run the backend quality gate only.

CONSTRAINT: Restore, build, run unit tests, run integration tests, and collect warnings without changing code.

RESTRICTION: Do not fix failures in this prompt. Do not run frontend tests or migrations.

USAGE: Use .claude/skills/run-quality-gate.

BEHAVIOR: Report each command, pass/fail, warnings, failed-test names, and likely owning module. Stop.
```

## Prompt 70 — Run frontend quality gate

```text
SCOPE: Run the frontend quality gate only.

CONSTRAINT: Install from the lockfile, lint when configured, run unit tests once, and run production build without changing code.

RESTRICTION: Do not fix failures, regenerate the API client, run Playwright, or run backend tests.

USAGE: Use .claude/skills/run-quality-gate.

BEHAVIOR: Report each command, pass/fail, warnings, and likely owning feature. Stop.
```

## Prompt 71 — Run API contract review

```text
SCOPE: Review the current OpenAPI contract and generated Angular client for drift only.

CONSTRAINT: Check operation IDs, request/response types, nullability, enums, ProblemDetails, paging, and generated-client reproducibility.

RESTRICTION: Do not edit API or Angular files.

USAGE: Delegate to api-contract-checker.

BEHAVIOR: Report prioritized findings with file references and stop.
```

## Prompt 72 — Run lifecycle-integrity review

```text
SCOPE: Review lifecycle implementation integrity only.

CONSTRAINT: Check atomic current-stage/history/activity writes, append-only behavior, stable stage IDs, disabled-stage handling, authorization, audit actor/source/correlation, and tests.

RESTRICTION: Do not edit files or review unrelated CRM modules.

USAGE: Delegate to lifecycle-integrity-checker.

BEHAVIOR: Report prioritized findings with file references and stop.
```

## Prompt 73 — Run test-gap analysis

```text
SCOPE: Identify missing tests for the implemented CRM scope only.

CONSTRAINT: Prioritize authorization, lifecycle transitions, transaction rollback, concurrency, validation, reporting boundaries, API contracts, Angular state, and accessibility.

RESTRICTION: Do not write tests or change production code.

USAGE: Delegate to test-gap-analyzer.

BEHAVIOR: Return a ranked test backlog with the behavior, test level, and target file for each gap. Stop.
```

## Prompt 74 — Run local end-to-end smoke test

```text
SCOPE: Run one local smoke test for the existing create-account and lifecycle-transition flow only.

CONSTRAINT: Start the approved local stack, create one test account, read it, transition its lifecycle once, and read its lifecycle history.

RESTRICTION: Do not change code, apply unreviewed migrations, seed permanent data, test unrelated features, or fix failures.

USAGE: Use .claude/skills/trace-request.

BEHAVIOR: Record resource health, request correlation IDs, HTTP results, database-visible outcome, and exact failure point if any. Stop.
```

---

# Part 10 — Reusable operational microstep templates

## Template A — Inspect one requested change

```text
SCOPE: Inspect the current implementation related to <change> only.
CONSTRAINT: Read CLAUDE.md and applicable rules.
RESTRICTION: Do not edit files.
USAGE: Use read-only tools.
BEHAVIOR: Report current behavior, owning files, contract/database impact, risks, and the smallest next implementation step. Stop.
```

## Template B — Plan one requested change

```text
SCOPE: Produce an implementation plan for <one change> only.
CONSTRAINT: Preserve current architecture and public contracts unless explicitly approved.
RESTRICTION: Do not edit files or include adjacent backlog items.
USAGE: Use .claude/skills/plan-microstep.
BEHAVIOR: List exact files, tests, commands, migration impact, and acceptance check. Stop for approval.
```

## Template C — Add one entity

```text
SCOPE: Add the <Entity> domain entity only.
CONSTRAINT: Protect its approved invariants and use UTC timestamps.
RESTRICTION: Do not add EF configuration, DbSet, migration, controller action, DTO, or UI.
USAGE: Follow backend.md and the owning module rules.
BEHAVIOR: Add the entity and focused unit tests, run them, report, and stop.
```

## Template D — Add one EF configuration

```text
SCOPE: Add EF Core mapping and DbSet for <Entity> only.
CONSTRAINT: Define keys, lengths, indexes, relationships, delete behavior, concurrency, and query filters explicitly.
RESTRICTION: Do not create or apply a migration or change another entity.
USAGE: Follow data.md.
BEHAVIOR: Add mapping, build the API, report relational decisions, and stop.
```

## Template E — Generate one migration

```text
SCOPE: Generate migration <Name> for the already-approved model change only.
CONSTRAINT: Use <db-context> and review generated T-SQL, destructive operations, SQL Server defaults/computed columns, filtered indexes, rowversion usage, and cascade behavior.
RESTRICTION: Do not apply or hand-edit the migration.
USAGE: Use add-database-migration.
BEHAVIOR: Generate, summarize operations and risks, and stop.
```

## Template F — Apply one migration locally

```text
SCOPE: Apply the already-reviewed migration <Name> to the local database only.
CONSTRAINT: Verify the target database and current migration list first.
RESTRICTION: Do not alter migration files or target shared environments.
USAGE: Use the canonical EF update command.
BEHAVIOR: Apply, verify migration history, report, and stop.
```

## Template G — Add one request contract

```text
SCOPE: Add the request DTO and validator for <operation> only.
CONSTRAINT: Match approved API naming, nullability, validation, and enum conventions.
RESTRICTION: Do not add service, controller action, response, generated client, or UI.
USAGE: Follow api-contracts.md.
BEHAVIOR: Add validator tests, run them, report, and stop.
```

## Template H — Add one Data operation

```text
SCOPE: Implement the Data operation for <operation> only.
CONSTRAINT: Keep HTTP concerns out, enforce domain rules, and preserve transaction/audit requirements.
RESTRICTION: Do not add Business, Facade, controller action, UI, migration, or unrelated refactor.
USAGE: Follow backend.md and the owning module rules.
BEHAVIOR: Add focused unit/integration tests, run them, report, and stop.
```

## Template J — Regenerate the Angular client

```text
SCOPE: Regenerate the Angular client from the current OpenAPI document only.
CONSTRAINT: Use the reproducible package script and preserve generated-code boundaries.
RESTRICTION: Do not hand-edit generated files or change the API.
USAGE: Use update-angular-api-client.
BEHAVIOR: Generate, build Angular, summarize changed operations/types, and stop.
```

## Template K — Add one Angular facade

```text
SCOPE: Add the facade for <feature> only.
CONSTRAINT: Wrap the generated client and expose typed loading/error/data state.
RESTRICTION: Do not add components, routes, templates, or direct HttpClient calls.
USAGE: Follow frontend.md.
BEHAVIOR: Add facade tests, run them, report, and stop.
```

## Template L — Add one Angular component

```text
SCOPE: Add the <Component> presentation component only.
CONSTRAINT: Standalone, accessible, typed inputs/outputs, design tokens, and no business logic.
RESTRICTION: Do not fetch data, add a route, or change backend code.
USAGE: Use add-angular-feature and accessibility-reviewer.
BEHAVIOR: Add component tests, run them, report, and stop.
```

## Template M — Integrate one existing component

```text
SCOPE: Integrate <Component> into <Page> only.
CONSTRAINT: Use existing facade state and existing component contracts.
RESTRICTION: Do not change the component API, add backend behavior, or integrate adjacent components.
USAGE: Follow frontend.md.
BEHAVIOR: Update page tests, run them, report, and stop.
```

## Template N — Diagnose one failure

```text
SCOPE: Diagnose <specific failure> only.
CONSTRAINT: Trace one request or test path from entry point to failure with evidence.
RESTRICTION: Do not edit code or speculate beyond evidence.
USAGE: Use trace-request.
BEHAVIOR: Report reproduction, correlation ID when available, failing boundary, evidence, and smallest proposed fix. Stop.
```

## Template O — Fix one confirmed defect

```text
SCOPE: Fix the confirmed defect <defect> only.
CONSTRAINT: Preserve contracts and add a regression test that fails before the fix and passes after it.
RESTRICTION: Do not refactor adjacent code or fix additional findings.
USAGE: Use the owning feature skill and code-reviewer.
BEHAVIOR: Implement the minimal fix, run the focused regression test, report changed files and residual risk, and stop.
```

## Template H2 — Add one Business operation

```text
SCOPE: Implement one Business operation only.
CONSTRAINT: Call only Data; enforce business rules and translate Business/Data models.
RESTRICTION: Do not access HTTP/cache/EF or add Facade/controller/UI/migration.
USAGE: Read onion-boundaries.md.
BEHAVIOR: Implement, unit test, report, and stop.
```

## Template H3 — Add one Facade operation

```text
SCOPE: Implement one Facade operation only.
CONSTRAINT: Call only Business; own validation, record authorization, cache behavior, and orchestration.
RESTRICTION: Do not access Data/EF or add controller/UI/migration.
USAGE: Use add-controller-endpoint.
BEHAVIOR: Implement, unit test validation/cache/authorization, report, and stop.
```

## Template I — Add one controller action

```text
SCOPE: Add one MVC controller action only.
CONSTRAINT: Call only Facade and map API contracts to/from Facade models and typed ProblemDetails.
RESTRICTION: Do not inject Business/Data, add logic/cache/EF, regenerate client, or add UI.
USAGE: Use add-controller-endpoint.
BEHAVIOR: Add action, HTTP-test, verify OpenAPI, report, and stop.
```

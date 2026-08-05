# Lifecycle CRM

*Project memory written as a SCRUB prompt: Scope, Constraints, Restrictions, Usage, Behavior. Loaded every Claude Code session.*

## Scope

- Lifecycle CRM tracks organizations and contacts through a configurable lifecycle journey from awareness through advocacy.
- The solution has one deployable ASP.NET Core API, one secondary Domain class-library project, one SQL Server/Azure SQL Database (SQLDB), one required .NET Aspire AppHost, one required ServiceDefaults project, and one Angular SPA.
- The API uses MVC controllers. Do not implement new minimal-API routes.
- The mandatory server-side call chain is:

```text
HTTP request
  -> Controller (API project)
  -> Facade (Domain project: validation, authorization context checks, cache lookup/invalidation, orchestration)
  -> Business (Domain project: business rules and API/domain/data model translation)
  -> Data (Domain project: EF Core queries, commands, transactions, persistence models)
  -> SQL Server
```

- Primary CRM areas: Identity & Access, Accounts, Contacts, Lifecycle, Activities, Tasks, Opportunities, Engagement, Reporting, Administration, and Audit.
- In bounds: controllers, Domain Facade/Business/Data layers, database schema, Angular application, tests, documentation, and `.claude/` tooling.
- Out of bounds unless explicitly approved: microservices, message brokers, distributed transactions, minimal APIs, MediatR, a public API gateway, additional databases, event sourcing, or a generic workflow engine.

## Constraints

### Stack

- .NET 10 and ASP.NET Core MVC Web API controllers.
- Angular standalone components, strict TypeScript, signals for local state, RxJS for asynchronous streams, and lazy-loaded feature routes.
- EF Core with Microsoft SQL Server/Azure SQL Database. One database and one migrations assembly discovered from the repository. Do not assume its project or folder; use the existing convention or document the decision before creating one.
- OpenAPI is the HTTP contract. Generate the Angular API client from the controller-produced OpenAPI document when practical.
- Authentication uses ASP.NET Core Identity or an external OIDC provider. Controller policies provide coarse authorization; Facades enforce record/context-sensitive access.
- .NET Aspire is required for local orchestration and the supported developer startup path. The AppHost must orchestrate the API, Angular application, SQL Server resource/database, health checks, telemetry, and declared dependencies.

### Required solution shape

```text
src/
├── <apphost-project>/                 # required orchestration and developer entry point
├── <service-defaults-project>/         # telemetry, health, resilience
├── <api-project>/                     # HTTP host and composition root only
│   ├── Controllers/                      # MVC controllers grouped by CRM area
│   ├── Contracts/                        # HTTP request/response contracts
│   ├── Filters/                          # HTTP-only filters
│   ├── Middleware/                       # HTTP-only middleware
│   ├── Configuration/                    # composition-root registration
│   └── Program.cs
├── <domain-project>/                  # secondary domain-layer project
│   ├── Common/
│   ├── Accounts/
│   │   ├── Facade/
│   │   ├── Business/
│   │   └── Data/
│   ├── Contacts/
│   │   ├── Facade/
│   │   ├── Business/
│   │   └── Data/
│   └── <Area>/
│       ├── Facade/
│       ├── Business/
│       └── Data/
└── web/                                  # Angular SPA

tests/
├── <api-unit-tests-project>/
├── <api-integration-tests-project>/
├── <domain-unit-tests-project>/
├── <domain-integration-tests-project>/
└── web/
```

### Onion responsibilities

#### Controller — API project

- Bind route, query, header, and body values.
- Apply authentication and coarse policy authorization attributes.
- Pass trusted request context and a typed Facade request to exactly one Facade operation.
- Translate the Facade result into `ActionResult<T>` and stable Problem Details.
- Declare OpenAPI metadata through controller/action attributes and typed contracts.
- Contain no business rules, cache calls, EF Core access, data-model translation, or calls to Business/Data.

#### Facade — Domain project

- Be the only Domain layer callable by controllers.
- Perform request validation that depends on operation context.
- Resolve and enforce record-level access using trusted current-user abstractions.
- Check cache before Business for cacheable queries.
- Invalidate or refresh cache after successful commands.
- Orchestrate one or more Business operations when required.
- Own idempotency coordination and operation-level result/error translation.
- Never use `DbContext`, `DbSet`, SQL, EF entities, or Data implementations directly.

#### Business — Domain project

- Be callable only by Facade.
- Enforce CRM business rules and lifecycle invariants.
- Translate between Facade models and Data request/result models.
- Coordinate Data operations through Data interfaces.
- Decide transaction intent, concurrency expectations, audit/timeline facts, and domain outcomes.
- Contain no HTTP types, controller contracts, claims parsing, cache provider calls, or direct EF Core access.

#### Data — Domain project

- Be callable only by Business.
- Own `<db-context>`, EF entities, configurations, migrations, queries, commands, transactions, and database exception translation.
- Accept and return Data-layer models, not API contracts or Business entities.
- Perform SQL projection, stable ordering, pagination, concurrency-safe writes, and atomic persistence.
- Never call Facade, Business implementations, controllers, Angular code, or external HTTP endpoints.

### Dependency direction

```text
<api-project> -> <domain-project>
Controller -> I<Feature>Facade
Facade -> I<Feature>Business
Business -> I<Feature>Data
Data -> EF Core/Microsoft SQL Server or Azure SQL Database
```

- Calls and references must never skip a layer.
- Interfaces are declared at the consuming boundary or in a narrowly scoped contracts folder consistent with the repository convention.
- No circular project, namespace, service-registration, or runtime dependencies.
- Cross-area work still follows the same chain. A Facade may orchestrate multiple Business interfaces; a Business component may coordinate multiple Data interfaces. Controllers never orchestrate multiple lower layers themselves.

### CRM domain

- An Account represents a customer organization or household. A Contact belongs to an account unless explicitly modeled as an independent prospect.
- Canonical lifecycle stages: Awareness, Interest, Consideration, Decision, Onboarding, Loyalty, Advocacy. Stable IDs survive rename, reorder, enable, and disable operations.
- A lifecycle transition updates current state and appends history atomically.
- Every transition records entity type/ID, prior/new stage, reason, effective UTC timestamp, actor, source, and correlation ID.
- Activities form the customer timeline: note, email, call, meeting, task, stage transition, opportunity event, system event, and custom activity.
- Deleting business records defaults to soft deletion where retention, reporting, or audit history would otherwise break.
- Metrics define date window, timezone, filters, and denominator.

### Frontend design system

- Inter from Google Fonts.
- Primary: Navy `#0D1117`, Slate `#1E293B`, Blue `#2563EB`, Teal `#14B8A6`, Amber `#F59E0B`, Red `#EF4444`, Surface `#F8FAFC`.
- Lifecycle: Awareness `#2563EB`, Interest `#14B8A6`, Consideration `#F59E0B`, Decision `#8B5CF6`, Onboarding `#22C55E`, Loyalty `#06B6D4`, Advocacy `#EF4444`.
- Dark navy sidebar, light content surface, 8px cards, 6px inputs, 8px spacing grid, 150–200ms transitions.
- WCAG AA contrast, keyboard navigation, visible focus, reduced motion, and one icon family.

### Canonical commands

- API: `dotnet restore`, `dotnet build`, `dotnet test`.
- EF migration: discover the actual data project, startup project, and DbContext first; then run `dotnet ef migrations add <Name> --project <data-project> --startup-project <api-project> --context <db-context>`.
- Database update: target only the Aspire-provided local SQLDB connection and run `dotnet ef database update --project <data-project> --startup-project <api-project> --context <db-context>`.
- Frontend from `src/web`: `npm ci`, `npm test -- --watch=false`, `npm run build`, `npx playwright test`.

## Restrictions

- Do not add minimal API route mappings for product endpoints. Use controllers deriving from `ControllerBase`.
- Do not inject Business or Data interfaces into controllers.
- Do not inject Data interfaces or `<db-context>` into Facades.
- Do not call Facades from Business, or any upper layer from Data.
- Do not pass API request/response contracts into Business or Data.
- Do not return EF entities, `IQueryable`, provider exceptions, or Data models above Business.
- Do not put validation or cache logic in controllers when it belongs in Facade.
- Do not put business rules or API/domain translation in Data.
- Do not place business logic in Angular components, controllers, `Program.cs`, migrations, mapping profiles, or cache adapters.
- Do not introduce generic repositories that merely mirror `DbSet`; Data interfaces must express business-relevant persistence operations.
- Do not bypass the lifecycle Business operation for stage changes.
- Do not hardcode lifecycle IDs, names, colors, URLs, credentials, tenant IDs, or connection strings in feature code.
- Do not physically delete lifecycle history or audit records through ordinary flows.
- Do not commit secrets or generated build/dependency directories.

## Usage

- Work in microsteps: one prompt, one primary action, one verification, then stop.
- Use `.claude/skills/add-controller-endpoint` for one controller action and its complete onion path.
- Use `.claude/skills/add-crm-module` for a new CRM area with Facade/Business/Data structure.
- Use `.claude/skills/plan-microstep` to split multi-action requests.
- Use `.claude/skills/trace-request` to inspect Controller -> Facade -> Business -> Data behavior.
- Use the remaining skills for lifecycle, audit, migrations, Angular, metrics, and quality gates.
- Use `docs/prompts/scrub-prompts.md` for the ordered one-action build process.

## Behavior

- Before editing, name the single action and the exact layer being changed.
- Inspect a comparable implementation in the same layer before introducing a new pattern.
- Preserve Controller -> Facade -> Business -> Data with no shortcuts.
- Build or test after every microstep and report changed files, command results, risks, and the next logical microstep.
- Stop rather than silently compensating when a required lower-layer contract is missing; identify the prerequisite microstep.
- Never generate/apply migrations or regenerate/adapt Angular clients in the same microstep as backend implementation.

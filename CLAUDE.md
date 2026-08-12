# Project Chicago

Project memory, written as a SCRUB prompt — Scope, Constraints, Restrictions, Usage, Behavior. This file is loaded every Claude Code session. Keep enduring architecture rules here; put path-specific details in `.claude/rules/`, repeatable procedures in `.claude/skills/`, review work in `.claude/agents/`, and deterministic safeguards in `.claude/hooks/`.

## Scope

- Project Chicago is a CRM application for managing customers through a lifecycle journey.
- The system is a distributed .NET solution developed locally with Aspire, backed by Microsoft SQL Server, integrated asynchronously through Azure Service Bus, and presented through a client-side React 19 application.
- The reusable Claude Code engineering toolkit lives in `.claude/`.
- In bounds: existing bounded services, each service's HTTP host, `.Core` project and `.Functions` project; the YARP gateway; Contracts and Shared libraries; Aspire AppHost/ServiceDefaults; Microsoft SQL databases; Service Bus resources; and the React client.
- Out of bounds unless explicitly approved: inventing a new bounded service, sharing a service database, introducing a second message broker, server-side React/Next.js, bypassing PCDS with a competing design system, or replacing architectural seams for convenience.
- The initial bounded-context catalog is intentionally not invented in this toolkit. When the project defines service names and ownership, document them here and in an architecture decision record.

## Constraints

### Runtime and orchestration

- .NET 10 is the backend target.
- Aspire is the development-time composition/orchestration model through `ProjectChicago.AppHost` and `ProjectChicago.ServiceDefaults`.
- Aspire may model API projects, Azure Functions projects, SQL Server databases, Service Bus/emulator resources, cache resources, gateway, and the React client for local development.
- Azure Functions use the .NET isolated worker model. Do not create in-process Functions.
- Production Azure Functions run on **Flex Consumption**. Treat Flex Consumption constraints as deployment architecture, not an implementation detail.
- Every bounded service has one sibling `ProjectChicago.<Service>.Functions` project deployed as its own Function App unless an explicit ADR changes this.
- Project Chicago Functions are asynchronous workloads. Do not add HTTP-triggered Functions for browser/application APIs; YARP + service HTTP hosts are the only HTTP application edge.
- A Functions project is a real deployable workload, not a `BackgroundService` hidden inside an API host.
- Do not put application or business logic in AppHost. AppHost declares resources, dependencies, configuration references, health/start ordering, and local orchestration only.

### Service shape — preserve this structure

Every bounded service uses three projects while preserving one `.Core` implementation stack:

```text
ProjectChicago.<Service>/             # thin ASP.NET Core HTTP/API host
ProjectChicago.<Service>.Core/        # domain/application implementation
ProjectChicago.<Service>.Functions/   # asynchronous entry points only
```

The HTTP host contains controllers/endpoints, middleware registration, and composition only. The Functions project contains Service Bus triggers, timer triggers, function-specific composition, `host.json`, and binding/configuration only. Both entry-point projects delegate to the same service-owned `.Core` behavior.

Inside `.Core`, keep the onion/layer direction:

```text
HTTP Controller ─┐
                 ├─> Facade -> Business -> Data -> Repository -> DbContext
Function Trigger ┘
```

Responsibilities are strict:

- **Controller / Function trigger**: transport binding, authentication/authorization context where applicable, request/event deserialization, correlation metadata, call the facade, map result/settlement. No business rules and no direct repositories.
- **Facade**: input validation, orchestration at the use-case boundary, cache lookup/invalidation, authorization policy calls when service-owned, and delegation to Business.
- **Business**: domain decisions, state-transition rules, model translation, and decision about which integration facts must be emitted. No EF queries and no Service Bus calls.
- **Data**: composes repository operations and owns transaction boundaries; persists domain changes plus outbox records atomically; performs inbox/idempotency state transitions for consumed messages.
- **Repository**: persistence operations for one service database. No cross-service queries and no business decisions.
- **DbContext**: EF Core SQL Server mapping and unit-of-work plumbing for the owning service only.

### Reference direction

Keep project references acyclic and boundary-safe:

```text
ProjectChicago.Contracts      # leaf: integration-event contracts only
           ↑
ProjectChicago.Shared         # cross-cutting mechanisms; may reference Contracts only if required
           ↑
ProjectChicago.<Service>.Core # service implementation; references Shared/Contracts
           ↑              ↑
<Service> HTTP host      <Service>.Functions
           ↑              ↑
           └────── ProjectChicago.AppHost references deployable projects for local orchestration
```

- One bounded service never references another bounded service's `.Core`, API host, Functions project, repository, DbContext, or internal model.
- Cross-service behavior uses a stable gateway HTTP contract when synchronous interaction is truly required, or integration events through Service Bus when asynchronous decoupling is appropriate.
- `ProjectChicago.Contracts` contains integration-event records/interfaces only. It is not a shared-domain-model library.
- `ProjectChicago.Shared` contains cross-cutting mechanism only: base persistence abstractions, outbox/inbox infrastructure, error contracts, correlation/telemetry helpers, cache abstractions, Service Bus serialization/publishing mechanisms, and similar infrastructure. No CRM domain logic.

### Messaging and Azure Functions

- Azure Service Bus is the asynchronous integration boundary.
- **Do not implement Service Bus consumers as `BackgroundService`, `IHostedService`, or hosted processors inside API projects.** Incoming asynchronous work is handled by Service Bus-triggered Azure Functions in the owning service's `.Functions` project, deployed on Flex Consumption.
- Preserve the transactional outbox pattern. A mutating request does not publish directly from Controller, Facade, Business, or Repository. The Data layer saves the domain transaction and an outbox row together.
- Replace the old always-running outbox dispatcher with a timer-triggered Azure Function per publishing service. The trigger delegates to a reusable outbox-relay mechanism; the Function itself contains no relay/domain logic.
- Only the outbox relay publishes integration events generated by transactional application work. Do not introduce ad-hoc `ServiceBusSender.SendMessageAsync` calls elsewhere.
- Consumed messages are idempotent. Record/check inbox state using the owning service database and design duplicate delivery as a normal condition.
- When message processing fails transiently, allow the Function invocation to fail so Service Bus retry/dead-letter policy can do its job. Do not catch-and-log-and-return success for an event that was not safely processed.
- Correlation ID, causation ID, message/event ID, event type/version, and occurred-at UTC are carried through the message boundary and logs.
- Event names describe facts in past tense. Events are versionable external contracts, not serialized EF entities.
- Service Bus entity names and connection information come from configuration/Aspire/Azure settings; never hardcode broker endpoints or credentials.
- A service's API project should not receive Service Bus credentials merely because its `.Functions` sibling needs them. Wire the narrowest resource references possible.

### Microsoft SQL Server

- PostgreSQL/Npgsql is not part of Project Chicago.
- Use Microsoft SQL Server locally through Aspire SQL Server hosting and EF Core's SQL Server provider/client integration.
- Each bounded service owns exactly one database. Local development may use one SQL Server resource with one database per service, but ownership remains isolated.
- Use `Microsoft.EntityFrameworkCore.SqlServer` / the Aspire SQL Server EF Core integration rather than Npgsql packages or PostgreSQL types.
- Use SQL Server-compatible data types and migrations (`uniqueidentifier`, `datetime2`, `nvarchar`, `rowversion` when justified). Do not introduce `jsonb`, PostgreSQL arrays, PostgreSQL-specific indexes, or Npgsql annotations.
- If a JSON payload must be retained (for example, an audit envelope), store it using a SQL Server-compatible representation selected by the project; do not assume PostgreSQL JSON behavior.
- Migration files live with the owning `.Core` project. Do not let Functions auto-create or auto-migrate schemas at message-processing time.
- A service may not read another service's database for joins, reporting shortcuts, validation, or troubleshooting.

### Edge / gateway

- `src/ProjectChicago.Gateway/` is the YARP edge and the browser's only backend address unless an explicit architecture decision changes this.
- The React client never calls internal service hosts or Function endpoints directly.
- The gateway exposes stable public CRM routes and resolves internal service resources through Aspire/configuration, not hardcoded ports.
- Cross-cutting edge concerns belong at the gateway when truly edge-wide; service/domain authorization stays with the owning service.

### Identity and authorization

- Project Chicago uses **ASP.NET Core Identity** as the application identity system.
- Do not replace ASP.NET Core Identity with Entra ID, Auth0, another OIDC product, or a custom password/token store unless an explicit architecture decision changes the baseline.
- ASP.NET Core Identity owns user credentials, password hashing, users, roles/claims, reset/confirmation tokens, lockout and related account-security mechanics through supported framework APIs. Do not reimplement those mechanics in CRM domain code.
- The bounded service/database that will own the Identity store is intentionally **not selected yet** because the bounded-service catalog is not defined. Do not invent an Identity service as a side effect of another feature.
- Browser authentication traffic goes through YARP to the eventual owning HTTP service. React never connects to an Identity database or Function endpoint directly.
- Cookie vs token transport, refresh/session strategy, MFA/passkey adoption, external providers and account-recovery policy require explicit security decisions; do not silently choose them while implementing an unrelated feature.
- Resource/action authorization remains service-owned even when the gateway applies edge-wide authenticated-user requirements.

### React 19 client and Project Chicago Design System (PCDS)

- The client is a **client-side React 19 + TypeScript + Vite + Tailwind CSS v4** application under `src/web/`.
- PCDS (`https://github.com/architect4hire/PCDS`) is the UI design-system source of truth. Do not create a competing set of tokens/primitives/recipes in feature folders.
- Preserve the PCDS architecture:
  - primitive and semantic tokens in the global CSS token layer;
  - shared Tailwind bundles and typed variants in design-system recipes;
  - reusable React primitives in `src/design-system`;
  - composed patterns for page headers, loading, empty and error states;
  - feature pages that compose those primitives and retain only feature-specific layout/behavior.
- Prefer design-system primitives such as `Button`, `Surface`, `Card`, `Field`, `Input`, `Stack`, `Cluster`, `Grid`, and `Tabs` over repeated Tailwind class bundles.
- Use the PCDS `cx()`/recipe approach for conditional class composition; do not paste recipe bundles into pages.
- Accessibility is a release requirement: semantic HTML, keyboard interaction, labels, visible focus, correct dialog/tab behavior, status announcements where needed, and reduced-motion support.
- Support both light and dark modes through the established PCDS theme mechanism.
- The UI talks only to the gateway through typed API modules. No component issues raw `fetch` calls to an internal service URL.
- Do not add Next.js, server components, SSR, or a second CSS/component framework unless explicitly requested.

### Observability

- Use ServiceDefaults/OpenTelemetry conventions across API hosts and Functions where supported.
- Every request and event-processing log should be traceable using correlation/causation identifiers.
- Log structured facts, not secrets or full sensitive CRM payloads.
- Function logs must include event type, message ID, correlation ID, owning service, result, retry-relevant failure information, and duration without logging credentials.

### Testing

- Unit-test Facade/Business/Data behavior at the layer that owns the rule.
- API tests verify request mapping, response/error contracts, authorization and gateway-visible behavior.
- Function tests verify event deserialization/binding adapter behavior and delegation, but business assertions belong in `.Core` tests.
- Messaging tests must cover duplicate delivery/idempotency, failed processing, outbox atomicity, outbox relay retry behavior, and event contract compatibility.
- SQL integration tests use SQL Server-compatible infrastructure. Do not use an in-memory provider to claim SQL-specific persistence behavior is tested.
- Frontend changes run lint/build and focused component tests when present; validate keyboard behavior, responsive layout, light/dark mode, and loading/empty/error states.

## Restrictions

- Never add `BackgroundService`/`IHostedService` for Service Bus consumption or outbox dispatch. If recurring/asynchronous work is needed, first determine whether it belongs in an Azure Function trigger.
- Never let a Function become a second business layer. A trigger delegates to its service `.Core`.
- Never publish a transactional integration event before its domain transaction commits to the outbox.
- Never mark an outbox message dispatched before Service Bus confirms the publish operation.
- Never mark an inbox message complete when business side effects failed.
- Never share a DbContext/database across service boundaries.
- Never add Npgsql/PostgreSQL packages, connection strings, migrations, or SQL syntax.
- Never hardcode SQL, Redis, Service Bus, gateway, or service endpoints/credentials.
- Never put CRM domain logic in Shared, Contracts, AppHost, Gateway, controllers, or Function trigger classes.
- Never bypass the gateway from React.
- Never duplicate PCDS token values or common Tailwind recipes inside feature pages.
- Never introduce a new service or redesign a service boundary as a side effect of implementing one feature. Propose boundary changes explicitly.
- Never silently swallow exceptions to make a Function invocation appear successful.
- Never log secrets, tokens, raw connection strings, or unnecessarily broad customer data.

## Usage

Before changing code:

1. Identify the owning bounded service and the entry path: HTTP, Service Bus trigger, timer trigger, UI, or infrastructure.
2. Read the matching `.claude/rules/*.md` files.
3. Use a matching skill when one exists instead of inventing a new procedure.
4. For multi-file changes, state a short plan that names affected projects and boundaries.
5. If the task would create a service, share a database, bypass the gateway, introduce a new broker, or change PCDS ownership, stop and surface the architectural decision instead of slipping it into implementation.

Useful skills:

- `add-endpoint` — add/modify an HTTP use case through Controller → Facade → Business → Data → Repository.
- `add-function-trigger` — add a Service Bus or timer-triggered Function that delegates into an existing service `.Core`.
- `add-integration-event` — add both publish and consume sides of a cross-service event, including outbox/inbox and Functions wiring.
- `add-component` — add React UI using PCDS and the gateway.
- `add-aspire-resource` — add/wire SQL Server DBs, Service Bus resources, Functions projects, cache, or a new deployable resource.
- `add-audit-event` — conditionally extend a support audit trail if the Audit bounded context is enabled.
- `trace-a-request` — reconstruct request/event flow from correlation telemetry/audit data without cross-service DB access.

Use read-only agents after implementation:

- `code-reviewer`
- `test-gap-analyzer`
- `api-contract-checker`
- `function-boundary-checker`
- `audit-coverage-checker` when the Audit context is enabled

## Behavior

- Preserve existing architecture before optimizing aesthetics or reducing files.
- Prefer small vertical changes that prove one complete seam over broad scaffolding with placeholders.
- When an external API/package/tooling surface is fast-moving (Aspire, Azure Functions, Service Bus bindings, Claude Code, React/Tailwind), verify current official documentation before writing version-sensitive configuration.
- Explain boundary-impacting choices in the change summary: owner, database, HTTP route, event(s), Function trigger(s), Service Bus entity configuration, and PCDS primitives used.
- If requirements are ambiguous but implementation can safely proceed, make the narrowest reversible assumption and record it. Ask only when a decision changes architecture, public contracts, security, persistence ownership, or deployment topology.
- Keep comments focused on why a non-obvious constraint exists. Let names and structure explain routine code.
- A feature is not complete merely because it compiles: validate the HTTP/event contract, persistence transaction, retry/idempotency path, telemetry, tests, and UI state handling that apply to it.

## Confirmed architecture decisions

1. The bounded-service catalog is intentionally undefined. Claude must not invent service names or boundaries.
2. Every future bounded service uses one HTTP host, one `.Core`, and one `.Functions` project.
3. Each publishing service uses a timer-triggered Function to drain its transactional outbox.
4. Each Service Bus subscription is consumed from the owning service's `.Functions` project.
5. Azure Functions production hosting is **Flex Consumption**. Function deployment guidance must not depend on unsupported hosting features such as deployment slots.
6. YARP is the **only gateway / browser-facing backend edge** for the solution. React never calls service or Function endpoints directly.
7. Each bounded service owns exactly one Microsoft SQL database. Local Aspire may use one SQL Server resource with one database resource per service while preserving ownership isolation.
8. PCDS is copied into Project Chicago and consumed from the local source in the React application. Skills must inspect and reuse/extend that local design system rather than recreating it.
9. Application identity uses **ASP.NET Core Identity**. Identity-store bounded-context ownership and auth transport/session details remain open until the service/security design is defined.

## Still intentionally open

- The CRM bounded-service catalog and concrete database names.
- The production Microsoft SQL hosting flavor (for example Azure SQL Database vs another SQL Server deployment).
- Service Bus topic/subscription naming and filtering topology.
- Whether Audit, Redis/caching and a reporting/read-model service are baseline bounded contexts/resources.
- The API edge style inside each service (controllers are the current preserved default; changing that requires a deliberate decision).
- Identity-store ownership plus cookie/token/session/MFA/external-provider policy.
- IaC/deployment ownership and tooling beyond the confirmed Flex Consumption target.

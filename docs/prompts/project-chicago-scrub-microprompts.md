# Project Chicago — SCRUB Microstep Implementation Prompts (Linked Requirements)



> Replacement prompt library for the obsolete Lifecycle CRM / Angular prompt set currently in the repository.



These prompts implement the Project Chicago requirements for **Clients → Projects → Tasks** while preserving the architecture encoded in the current `CLAUDE.md` and `.claude/` toolkit.

The sequence is deliberately granular:

- **one prompt = one primary change**
- every prompt names the requirement IDs it advances, summarizes the applicable requirement intent in a few sentences, and links to the canonical requirement text
- every implementation prompt is independently verifiable
- adjacent work is explicitly forbidden
- architecture/security decisions that are still open in `CLAUDE.md` are resolved through explicit approval gates rather than being invented as side effects
- each prompt ends after its verification; do not let Claude continue into the next prompt

## Compact requirement callout contract

Each micro-prompt includes only four requirement elements: traceability IDs, direct links to the canonical requirement sections, a short requirement-intent summary, and an explicit source-of-truth rule. Full requirement prose is intentionally **not** duplicated in this file.

Claude must open the linked requirement before changing code. If the linked requirement and the prompt disagree, the requirement wins and the implementation step stops for documentation correction.

## Architecture this sequence preserves

```text
Browser / React 19
      |
      v
YARP Gateway                     # only browser-facing backend edge
      |
      v
ProjectChicago.<Service>         # thin ASP.NET Core HTTP host
      |
      v
Facade -> Business -> Data -> Repository -> DbContext
      ^
      |
ProjectChicago.<Service>.Functions
    |                     |
ServiceBusTrigger      TimerTrigger -> OutboxRelay -> Service Bus
```

Per bounded service:

```text
ProjectChicago.<Service>/
ProjectChicago.<Service>.Core/
ProjectChicago.<Service>.Functions/
```

Infrastructure:

- .NET 10
- .NET Aspire
- Microsoft SQL Server / Azure SQL compatible EF Core provider
- Azure Service Bus
- Azure Functions isolated worker
- Azure Functions Flex Consumption in production
- ASP.NET Core Identity
- React 19 + TypeScript + Vite + Tailwind CSS v4
- copied local Project Chicago Design System (PCDS)
- OpenTelemetry
- Azure Monitor / Application Insights as the production single pane of glass
- YARP as the only browser-facing backend edge

## Recommended bounded-context decision

The current repository intentionally leaves the service catalog open. The requirements now give enough business scope to make a deliberate recommendation:

- **Crm** — owns Clients, Projects, Tasks and their business rules.
- **Identity** — owns ASP.NET Core Identity user/role persistence and authentication/account operations.
- **Audit** — owns the durable append-only business audit trail.

**Do not silently assume this recommendation is approved.** Prompts 02–07 are explicit decision/ADR gates. Downstream prompts require the approved ADRs to exist.

## Mandatory execution contract for every prompt

Before changing code, Claude must:

1. Read root `CLAUDE.md`.
2. Read the prompt's concise `REQUIREMENTS` callout and follow its links to the canonical requirements document. The canonical requirement text is the source of truth.
3. Read the matching `.claude/rules/*.md`.
4. Use the matching `.claude/skills/*/SKILL.md` when one exists.
5. Inspect existing code before deciding that an artifact is missing.
6. State the **single primary change**.
7. List the exact files expected to change.
8. Stop without editing if the change would require an unresolved architecture, security, public-contract, persistence-ownership, or deployment-topology decision.
9. Make only the primary change.
10. Run the smallest meaningful verification.
11. Report:
    - files changed,
    - verification command(s),
    - pass/fail,
    - requirement IDs advanced,
    - follow-up prompt number,
    - and then **STOP**.

### Atomicity rule

A prompt may modify multiple files **only when those files together are one indivisible seam**. Examples:

- an interface and its implementation;
- an entity and its unit test;
- an EF configuration and a focused SQL integration test;
- one controller action and its HTTP contract test.

A prompt may **not** implement the next architectural layer merely because it is convenient.

### SCRUB skeleton

```text
REQUIREMENTS:
  TRACEABILITY: <requirement IDs>
  REQUIREMENT LINKS: <relative links to canonical requirements>
  REQUIREMENT INTENT: <2–4 concise sentences summarizing the applicable behavior>
  SOURCE OF TRUTH: Read the linked requirement(s) before coding; stop on drift.

SCOPE:        one concrete action and its exact boundary
CONSTRAINT:   architecture, stack, rules, invariants, and test expectations
RESTRICTION:  adjacent work that must NOT be started
USAGE:        rules / skills / agents / tools / commands to use
BEHAVIOR:     inspect -> change one thing -> verify -> report -> STOP
```

---

# Part 0 — Discovery, architecture gates, and traceability

## Prompt 000 — Inventory the repository without changing it

```text
REQUIREMENTS:
  TRACEABILITY: Requirements governance; all requirement families
  REQUIREMENT LINKS: [Requirements governance](../requirements/lightweight-crm-product-and-system-requirements.md#48-requirements-governance); [Project Chicago requirements](../requirements/lightweight-crm-product-and-system-requirements.md)
  REQUIREMENT INTENT: The canonical requirements are the product/system source of truth and must remain lightweight, secure, auditable, traceable, observable, testable, and operationally diagnosable. Do not invent missing business behavior or silently resolve an open requirement.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Inspect the repository and produce a current-state inventory only.

CONSTRAINT: Read CLAUDE.md, the complete .claude folder, docs, solution/project/package files if present, and the current docs/prompts/scrub-prompts.md. Identify what code exists versus what is only documented.

RESTRICTION: Do not create, edit, rename, or delete files. Do not install packages. Do not run migrations. Do not make architecture decisions.

USAGE: Use read-only repository tools. Pay special attention to .claude/rules/{backend,functions,messaging,database,gateway,identity,frontend,audit,aspire}.md and the listed skills.

BEHAVIOR: Report the repository tree, current project state, unresolved decisions from CLAUDE.md, obsolete prompt assumptions, and the smallest build/test commands currently possible. Verify by showing git status is unchanged. STOP.
```

## Prompt 001 — Bind the requirements source and build an ID index

```text
REQUIREMENTS:
  TRACEABILITY: All requirement IDs
  REQUIREMENT LINKS: [Project Chicago requirements](../requirements/lightweight-crm-product-and-system-requirements.md)
  REQUIREMENT INTENT: The canonical requirements are the product/system source of truth and must remain lightweight, secure, auditable, traceable, observable, testable, and operationally diagnosable. Do not invent missing business behavior or silently resolve an open requirement.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Locate the authoritative Project Chicago requirements markdown and build a read-only index of its requirement IDs grouped by domain.

CONSTRAINT: The correct file contains the CLIENT, PROJECT, TASK, SEC, TRACE, OTEL, OBS, AUDIT, ASYNC, OUTBOX, API, DATA, UX, ACCESS, TEST, DEPLOY and OPS families. Treat that document as product source of truth.

RESTRICTION: Do not modify the requirements file. Do not infer missing requirements. Do not start architecture or code changes.

USAGE: Use read-only search. Compare requirement IDs with CLAUDE.md constraints.

BEHAVIOR: Report the exact requirements-file path, duplicate/missing IDs if any, and the grouped index. Verify no file changed. STOP.
```

## Prompt 002 — Propose the bounded-context catalog

```text
REQUIREMENTS:
  TRACEABILITY: PR-001..006; DATA-030..034; SEC-001..016; AUDIT-001..008
  REQUIREMENT LINKS: [PR-001..006](../requirements/lightweight-crm-product-and-system-requirements.md#pr-001); [DATA-030..034](../requirements/lightweight-crm-product-and-system-requirements.md#data-030); [SEC-001..016](../requirements/lightweight-crm-product-and-system-requirements.md#sec-001); [AUDIT-001..008](../requirements/lightweight-crm-product-and-system-requirements.md#audit-001)
  REQUIREMENT INTENT: Keep Project Chicago lightweight and Client-centric while making traceability, auditability, security, and observability default system behaviors. Use Microsoft SQL Server/Azure SQL, one database per bounded service, no cross-service database queries, and controlled schema migrations. Also satisfy the remaining linked cross-cutting constraints that apply to this atomic step.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Produce an architecture recommendation for the initial bounded-context catalog only.

CONSTRAINT: Use the lightweight business scope and current CLAUDE.md. Evaluate the recommended shape: Crm owns Clients/Projects/Tasks; Identity owns ASP.NET Core Identity; Audit owns the append-only audit trail. Each service must have one API host, one .Core, one .Functions project and exactly one SQL database.

RESTRICTION: Do not scaffold projects. Do not edit CLAUDE.md. Do not invent additional services such as Reporting, Notifications, Search, or Workflow.

USAGE: Use read-only analysis and current architecture rules.

BEHAVIOR: Return one recommended catalog, responsibilities, database ownership, reasons, and rejected alternatives. Explicitly ask for approval of this architecture decision and STOP.
```

## Prompt 003 — Record the approved bounded-context ADR

```text
REQUIREMENTS:
  TRACEABILITY: PR-001..006; DATA-031..034
  REQUIREMENT LINKS: [PR-001..006](../requirements/lightweight-crm-product-and-system-requirements.md#pr-001); [DATA-031..034](../requirements/lightweight-crm-product-and-system-requirements.md#data-031)
  REQUIREMENT INTENT: Keep Project Chicago lightweight and Client-centric while making traceability, auditability, security, and observability default system behaviors. Use SQL Server/Azure SQL with service-owned databases, enforced relationships, UTC timestamps, safe public IDs, optimistic concurrency, and archival over routine hard deletion.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: After the user has approved Prompt 002's catalog, create one ADR recording that exact bounded-context decision.

CONSTRAINT: The ADR must name service responsibilities, database ownership, allowed interaction modes, three-project service shape, and the rule that Clients/Projects/Tasks remain in the Crm boundary unless a later ADR changes it.

RESTRICTION: Do not create code projects. Do not modify unrelated ADRs. Do not add implementation details beyond the approved decision.

USAGE: Follow the repository ADR/documentation convention discovered in Prompt 000.

BEHAVIOR: Create the ADR only. Verify the file is valid markdown and contains the approved service names and one-database-per-service rule. Report and STOP.
```

## Prompt 004 — Update CLAUDE.md with the approved service catalog

```text
REQUIREMENTS:
  TRACEABILITY: Requirements governance; DATA-031..034
  REQUIREMENT LINKS: [Requirements governance](../requirements/lightweight-crm-product-and-system-requirements.md#48-requirements-governance); [DATA-031..034](../requirements/lightweight-crm-product-and-system-requirements.md#data-031)
  REQUIREMENT INTENT: The requirements document is the functional source of truth; implement only referenced behavior and stop rather than inventing missing decisions. Use SQL Server/Azure SQL with service-owned databases, enforced relationships, UTC timestamps, safe public IDs, optimistic concurrency, and archival over routine hard deletion.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Update only the bounded-service catalog/open-decision portions of CLAUDE.md to reflect the ADR approved and recorded in Prompt 003.

CONSTRAINT: Preserve every existing Functions, SQL Server, YARP, PCDS, Identity, messaging, testing and observability rule not superseded by the ADR.

RESTRICTION: Do not rewrite CLAUDE.md wholesale. Do not change auth transport, Service Bus topology, audit retention, or observability exporter decisions.

USAGE: Read the ADR from Prompt 003 and edit only the minimum CLAUDE.md lines.

BEHAVIOR: Verify the service catalog is no longer listed as open, the three-project pattern remains intact, and no unrelated architecture rule changed. STOP.
```

## Prompt 005 — Propose the browser authentication/session decision

```text
REQUIREMENTS:
  TRACEABILITY: SEC-001..025
  REQUIREMENT LINKS: [SEC-001..025](../requirements/lightweight-crm-product-and-system-requirements.md#sec-001)
  REQUIREMENT INTENT: Use ASP.NET Core Identity and server-side role/policy authorization with least privilege; protect APIs and never expose passwords, tokens, secrets, or sensitive internals.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Produce the security decision for browser authentication/session transport only.

CONSTRAINT: ASP.NET Core Identity remains the credential/user framework. Compare the smallest production-suitable options for a client-side React application behind YARP, including secure cookie/session and token approaches. Address CSRF, token storage, revocation, multi-service authorization context, HTTPS, and 401 versus 403 semantics.

RESTRICTION: Do not implement authentication. Do not change Identity framework. Do not create user tables. Do not change gateway routing.

USAGE: Read .claude/rules/identity.md, gateway.md, backend.md and the security requirements. Use current official Microsoft guidance when version-sensitive details matter.

BEHAVIOR: Recommend one transport/session strategy with concrete security invariants, rejected alternatives, and test implications. Ask for approval and STOP.
```

## Prompt 006 — Record the approved authentication ADR

```text
REQUIREMENTS:
  TRACEABILITY: SEC-001..025
  REQUIREMENT LINKS: [SEC-001..025](../requirements/lightweight-crm-product-and-system-requirements.md#sec-001)
  REQUIREMENT INTENT: Use ASP.NET Core Identity and server-side role/policy authorization with least privilege; protect APIs and never expose passwords, tokens, secrets, or sensitive internals.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Create one ADR containing only the authentication/session decision approved after Prompt 005.

CONSTRAINT: Record ASP.NET Core Identity ownership, browser transport, session/token lifetime model, logout/revocation behavior, trusted-claims propagation, CSRF stance, and 401/403 semantics.

RESTRICTION: Do not implement code, MFA, passkeys, external providers, or account recovery beyond what was explicitly approved.

USAGE: Use the repository ADR convention.

BEHAVIOR: Verify the ADR contains no unresolved placeholder for the approved transport/session strategy. Report and STOP.
```

## Prompt 007 — Record the audit architecture and retention decision

```text
REQUIREMENTS:
  TRACEABILITY: AUDIT-001..008; DATA-020..023; PRIV-001..005
  REQUIREMENT LINKS: [AUDIT-001..008](../requirements/lightweight-crm-product-and-system-requirements.md#audit-001); [DATA-020..023](../requirements/lightweight-crm-product-and-system-requirements.md#data-020); [PRIV-001..005](../requirements/lightweight-crm-product-and-system-requirements.md#priv-001)
  REQUIREMENT INTENT: Every Client/Project/Task mutation produces append-only audit evidence with entity/action/time/actor/source, trace/correlation, and applicable before/after values; secrets are redacted. Normal workflows archive rather than destructively delete records; history remains available and permanent purge is privileged and retention/privacy governed. Collect only necessary CRM data, minimize sensitive duplication and PII in telemetry, enforce authorization, and document retention before production.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Create an ADR for full business auditability using the approved Audit bounded context.

CONSTRAINT: Define append-only audit ownership, event-driven ingestion through Service Bus, required audit fields, previous/new changed values when applicable, actor/system identity, trace/correlation linkage, redaction rules, ordering, retention policy placeholder or approved duration, and purge authority.

RESTRICTION: Do not scaffold Audit projects. Do not use application logs as the audit store. Do not allow owning services to write AuditDb directly.

USAGE: Read .claude/rules/audit.md and messaging.md plus AUDIT requirements.

BEHAVIOR: Verify the ADR explicitly separates operational telemetry from durable business audit records and defines how mutations reach Audit through outbox -> bus -> Audit Functions. Report and STOP.
```

## Prompt 008 — Propose Service Bus topology

```text
REQUIREMENTS:
  TRACEABILITY: ASYNC-001..008; OUTBOX-001..006; TRACE-003..007
  REQUIREMENT LINKS: [ASYNC-001..008](../requirements/lightweight-crm-product-and-system-requirements.md#async-001); [OUTBOX-001..006](../requirements/lightweight-crm-product-and-system-requirements.md#outbox-001); [TRACE-003..007](../requirements/lightweight-crm-product-and-system-requirements.md#trace-003)
  REQUIREMENT INTENT: Durable async work uses Azure Service Bus and Service Bus-triggered Functions with trace correlation, duplicate tolerance/idempotency, bounded retry behavior, and dead-letter visibility. When a transaction changes state and publishes an event, state and outbox commit together; a timer Function relays pending messages idempotently and exposes backlog/failure metrics. Propagate W3C distributed trace context across gateway, APIs, SQL, Service Bus, and Functions so an operation can be followed cradle to grave.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Propose the initial Azure Service Bus topology needed by Crm, Identity and Audit only.

CONSTRAINT: Keep entity names in infrastructure/configuration, support at-least-once delivery, Audit consumption, correlation/causation metadata, dead-letter handling and least privilege. Prefer the smallest topology that supports current requirements.

RESTRICTION: Do not implement resources. Do not invent business workflows between Crm and Identity. Do not encode domain rules as broker filters.

USAGE: Read .claude/rules/messaging.md, functions.md, aspire.md and the approved service catalog/audit ADRs.

BEHAVIOR: Return topic/queue/subscription recommendation, filters if any, publisher/consumer permissions, retry/DLQ expectations and rejected alternatives. Ask for approval and STOP.
```

## Prompt 009 — Record the approved Service Bus topology ADR

```text
REQUIREMENTS:
  TRACEABILITY: ASYNC-001..008; OUTBOX-001..006
  REQUIREMENT LINKS: [ASYNC-001..008](../requirements/lightweight-crm-product-and-system-requirements.md#async-001); [OUTBOX-001..006](../requirements/lightweight-crm-product-and-system-requirements.md#outbox-001)
  REQUIREMENT INTENT: Durable async work uses Azure Service Bus and Service Bus-triggered Functions with trace correlation, duplicate tolerance/idempotency, bounded retry behavior, and dead-letter visibility. When a transaction changes state and publishes an event, state and outbox commit together; a timer Function relays pending messages idempotently and exposes backlog/failure metrics.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Create one ADR containing the topology approved after Prompt 008.

CONSTRAINT: Record logical entity names, publisher/consumer ownership, configuration keys, retry/dead-letter expectations and the rule that topology stays out of domain code.

RESTRICTION: Do not create Aspire resources or Function bindings yet.

USAGE: Use the repository ADR convention and .claude/rules/messaging.md.

BEHAVIOR: Verify every publishing/consuming bounded context can be mapped to an approved entity without ambiguity. Report and STOP.
```

## Prompt 010 — Record the observability architecture ADR

```text
REQUIREMENTS:
  TRACEABILITY: TRACE-001..007; OTEL-001..006; OBS-001..005; LOG-001..006; OPS-001..004
  REQUIREMENT LINKS: [TRACE-001..007](../requirements/lightweight-crm-product-and-system-requirements.md#trace-001); [OTEL-001..006](../requirements/lightweight-crm-product-and-system-requirements.md#otel-001); [OBS-001..005](../requirements/lightweight-crm-product-and-system-requirements.md#obs-001); [LOG-001..006](../requirements/lightweight-crm-product-and-system-requirements.md#log-001); [OPS-001..004](../requirements/lightweight-crm-product-and-system-requirements.md#ops-001)
  REQUIREMENT INTENT: Every inbound request participates in a trace propagated through gateway, services, SQL, HTTP, Service Bus, Functions, and downstream work with safe diagnostic metadata. Every API/service/Function uses OpenTelemetry for traces, metrics, and log correlation, including dependency instrumentation and meaningful business spans where needed. Also satisfy the remaining linked cross-cutting constraints that apply to this atomic step.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Create the observability ADR establishing OpenTelemetry as the instrumentation standard and Azure Monitor/Application Insights as the production single pane of glass.

CONSTRAINT: Define W3C trace context, service/resource naming, correlation/causation relationship, log-trace correlation, Azure Functions participation, Service Bus propagation, SQL dependency tracing, safe business identifiers, environment/service/version attributes, sampling ownership, and local Aspire Dashboard behavior.

RESTRICTION: Do not implement instrumentation. Do not log customer payloads or secrets. Do not replace OpenTelemetry with a proprietary logging-only approach.

USAGE: Read CLAUDE.md observability rules and current official OpenTelemetry/Azure Monitor guidance if package/API details are mentioned.

BEHAVIOR: Verify the ADR can answer how an operator traces Browser -> YARP -> API -> SQL -> Outbox -> Timer Function -> Service Bus -> Consumer Function -> SQL. Report and STOP.
```

## Prompt 011 — Create the requirements-to-prompt traceability matrix

```text
REQUIREMENTS:
  TRACEABILITY: Requirements governance; all requirement families
  REQUIREMENT LINKS: [Requirements governance](../requirements/lightweight-crm-product-and-system-requirements.md#48-requirements-governance); [Project Chicago requirements](../requirements/lightweight-crm-product-and-system-requirements.md)
  REQUIREMENT INTENT: The canonical requirements are the product/system source of truth and must remain lightweight, secure, auditable, traceable, observable, testable, and operationally diagnosable. Do not invent missing business behavior or silently resolve an open requirement.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Create a markdown traceability matrix mapping every requirement ID in the authoritative requirements file to at least one prompt number in this sequence.

CONSTRAINT: Mark architecture-decision, implementation, test, and final-verification coverage separately where useful.

RESTRICTION: Do not change application code or requirements. Do not mark a requirement covered by a prompt that does not actually verify it.

USAGE: Use the requirements index from Prompt 001 and this prompt document.

BEHAVIOR: Verify every requirement ID appears at least once or is explicitly marked deferred with rationale. Report uncovered IDs and STOP.
```

---

# Part 1 — Solution, shared platform, observability, and edge foundation

## Prompt 012 — Create the .NET solution file

```text
REQUIREMENTS:
  TRACEABILITY: DEPLOY-001; TEST-001..007
  REQUIREMENT LINKS: [DEPLOY-001](../requirements/lightweight-crm-product-and-system-requirements.md#deploy-001); [TEST-001..007](../requirements/lightweight-crm-product-and-system-requirements.md#test-001)
  REQUIREMENT INTENT: Support environment-specific configuration, externalized secrets, Flex Consumption Functions, and consistent deployment/telemetry metadata. Automated tests cover business rules, authorization, APIs, SQL-compatible persistence, message consumers, audit generation, and representative distributed tracing.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Create the Project Chicago solution file only if one does not already exist.

CONSTRAINT: Use the repository SDK/version conventions. Preserve central build/package conventions if already present.

RESTRICTION: Do not create projects, solution folders, packages, or source files.

USAGE: Use the dotnet CLI.

BEHAVIOR: Verify with `dotnet sln <solution> list` and `git diff --check`. Report and STOP.
```

## Prompt 013 — Create repository-wide build defaults

```text
REQUIREMENTS:
  TRACEABILITY: TEST-001..007; DEPLOY-001
  REQUIREMENT LINKS: [TEST-001..007](../requirements/lightweight-crm-product-and-system-requirements.md#test-001); [DEPLOY-001](../requirements/lightweight-crm-product-and-system-requirements.md#deploy-001)
  REQUIREMENT INTENT: Automated tests cover business rules, authorization, APIs, SQL-compatible persistence, message consumers, audit generation, and representative distributed tracing. Support environment-specific configuration, externalized secrets, Flex Consumption Functions, and consistent deployment/telemetry metadata.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Create or minimally update Directory.Build.props with repository-wide .NET build defaults only.

CONSTRAINT: Target the approved .NET 10 baseline; enable nullable and implicit usings only if consistent with CLAUDE.md; enable analyzers/warnings policy without adding feature-specific settings.

RESTRICTION: Do not add package versions, project references, domain conventions, or deployment settings.

USAGE: Inspect existing files first; use normal MSBuild conventions.

BEHAVIOR: Run a syntax/build evaluation appropriate for an otherwise-empty solution and `git diff --check`. STOP.
```

## Prompt 014 — Create central package management

```text
REQUIREMENTS:
  TRACEABILITY: DEPLOY-001; TEST-001..007
  REQUIREMENT LINKS: [DEPLOY-001](../requirements/lightweight-crm-product-and-system-requirements.md#deploy-001); [TEST-001..007](../requirements/lightweight-crm-product-and-system-requirements.md#test-001)
  REQUIREMENT INTENT: Support environment-specific configuration, externalized secrets, Flex Consumption Functions, and consistent deployment/telemetry metadata. Automated tests cover business rules, authorization, APIs, SQL-compatible persistence, message consumers, audit generation, and representative distributed tracing.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Create or minimally update Directory.Packages.props to establish central package management only.

CONSTRAINT: Do not guess package versions: verify current compatible versions when packages are actually added. The file may initially contain only central-management plumbing.

RESTRICTION: Do not add feature packages merely to pre-populate the file.

USAGE: Use MSBuild central package management conventions.

BEHAVIOR: Verify XML parses and `dotnet restore` does not fail solely because of this change. STOP.
```

## Prompt 015 — Create ProjectChicago.AppHost

```text
REQUIREMENTS:
  TRACEABILITY: DEPLOY-001; OPS-001..004
  REQUIREMENT LINKS: [DEPLOY-001](../requirements/lightweight-crm-product-and-system-requirements.md#deploy-001); [OPS-001..004](../requirements/lightweight-crm-product-and-system-requirements.md#ops-001)
  REQUIREMENT INTENT: Support environment-specific configuration, externalized secrets, Flex Consumption Functions, and consistent deployment/telemetry metadata. Operators can determine service health and detect rising errors/latency, SQL or Service Bus failures, Function failures, auth anomalies, dead letters, and outbox backlog.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Create the Aspire AppHost project only.

CONSTRAINT: Use the repository-approved/current Aspire template compatible with .NET 10. AppHost is declarative orchestration only.

RESTRICTION: Do not add SQL Server, Service Bus, services, gateway, web, or business logic.

USAGE: Use the add-aspire-resource skill and verify current Aspire CLI/template names before execution.

BEHAVIOR: Add the project to the solution and run `dotnet build` for AppHost. Report and STOP.
```

## Prompt 016 — Create ProjectChicago.ServiceDefaults

```text
REQUIREMENTS:
  TRACEABILITY: OTEL-001..006; OPS-001..004
  REQUIREMENT LINKS: [OTEL-001..006](../requirements/lightweight-crm-product-and-system-requirements.md#otel-001); [OPS-001..004](../requirements/lightweight-crm-product-and-system-requirements.md#ops-001)
  REQUIREMENT INTENT: Every API/service/Function uses OpenTelemetry for traces, metrics, and log correlation, including dependency instrumentation and meaningful business spans where needed. Operators can determine service health and detect rising errors/latency, SQL or Service Bus failures, Function failures, auth anomalies, dead letters, and outbox backlog.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Create the Aspire ServiceDefaults project only.

CONSTRAINT: Use the current template compatible with the AppHost and .NET 10.

RESTRICTION: Do not add Project Chicago-specific telemetry exporters or service references yet.

USAGE: Verify the current Aspire template against official docs.

BEHAVIOR: Add to solution, build ServiceDefaults, report generated default health/telemetry capabilities, and STOP.
```

## Prompt 017 — Create ProjectChicago.Contracts

```text
REQUIREMENTS:
  TRACEABILITY: ASYNC-001..008; OUTBOX-001..006
  REQUIREMENT LINKS: [ASYNC-001..008](../requirements/lightweight-crm-product-and-system-requirements.md#async-001); [OUTBOX-001..006](../requirements/lightweight-crm-product-and-system-requirements.md#outbox-001)
  REQUIREMENT INTENT: Durable async work uses Azure Service Bus and Service Bus-triggered Functions with trace correlation, duplicate tolerance/idempotency, bounded retry behavior, and dead-letter visibility. When a transaction changes state and publishes an event, state and outbox commit together; a timer Function relays pending messages idempotently and exposes backlog/failure metrics.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Create the Contracts class library only.

CONSTRAINT: Contracts is a leaf containing integration-event contracts only.

RESTRICTION: Do not add CRM domain entities, ServiceModels, audit storage models, EF packages, or service references.

USAGE: Use dotnet CLI and CLAUDE.md reference-direction rules.

BEHAVIOR: Add to solution and build. Verify it has no project reference to Shared or a service. STOP.
```

## Prompt 018 — Create ProjectChicago.Shared

```text
REQUIREMENTS:
  TRACEABILITY: TRACE-001..007; OTEL-001..006; OUTBOX-001..006; ERROR-001..005
  REQUIREMENT LINKS: [TRACE-001..007](../requirements/lightweight-crm-product-and-system-requirements.md#trace-001); [OTEL-001..006](../requirements/lightweight-crm-product-and-system-requirements.md#otel-001); [OUTBOX-001..006](../requirements/lightweight-crm-product-and-system-requirements.md#outbox-001); [ERROR-001..005](../requirements/lightweight-crm-product-and-system-requirements.md#error-001)
  REQUIREMENT INTENT: Every inbound request participates in a trace propagated through gateway, services, SQL, HTTP, Service Bus, Functions, and downstream work with safe diagnostic metadata. Every API/service/Function uses OpenTelemetry for traces, metrics, and log correlation, including dependency instrumentation and meaningful business spans where needed. Also satisfy the remaining linked cross-cutting constraints that apply to this atomic step.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Create the Shared class library only and add the approved reference to Contracts if required by the architecture.

CONSTRAINT: Shared contains cross-cutting mechanisms only.

RESTRICTION: Do not add CRM/Identity/Audit domain behavior, entities, or direct service references.

USAGE: Use dotnet CLI and CLAUDE.md reference-direction rules.

BEHAVIOR: Add to solution, build, and verify the project reference graph is acyclic. STOP.
```

## Prompt 019 — Create ProjectChicago.Gateway

```text
REQUIREMENTS:
  TRACEABILITY: SEC-020..025; TRACE-001..007; API-001..007
  REQUIREMENT LINKS: [SEC-020..025](../requirements/lightweight-crm-product-and-system-requirements.md#sec-020); [TRACE-001..007](../requirements/lightweight-crm-product-and-system-requirements.md#trace-001); [API-001..007](../requirements/lightweight-crm-product-and-system-requirements.md#api-001)
  REQUIREMENT INTENT: Public API access goes through the Project Chicago gateway over HTTPS with validated inputs and safe logging that excludes credentials, tokens, secrets, and unnecessary PII. Every inbound request participates in a trace propagated through gateway, services, SQL, HTTP, Service Bus, Functions, and downstream work with safe diagnostic metadata. Use consistent REST routes, conventional HTTP verbs/status codes, bounded pagination, OpenAPI documentation, and versionable public contracts.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Create the YARP gateway project only.

CONSTRAINT: Gateway is the sole browser-facing backend edge and must remain persistence/broker-free.

RESTRICTION: Do not add routes, SQL, Service Bus, auth policy, or business middleware yet.

USAGE: Use current YARP/Aspire conventions and .claude/rules/gateway.md.

BEHAVIOR: Add to solution and build. Verify no SQL/Service Bus package reference was added. STOP.
```

## Prompt 020 — Create the React 19 Vite application

```text
REQUIREMENTS:
  TRACEABILITY: UX-001..006; ACCESS-001..005; DESIGN-001..004
  REQUIREMENT LINKS: [UX-001..006](../requirements/lightweight-crm-product-and-system-requirements.md#ux-001); [ACCESS-001..005](../requirements/lightweight-crm-product-and-system-requirements.md#access-001); [DESIGN-001..004](../requirements/lightweight-crm-product-and-system-requirements.md#design-001)
  REQUIREMENT INTENT: The UI prioritizes simple workflows with clear validation/save/failure/loading/empty/unauthorized states, explicit destructive intent, and responsive desktop/tablet behavior. Frontend behavior targets WCAG 2.2 AA with keyboard access, labels, associated validation messages, and non-color-only state. Frontend features use local PCDS components and shared typography/spacing/color/border/elevation/state/layout tokens instead of recreating them.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Create the client-side React 19 + TypeScript + Vite application under `src/web` only.

CONSTRAINT: Use strict TypeScript. Preserve client-side-only architecture.

RESTRICTION: Do not add feature pages, API calls, auth, Tailwind customization, PCDS components, or internal service URLs.

USAGE: Follow .claude/rules/frontend.md and verify current Vite/React scaffolding commands.

BEHAVIOR: Run the generated web build and report package versions. STOP.
```

## Prompt 021 — Install Tailwind CSS v4 into the React app

```text
REQUIREMENTS:
  TRACEABILITY: DESIGN-001..004; UX-001..006
  REQUIREMENT LINKS: [DESIGN-001..004](../requirements/lightweight-crm-product-and-system-requirements.md#design-001); [UX-001..006](../requirements/lightweight-crm-product-and-system-requirements.md#ux-001)
  REQUIREMENT INTENT: Frontend features use local PCDS components and shared typography/spacing/color/border/elevation/state/layout tokens instead of recreating them. The UI prioritizes simple workflows with clear validation/save/failure/loading/empty/unauthorized states, explicit destructive intent, and responsive desktop/tablet behavior.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Add Tailwind CSS v4 using the Vite integration only.

CONSTRAINT: Preserve strict React/Vite client architecture and existing package-manager strategy.

RESTRICTION: Do not create brand tokens, feature styles, or a competing design system.

USAGE: Follow frontend.md and current official Tailwind v4 Vite guidance.

BEHAVIOR: Run web build and verify Tailwind is processed. STOP.
```

## Prompt 022 — Copy PCDS into the local design-system source

```text
REQUIREMENTS:
  TRACEABILITY: DESIGN-001..004; ACCESS-001..005
  REQUIREMENT LINKS: [DESIGN-001..004](../requirements/lightweight-crm-product-and-system-requirements.md#design-001); [ACCESS-001..005](../requirements/lightweight-crm-product-and-system-requirements.md#access-001)
  REQUIREMENT INTENT: Frontend features use local PCDS components and shared typography/spacing/color/border/elevation/state/layout tokens instead of recreating them. Frontend behavior targets WCAG 2.2 AA with keyboard access, labels, associated validation messages, and non-color-only state.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Copy the approved PCDS source into Project Chicago's local `src/web/src/design-system` location (or the location explicitly established by the repo) only.

CONSTRAINT: Treat the copied source as authoritative local code. Preserve its tokens, recipes, primitives, theme mechanism and exports.

RESTRICTION: Do not rewrite PCDS, redesign tokens, or create feature pages. Do not use upstream PCDS at runtime.

USAGE: Follow frontend.md and add-component skill. Inspect the source being copied before applying.

BEHAVIOR: Run the web build and any PCDS tests/lint included in the copied source. Verify no duplicate design-system directory exists. STOP.
```

## Prompt 023 — Wire ServiceDefaults into the Gateway

```text
REQUIREMENTS:
  TRACEABILITY: OTEL-001..006; OPS-001..004
  REQUIREMENT LINKS: [OTEL-001..006](../requirements/lightweight-crm-product-and-system-requirements.md#otel-001); [OPS-001..004](../requirements/lightweight-crm-product-and-system-requirements.md#ops-001)
  REQUIREMENT INTENT: Every API/service/Function uses OpenTelemetry for traces, metrics, and log correlation, including dependency instrumentation and meaningful business spans where needed. Operators can determine service health and detect rising errors/latency, SQL or Service Bus failures, Function failures, auth anomalies, dead letters, and outbox backlog.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Add the ServiceDefaults project reference and standard service-default registration to Gateway only.

CONSTRAINT: Program.cs remains composition only.

RESTRICTION: Do not configure routes, authentication, custom telemetry exporters, SQL, or Service Bus.

USAGE: Follow .claude/rules/gateway.md and aspire.md.

BEHAVIOR: Build Gateway and verify default health endpoints/telemetry registration compile. STOP.
```

## Prompt 024 — Add Azure Monitor OpenTelemetry export to ServiceDefaults

```text
REQUIREMENTS:
  TRACEABILITY: OTEL-001..006; OBS-001..005
  REQUIREMENT LINKS: [OTEL-001..006](../requirements/lightweight-crm-product-and-system-requirements.md#otel-001); [OBS-001..005](../requirements/lightweight-crm-product-and-system-requirements.md#obs-001)
  REQUIREMENT INTENT: Every API/service/Function uses OpenTelemetry for traces, metrics, and log correlation, including dependency instrumentation and meaningful business spans where needed. Azure Monitor/Application Insights provides centralized investigation and dashboards for request/dependency/Function/Service Bus/SQL health, errors, latency, and trace/entity filtering.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Add the production OpenTelemetry exporter configuration to ServiceDefaults only, using Azure Monitor/Application Insights according to the approved observability ADR.

CONSTRAINT: Exporter configuration must be environment/configuration driven; local development may continue using Aspire Dashboard OTLP behavior. Resource attributes must include service name/version/environment.

RESTRICTION: Do not add service-specific spans or hardcode Application Insights connection strings.

USAGE: Use current official Azure Monitor OpenTelemetry .NET guidance; preserve standard Aspire telemetry.

BEHAVIOR: Add focused configuration tests if feasible and build ServiceDefaults plus Gateway. Verify exporter activation is conditional/config-driven. STOP.
```

## Prompt 025 — Add SQL and Service Bus tracing instrumentation to ServiceDefaults

```text
REQUIREMENTS:
  TRACEABILITY: TRACE-003..007; OTEL-003..006
  REQUIREMENT LINKS: [TRACE-003..007](../requirements/lightweight-crm-product-and-system-requirements.md#trace-003); [OTEL-003..006](../requirements/lightweight-crm-product-and-system-requirements.md#otel-003)
  REQUIREMENT INTENT: Propagate W3C distributed trace context across gateway, APIs, SQL, Service Bus, and Functions so an operation can be followed cradle to grave. Instrument APIs, services, Functions, SQL, HTTP, and Service Bus with OpenTelemetry for traces, metrics, and correlated structured logs.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Extend ServiceDefaults with approved OpenTelemetry instrumentation needed for SQL client/EF and Azure Service Bus dependencies only.

CONSTRAINT: Use Activity/W3C-compatible instrumentation and avoid recording sensitive SQL parameters or message bodies.

RESTRICTION: Do not add custom business spans or service-specific code.

USAGE: Use current official OpenTelemetry/Azure SDK instrumentation packages compatible with the pinned stack.

BEHAVIOR: Build ServiceDefaults and a consuming host. Verify instrumentation is registered once and no sensitive-data options are enabled. STOP.
```

## Prompt 026 — Create the shared correlation context abstraction

```text
REQUIREMENTS:
  TRACEABILITY: TRACE-001..007; LOG-003; AUDIT-002
  REQUIREMENT LINKS: [TRACE-001..007](../requirements/lightweight-crm-product-and-system-requirements.md#trace-001); [LOG-003](../requirements/lightweight-crm-product-and-system-requirements.md#log-003); [AUDIT-002](../requirements/lightweight-crm-product-and-system-requirements.md#audit-002)
  REQUIREMENT INTENT: Every inbound request participates in a trace propagated through gateway, services, SQL, HTTP, Service Bus, Functions, and downstream work with safe diagnostic metadata. Use structured trace-correlated logs without sensitive payload leakage or duplicate exception logging at every layer. Every business mutation must create append-only audit evidence describing what changed, when, who caused it, and applicable before/after values while preserving trace correlation and redacting secrets.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Create the Shared correlation/request context abstraction and immutable value type(s) only.

CONSTRAINT: Support TraceId, CorrelationId, CausationId, actor ID/category when available, and request/message identifier without depending on HttpContext in Shared.

RESTRICTION: Do not add HTTP middleware, Function adapters, audit persistence, or Service Bus code.

USAGE: Follow CLAUDE.md observability/messaging rules.

BEHAVIOR: Add unit tests for valid generation/propagation semantics and build Shared. STOP.
```

## Prompt 026A — Add the HTTP request/actor context adapter

```text
REQUIREMENTS:
  TRACEABILITY: TRACE-001..007; SEC-010..013; AUDIT-002; LOG-003
  REQUIREMENT LINKS: [TRACE-001..007](../requirements/lightweight-crm-product-and-system-requirements.md#trace-001); [SEC-010..013](../requirements/lightweight-crm-product-and-system-requirements.md#sec-010); [AUDIT-002](../requirements/lightweight-crm-product-and-system-requirements.md#audit-002); [LOG-003](../requirements/lightweight-crm-product-and-system-requirements.md#log-003)
  REQUIREMENT INTENT: Every inbound request participates in a trace propagated through gateway, services, SQL, HTTP, Service Bus, Functions, and downstream work with safe diagnostic metadata. Use ASP.NET Core Identity and server-side role/policy authorization with least privilege; protect APIs and never expose passwords, tokens, secrets, or sensitive internals. Also satisfy the remaining linked cross-cutting constraints that apply to this atomic step.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Add the HTTP-host adapter that maps validated ASP.NET Core authentication/Activity state into the shared correlation/actor context abstraction only.

CONSTRAINT: Resolve TraceId/CorrelationId from the established trace/correlation pipeline and actor ID/roles from trusted authenticated ClaimsPrincipal. Keep the shared abstraction independent of HttpContext.

RESTRICTION: Do not configure authentication, authorization policies, audit persistence, controller actions, or Function message context.

USAGE: Follow backend.md, identity.md and the approved observability/auth ADRs.

BEHAVIOR: Add focused tests for authenticated, anonymous and malformed/untrusted header cases; build the host/shared projects and STOP.
```

## Prompt 027 — Add gateway correlation normalization middleware

```text
REQUIREMENTS:
  TRACEABILITY: TRACE-001..007; LOG-003; SEC-020..025
  REQUIREMENT LINKS: [TRACE-001..007](../requirements/lightweight-crm-product-and-system-requirements.md#trace-001); [LOG-003](../requirements/lightweight-crm-product-and-system-requirements.md#log-003); [SEC-020..025](../requirements/lightweight-crm-product-and-system-requirements.md#sec-020)
  REQUIREMENT INTENT: Every inbound request participates in a trace propagated through gateway, services, SQL, HTTP, Service Bus, Functions, and downstream work with safe diagnostic metadata. Use structured trace-correlated logs without sensitive payload leakage or duplicate exception logging at every layer. Public API access goes through the Project Chicago gateway over HTTPS with validated inputs and safe logging that excludes credentials, tokens, secrets, and unnecessary PII.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Add gateway middleware that accepts/creates the approved correlation identifier, participates in W3C trace context, and returns the safe correlation reference to callers.

CONSTRAINT: Use Activity.Current as the distributed trace source of truth; normalize only approved correlation headers; log structured IDs without payloads.

RESTRICTION: Do not implement CRM logic, auth, service routes, or custom trace exporters.

USAGE: Follow gateway.md and approved observability ADR.

BEHAVIOR: Add gateway tests for new ID, valid incoming ID, invalid/oversized ID handling and response propagation. Run focused tests and STOP.
```

## Prompt 028 — Create the shared ProblemDetails/error contract

```text
REQUIREMENTS:
  TRACEABILITY: ERROR-001..005; API-004
  REQUIREMENT LINKS: [ERROR-001..005](../requirements/lightweight-crm-product-and-system-requirements.md#error-001); [API-004](../requirements/lightweight-crm-product-and-system-requirements.md#api-004)
  REQUIREMENT INTENT: Return safe errors that distinguish validation/auth/authz/not-found/concurrency/internal failures and provide a trace/support reference without exposing internals. Expose consistent REST-oriented, documented, versionable APIs using conventional HTTP verbs/status codes and bounded pagination for collections.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Create the shared public error/ProblemDetails extension contract only.

CONSTRAINT: Include safe error code, title/detail rules, trace/support reference and validation field errors where applicable.

RESTRICTION: Do not add host exception middleware, validators, or domain exceptions.

USAGE: Follow backend.md and API requirements.

BEHAVIOR: Add serialization/unit tests in Shared. Verify no stack trace/database/broker detail is exposed. STOP.
```

## Prompt 029 — Create the integration-event envelope contract

```text
REQUIREMENTS:
  TRACEABILITY: ASYNC-004; TRACE-003..007; OUTBOX-005; AUDIT-006..007
  REQUIREMENT LINKS: [ASYNC-004](../requirements/lightweight-crm-product-and-system-requirements.md#async-004); [TRACE-003..007](../requirements/lightweight-crm-product-and-system-requirements.md#trace-003); [OUTBOX-005](../requirements/lightweight-crm-product-and-system-requirements.md#outbox-005); [AUDIT-006..007](../requirements/lightweight-crm-product-and-system-requirements.md#audit-006)
  REQUIREMENT INTENT: Use Azure Service Bus and Azure Functions for durable async work with idempotent/duplicate-tolerant consumers, bounded retries, and dead-letter visibility. Propagate W3C distributed trace context across gateway, APIs, SQL, Service Bus, and Functions so an operation can be followed cradle to grave. Also satisfy the remaining linked cross-cutting constraints that apply to this atomic step.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Create the versioned integration-event envelope primitives in Contracts only.

CONSTRAINT: Carry event/message ID, type, version, occurred-at UTC, CorrelationId, CausationId and approved actor metadata. Keep payload contracts separate and minimal.

RESTRICTION: Do not add specific CRM events, serializer implementation, Service Bus SDK, or EF entities.

USAGE: Follow messaging.md and add-integration-event skill.

BEHAVIOR: Add contract/unit tests for required metadata and deterministic identity semantics. Build Contracts and STOP.
```

## Prompt 029A — Create the approved business-audit integration event contract

```text
REQUIREMENTS:
  TRACEABILITY: AUDIT-001..008; ASYNC-001..008; OUTBOX-001..006; PRIV-001..005
  REQUIREMENT LINKS: [AUDIT-001..008](../requirements/lightweight-crm-product-and-system-requirements.md#audit-001); [ASYNC-001..008](../requirements/lightweight-crm-product-and-system-requirements.md#async-001); [OUTBOX-001..006](../requirements/lightweight-crm-product-and-system-requirements.md#outbox-001); [PRIV-001..005](../requirements/lightweight-crm-product-and-system-requirements.md#priv-001)
  REQUIREMENT INTENT: Every Client/Project/Task mutation produces append-only audit evidence with entity/action/time/actor/source, trace/correlation, and applicable before/after values; secrets are redacted. Durable async work uses Azure Service Bus and Service Bus-triggered Functions with trace correlation, duplicate tolerance/idempotency, bounded retry behavior, and dead-letter visibility. Also satisfy the remaining linked cross-cutting constraints that apply to this atomic step.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Create only the cross-service audit event contract defined by the approved Audit ADR.

CONSTRAINT: The event must be a versioned past-tense fact and carry the minimum durable business-audit data required by AUDIT-002: owning service, entity type/ID, action, actor ID/type, occurred-at UTC, approved changed-field metadata and previous/new safe values when applicable, plus the standard envelope trace/correlation/causation identifiers.

RESTRICTION: Do not serialize EF entities, credentials, tokens, full customer payloads, repositories, Service Bus SDK types, or AuditDb storage models. Do not create per-entity event types unless the ADR explicitly chose that design.

USAGE: Follow messaging.md, audit.md and add-integration-event/add-audit-event skills.

BEHAVIOR: Add contract tests for required metadata, versioning and serialization/redaction boundaries; build Contracts and STOP.
```

## Prompt 030 — Create shared OutboxMessage persistence model/configuration

```text
REQUIREMENTS:
  TRACEABILITY: OUTBOX-001..006; DATA-006
  REQUIREMENT LINKS: [OUTBOX-001..006](../requirements/lightweight-crm-product-and-system-requirements.md#outbox-001); [DATA-006](../requirements/lightweight-crm-product-and-system-requirements.md#data-006)
  REQUIREMENT INTENT: When a transaction changes state and publishes an event, state and outbox commit together; a timer Function relays pending messages idempotently and exposes backlog/failure metrics. Use SQL Server/Azure SQL with service-owned databases, enforced relationships, UTC timestamps, safe public IDs, optimistic concurrency, and archival over routine hard deletion.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Create Shared's SQL Server-compatible OutboxMessage persistence model and EF configuration only.

CONSTRAINT: Include stable message/event ID, contract type/version, payload representation, correlation/causation, occurred-at UTC, created-at UTC, dispatch status/attempt metadata and concurrency/lease fields required by the approved relay design.

RESTRICTION: Do not create a DbContext, relay, Service Bus publisher, migration, or CRM entity.

USAGE: Follow database.md and messaging.md.

BEHAVIOR: Add EF model metadata/unit tests where practical and build Shared. Verify no PostgreSQL-specific type/annotation exists. STOP.
```

## Prompt 031 — Create shared InboxMessage persistence model/configuration

```text
REQUIREMENTS:
  TRACEABILITY: ASYNC-005..008; AUDIT-004; DATA-006
  REQUIREMENT LINKS: [ASYNC-005..008](../requirements/lightweight-crm-product-and-system-requirements.md#async-005); [AUDIT-004](../requirements/lightweight-crm-product-and-system-requirements.md#audit-004); [DATA-006](../requirements/lightweight-crm-product-and-system-requirements.md#data-006)
  REQUIREMENT INTENT: Use Azure Service Bus and Azure Functions for durable async work with idempotent/duplicate-tolerant consumers, bounded retries, and dead-letter visibility. Every business mutation must create append-only audit evidence describing what changed, when, who caused it, and applicable before/after values while preserving trace correlation and redacting secrets. Use SQL Server/Azure SQL with service-owned databases, enforced relationships, UTC timestamps, safe public IDs, optimistic concurrency, and archival over routine hard deletion.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Create Shared's SQL Server-compatible InboxMessage persistence model and EF configuration only.

CONSTRAINT: Support message/event ID uniqueness, received/started/completed UTC state, failure/recovery metadata and service-owned persistence.

RESTRICTION: Do not create consumer logic, a central inbox database, or migrations.

USAGE: Follow database.md and messaging.md.

BEHAVIOR: Add model/config tests and build Shared. Verify uniqueness/idempotency key is explicit. STOP.
```

## Prompt 032 — Create the shared event serializer

```text
REQUIREMENTS:
  TRACEABILITY: ASYNC-004; OUTBOX-005; TRACE-003..007
  REQUIREMENT LINKS: [ASYNC-004](../requirements/lightweight-crm-product-and-system-requirements.md#async-004); [OUTBOX-005](../requirements/lightweight-crm-product-and-system-requirements.md#outbox-005); [TRACE-003..007](../requirements/lightweight-crm-product-and-system-requirements.md#trace-003)
  REQUIREMENT INTENT: Use Azure Service Bus and Azure Functions for durable async work with idempotent/duplicate-tolerant consumers, bounded retries, and dead-letter visibility. Commit state and integration events atomically through a transactional outbox, then relay them with a timer-triggered Function and observable retry/backlog behavior. Propagate W3C distributed trace context across gateway, APIs, SQL, Service Bus, and Functions so an operation can be followed cradle to grave.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Create the Shared integration-event envelope serializer/deserializer only.

CONSTRAINT: Serialize versioned Contracts payloads deterministically using the repository-approved JSON settings. Reject unsupported/unknown contract versions through a typed result/exception policy.

RESTRICTION: Do not send to Service Bus, read a database, or add business event types.

USAGE: Follow messaging.md.

BEHAVIOR: Add round-trip, unknown-version and malformed-payload unit tests. Run Shared tests and STOP.
```

## Prompt 033 — Create the shared Service Bus publisher abstraction

```text
REQUIREMENTS:
  TRACEABILITY: ASYNC-001..008; OUTBOX-003..006
  REQUIREMENT LINKS: [ASYNC-001..008](../requirements/lightweight-crm-product-and-system-requirements.md#async-001); [OUTBOX-003..006](../requirements/lightweight-crm-product-and-system-requirements.md#outbox-003)
  REQUIREMENT INTENT: Durable async work uses Azure Service Bus and Service Bus-triggered Functions with trace correlation, duplicate tolerance/idempotency, bounded retry behavior, and dead-letter visibility. Commit state and integration events atomically through a transactional outbox, then relay them with a timer-triggered Function and observable retry/backlog behavior.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Create the Shared Service Bus publisher abstraction and Azure Service Bus SDK implementation only.

CONSTRAINT: Publisher accepts an already-serialized envelope and configured entity destination; set MessageId and trace/correlation metadata according to ADR/rules; use injected Aspire/Azure client.

RESTRICTION: Do not query outbox rows, mark dispatch state, create background workers, or hardcode entity names/credentials.

USAGE: Follow messaging.md and functions.md.

BEHAVIOR: Add unit tests around message metadata using an abstraction/fake boundary. Build Shared and STOP.
```

## Prompt 034 — Create the reusable outbox relay mechanism

```text
REQUIREMENTS:
  TRACEABILITY: OUTBOX-003..006; ASYNC-005..008; OBS-005
  REQUIREMENT LINKS: [OUTBOX-003..006](../requirements/lightweight-crm-product-and-system-requirements.md#outbox-003); [ASYNC-005..008](../requirements/lightweight-crm-product-and-system-requirements.md#async-005); [OBS-005](../requirements/lightweight-crm-product-and-system-requirements.md#obs-005)
  REQUIREMENT INTENT: Commit state and integration events atomically through a transactional outbox, then relay them with a timer-triggered Function and observable retry/backlog behavior. Use Azure Service Bus and Azure Functions for durable async work with idempotent/duplicate-tolerant consumers, bounded retries, and dead-letter visibility. Centralize operational visibility in Azure Monitor/Application Insights for requests, dependencies, Functions, Service Bus, SQL, failures, and trace-based investigation.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Create Shared's reusable IOutboxRelay and relay implementation only.

CONSTRAINT: Relay must select a bounded batch from the owning service store, use the approved lease/concurrency strategy, publish through the shared publisher, mark dispatched only after confirmed publish, leave failures retryable, honor cancellation and emit structured metrics/logs.

RESTRICTION: Do not add a TimerTrigger, BackgroundService, service-specific DbContext, or business event switch.

USAGE: Follow add-function-trigger and messaging/functions rules.

BEHAVIOR: Add focused tests: empty batch, successful send marks dispatched, failed send remains pending, partial batch, cancellation, and lease/concurrency behavior. STOP.
```

## Prompt 035 — Add local SQL Server resource to AppHost

```text
REQUIREMENTS:
  TRACEABILITY: DATA-030..034; DEPLOY-001
  REQUIREMENT LINKS: [DATA-030..034](../requirements/lightweight-crm-product-and-system-requirements.md#data-030); [DEPLOY-001](../requirements/lightweight-crm-product-and-system-requirements.md#deploy-001)
  REQUIREMENT INTENT: Use Microsoft SQL Server/Azure SQL, one database per bounded service, no cross-service database queries, and controlled schema migrations. Support environment-specific configuration, externalized secrets, Flex Consumption Functions, and consistent deployment/telemetry metadata.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Add one local Aspire SQL Server resource only.

CONSTRAINT: Use current Aspire SQL Server hosting integration; credentials/config are Aspire-managed and not hardcoded in source.

RESTRICTION: Do not add service databases or wire any project yet.

USAGE: Use add-aspire-resource skill and verify current Aspire APIs.

BEHAVIOR: Build AppHost and inspect the application model/resource list to prove the SQL Server resource exists. STOP.
```

## Prompt 036 — Add local Azure Service Bus resource to AppHost

```text
REQUIREMENTS:
  TRACEABILITY: ASYNC-001..008; DEPLOY-001
  REQUIREMENT LINKS: [ASYNC-001..008](../requirements/lightweight-crm-product-and-system-requirements.md#async-001); [DEPLOY-001](../requirements/lightweight-crm-product-and-system-requirements.md#deploy-001)
  REQUIREMENT INTENT: Durable async work uses Azure Service Bus and Service Bus-triggered Functions with trace correlation, duplicate tolerance/idempotency, bounded retry behavior, and dead-letter visibility. Support environment-specific configuration, externalized secrets, Flex Consumption Functions, and consistent deployment/telemetry metadata.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Add the local Azure Service Bus emulator/resource to AppHost only according to the approved topology ADR.

CONSTRAINT: Entity topology must come from ADR/config and use current Aspire/Azure Service Bus emulator support.

RESTRICTION: Do not wire publishers/consumers or add Functions projects yet.

USAGE: Use add-aspire-resource skill and current official Aspire/Service Bus emulator guidance.

BEHAVIOR: Build AppHost and inspect the resource model. Verify configured entities match the ADR. STOP.
```

## Prompt 037 — Register Gateway in AppHost

```text
REQUIREMENTS:
  TRACEABILITY: SEC-020..025; TRACE-001..007
  REQUIREMENT LINKS: [SEC-020..025](../requirements/lightweight-crm-product-and-system-requirements.md#sec-020); [TRACE-001..007](../requirements/lightweight-crm-product-and-system-requirements.md#trace-001)
  REQUIREMENT INTENT: Public API access goes through the Project Chicago gateway over HTTPS with validated inputs and safe logging that excludes credentials, tokens, secrets, and unnecessary PII. Every inbound request participates in a trace propagated through gateway, services, SQL, HTTP, Service Bus, Functions, and downstream work with safe diagnostic metadata.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Register ProjectChicago.Gateway as an AppHost project resource only.

CONSTRAINT: Use Aspire service discovery/configuration and default health/telemetry conventions.

RESTRICTION: Do not add routes to nonexistent services or wire SQL/Service Bus.

USAGE: Follow aspire.md and gateway.md.

BEHAVIOR: Build AppHost and verify Gateway appears as a project resource. STOP.
```

## Prompt 038 — Register the React app in AppHost

```text
REQUIREMENTS:
  TRACEABILITY: UX-001..006; DEPLOY-001
  REQUIREMENT LINKS: [UX-001..006](../requirements/lightweight-crm-product-and-system-requirements.md#ux-001); [DEPLOY-001](../requirements/lightweight-crm-product-and-system-requirements.md#deploy-001)
  REQUIREMENT INTENT: The UI prioritizes simple workflows with clear validation/save/failure/loading/empty/unauthorized states, explicit destructive intent, and responsive desktop/tablet behavior. Support environment-specific configuration, externalized secrets, Flex Consumption Functions, and consistent deployment/telemetry metadata.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Register the existing React/Vite application as the AppHost web resource only.

CONSTRAINT: Use the current Aspire JavaScript/Vite integration and the repository's start script.

RESTRICTION: Do not add API URLs, feature routes, auth, or direct service references.

USAGE: Follow aspire.md/frontend.md and verify current Aspire JS APIs.

BEHAVIOR: Run the smallest AppHost model/build verification and web build. STOP.
```

---

# Part 2 — CRM bounded service scaffold and persistence foundation

## Prompt 039 — Create the Crm HTTP host

```text
REQUIREMENTS:
  TRACEABILITY: CLIENT-001..032; PROJECT-001..031; TASK-001..022
  REQUIREMENT LINKS: [CLIENT-001..032](../requirements/lightweight-crm-product-and-system-requirements.md#client-001); [PROJECT-001..031](../requirements/lightweight-crm-product-and-system-requirements.md#project-001); [TASK-001..022](../requirements/lightweight-crm-product-and-system-requirements.md#task-001)
  REQUIREMENT INTENT: Clients are the CRM anchor and must support the required data, lifecycle/archive behavior, searchable paginated lists, detail views, ownership, and auditable changes. Projects belong to one Client and must support the required metadata, statuses, filtering/search/detail behavior, completion rules, and non-destructive archival. Tasks belong to one Project and must support assignment, priority, status/completion/reopen behavior, overdue detection, and filterable task views.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Create `ProjectChicago.Crm` ASP.NET Core Web API host only, assuming the approved bounded-context ADR uses `Crm`.

CONSTRAINT: Target .NET 10; host contains transport/composition only.

RESTRICTION: Do not add controllers, EF, domain models, authentication, routes, or Service Bus.

USAGE: Follow backend.md and dotnet CLI conventions.

BEHAVIOR: Add to solution and build. STOP.
```

## Prompt 040 — Create the Crm.Core project

```text
REQUIREMENTS:
  TRACEABILITY: DATA-001..008; CLIENT/PROJECT/TASK requirements
  REQUIREMENT LINKS: [DATA-001..008](../requirements/lightweight-crm-product-and-system-requirements.md#data-001); [Client requirements](../requirements/lightweight-crm-product-and-system-requirements.md#4-client-requirements), [Project requirements](../requirements/lightweight-crm-product-and-system-requirements.md#8-project-requirements), [Task requirements](../requirements/lightweight-crm-product-and-system-requirements.md#12-task-requirements)
  REQUIREMENT INTENT: Enforce Client→Project→Task relationships, validate before mutation, store UTC, use safe public IDs, and prevent silent concurrent overwrites.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Create `ProjectChicago.Crm.Core` class library only.

CONSTRAINT: It will own Facade -> Business -> Data -> Repository -> DbContext for Clients/Projects/Tasks.

RESTRICTION: Do not add folders, packages, entities, or references yet.

USAGE: Follow backend.md.

BEHAVIOR: Add to solution and build. STOP.
```

## Prompt 041 — Create the Crm.Functions project

```text
REQUIREMENTS:
  TRACEABILITY: ASYNC-001..008; OUTBOX-003..006
  REQUIREMENT LINKS: [ASYNC-001..008](../requirements/lightweight-crm-product-and-system-requirements.md#async-001); [OUTBOX-003..006](../requirements/lightweight-crm-product-and-system-requirements.md#outbox-003)
  REQUIREMENT INTENT: Durable async work uses Azure Service Bus and Service Bus-triggered Functions with trace correlation, duplicate tolerance/idempotency, bounded retry behavior, and dead-letter visibility. Commit state and integration events atomically through a transactional outbox, then relay them with a timer-triggered Function and observable retry/backlog behavior.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Create `ProjectChicago.Crm.Functions` as a .NET isolated Azure Functions project only.

CONSTRAINT: Target the supported Functions 4.x/.NET 10 isolated worker baseline and Flex Consumption-compatible architecture.

RESTRICTION: Do not add triggers, Service Bus bindings, relay code, HTTP triggers, or business logic.

USAGE: Follow functions.md and verify current Functions template/packages.

BEHAVIOR: Add to solution and build. STOP.
```

## Prompt 042 — Wire Crm project references

```text
REQUIREMENTS:
  TRACEABILITY: DATA-031..034; architecture constraints
  REQUIREMENT LINKS: [DATA-031..034](../requirements/lightweight-crm-product-and-system-requirements.md#data-031); [Project Chicago requirements](../requirements/lightweight-crm-product-and-system-requirements.md) and [CLAUDE.md](../../CLAUDE.md)
  REQUIREMENT INTENT: Use SQL Server/Azure SQL with service-owned databases, enforced relationships, UTC timestamps, safe public IDs, optimistic concurrency, and archival over routine hard deletion. Preserve the service, layer, persistence, gateway, and Function boundaries defined by the requirements, ADRs, and CLAUDE.md.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Add only the approved project references for Crm: Crm.Core -> Shared/Contracts as needed; Crm host -> Crm.Core + ServiceDefaults; Crm.Functions -> Crm.Core + Shared/Contracts + ServiceDefaults as appropriate.

CONSTRAINT: Preserve acyclic reference direction.

RESTRICTION: Do not add package dependencies or source code.

USAGE: Use dotnet CLI and CLAUDE.md reference rules.

BEHAVIOR: Build all three projects and print the reference graph. Verify no Crm project references Identity.Core or Audit.Core. STOP.
```

## Prompt 043 — Create Crm.Core unit-test project

```text
REQUIREMENTS:
  TRACEABILITY: TEST-001..007
  REQUIREMENT LINKS: [TEST-001..007](../requirements/lightweight-crm-product-and-system-requirements.md#test-001)
  REQUIREMENT INTENT: Automated tests cover business rules, authorization, APIs, SQL-compatible persistence, message consumers, audit generation, and representative distributed tracing.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Create `ProjectChicago.Crm.Core.Tests` only and reference Crm.Core.

CONSTRAINT: Use the repository's approved test framework.

RESTRICTION: Do not add tests or SQL containers yet.

USAGE: Use dotnet CLI.

BEHAVIOR: Add to solution and build the test project. STOP.
```

## Prompt 044 — Create Crm API test project

```text
REQUIREMENTS:
  TRACEABILITY: TEST-003; SEC-010..013
  REQUIREMENT LINKS: [TEST-003](../requirements/lightweight-crm-product-and-system-requirements.md#test-003); [SEC-010..013](../requirements/lightweight-crm-product-and-system-requirements.md#sec-010)
  REQUIREMENT INTENT: Automate business, authorization, API, SQL, messaging, audit, tracing, Function, and UI behavior at the boundary that can actually prove it. Use ASP.NET Core Identity and server-side role/policy authorization with least privilege; protect APIs and never expose passwords, tokens, secrets, or sensitive internals.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Create `ProjectChicago.Crm.Api.Tests` only and reference the Crm host.

CONSTRAINT: Use the established ASP.NET Core integration-test framework pattern.

RESTRICTION: Do not add WebApplicationFactory setup or test cases yet.

USAGE: Use dotnet CLI.

BEHAVIOR: Add to solution and build. STOP.
```

## Prompt 045 — Create Crm Functions test project

```text
REQUIREMENTS:
  TRACEABILITY: TEST-005; TEST-007
  REQUIREMENT LINKS: [TEST-005](../requirements/lightweight-crm-product-and-system-requirements.md#test-005); [TEST-007](../requirements/lightweight-crm-product-and-system-requirements.md#test-007)
  REQUIREMENT INTENT: Automate business, authorization, API, SQL, messaging, audit, tracing, Function, and UI behavior at the boundary that can actually prove it.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Create `ProjectChicago.Crm.Functions.Tests` only and reference Crm.Functions.

CONSTRAINT: Use the established test framework.

RESTRICTION: Do not add Function fixtures or tests yet.

USAGE: Use dotnet CLI.

BEHAVIOR: Add to solution and build. STOP.
```

## Prompt 046 — Add EF Core SQL Server packages to Crm.Core

```text
REQUIREMENTS:
  TRACEABILITY: DATA-030..034; TEST-004
  REQUIREMENT LINKS: [DATA-030..034](../requirements/lightweight-crm-product-and-system-requirements.md#data-030); [TEST-004](../requirements/lightweight-crm-product-and-system-requirements.md#test-004)
  REQUIREMENT INTENT: Use Microsoft SQL Server/Azure SQL, one database per bounded service, no cross-service database queries, and controlled schema migrations. Automate business, authorization, API, SQL, messaging, audit, tracing, Function, and UI behavior at the boundary that can actually prove it.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Add only the EF Core SQL Server and required design/migration packages to Crm.Core using central package management.

CONSTRAINT: Use versions compatible with .NET 10 and the approved Aspire stack.

RESTRICTION: Do not create DbContext, entities, migrations, or connection strings.

USAGE: Follow database.md and verify current Microsoft package versions.

BEHAVIOR: Restore and build Crm.Core. Verify no Npgsql/PostgreSQL package appears in the dependency graph. STOP.
```

## Prompt 047 — Create CrmDbContext with shared outbox/inbox sets

```text
REQUIREMENTS:
  TRACEABILITY: DATA-001..008; OUTBOX-001..006; ASYNC-005
  REQUIREMENT LINKS: [DATA-001..008](../requirements/lightweight-crm-product-and-system-requirements.md#data-001); [OUTBOX-001..006](../requirements/lightweight-crm-product-and-system-requirements.md#outbox-001); [ASYNC-005](../requirements/lightweight-crm-product-and-system-requirements.md#async-005)
  REQUIREMENT INTENT: Enforce Client→Project→Task relationships, validate before mutation, store UTC, use safe public IDs, and prevent silent concurrent overwrites. When a transaction changes state and publishes an event, state and outbox commit together; a timer Function relays pending messages idempotently and exposes backlog/failure metrics. Use Azure Service Bus and Azure Functions for durable async work with idempotent/duplicate-tolerant consumers, bounded retries, and dead-letter visibility.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Create the empty service-owned `CrmDbContext` and register Shared OutboxMessage/InboxMessage sets/configuration only.

CONSTRAINT: DbContext belongs to Crm.Core and uses SQL Server via Aspire-injected configuration. No CRM domain DbSets yet.

RESTRICTION: Do not add Client/Project/Task entities or migrations.

USAGE: Follow database.md/backend.md.

BEHAVIOR: Add focused model tests proving only Outbox/Inbox tables are currently mapped. Build Crm.Core and STOP.
```

## Prompt 048 — Register the Crm database resource in AppHost

```text
REQUIREMENTS:
  TRACEABILITY: DATA-031..034; DEPLOY-001
  REQUIREMENT LINKS: [DATA-031..034](../requirements/lightweight-crm-product-and-system-requirements.md#data-031); [DEPLOY-001](../requirements/lightweight-crm-product-and-system-requirements.md#deploy-001)
  REQUIREMENT INTENT: Use SQL Server/Azure SQL with service-owned databases, enforced relationships, UTC timestamps, safe public IDs, optimistic concurrency, and archival over routine hard deletion. Support environment-specific configuration, externalized secrets, Flex Consumption Functions, and consistent deployment/telemetry metadata.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Add one `crmdb` (or ADR-approved equivalent) database under the existing Aspire SQL Server resource only.

CONSTRAINT: The resource name must match the approved service catalog and injected connection convention.

RESTRICTION: Do not wire projects yet or create schema.

USAGE: Use add-aspire-resource skill.

BEHAVIOR: Build AppHost and inspect the resource model. STOP.
```

## Prompt 049 — Wire Crm host to ServiceDefaults and CrmDb

```text
REQUIREMENTS:
  TRACEABILITY: OTEL-001..006; DATA-030..034; OPS-001
  REQUIREMENT LINKS: [OTEL-001..006](../requirements/lightweight-crm-product-and-system-requirements.md#otel-001); [DATA-030..034](../requirements/lightweight-crm-product-and-system-requirements.md#data-030); [OPS-001](../requirements/lightweight-crm-product-and-system-requirements.md#ops-001)
  REQUIREMENT INTENT: Every API/service/Function uses OpenTelemetry for traces, metrics, and log correlation, including dependency instrumentation and meaningful business spans where needed. Use Microsoft SQL Server/Azure SQL, one database per bounded service, no cross-service database queries, and controlled schema migrations. Expose health and telemetry that detect errors, latency, dependency failures, authentication anomalies, dead letters, and outbox backlog.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Configure the Crm HTTP host composition root to use ServiceDefaults and the Aspire SQL Server EF Core integration for CrmDb only.

CONSTRAINT: Keep Program.cs composition-only and configuration-driven.

RESTRICTION: Do not add controllers, auth, migrations-at-startup, Service Bus clients, or business logic.

USAGE: Follow backend.md/aspire.md/database.md.

BEHAVIOR: Build Crm host and add a minimal host-start integration test if needed to prove DI resolves CrmDbContext. STOP.
```

## Prompt 050 — Register Crm host in AppHost

```text
REQUIREMENTS:
  TRACEABILITY: DEPLOY-001; OPS-001..004
  REQUIREMENT LINKS: [DEPLOY-001](../requirements/lightweight-crm-product-and-system-requirements.md#deploy-001); [OPS-001..004](../requirements/lightweight-crm-product-and-system-requirements.md#ops-001)
  REQUIREMENT INTENT: Support environment-specific configuration, externalized secrets, Flex Consumption Functions, and consistent deployment/telemetry metadata. Operators can determine service health and detect rising errors/latency, SQL or Service Bus failures, Function failures, auth anomalies, dead letters, and outbox backlog.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Register the Crm HTTP host in AppHost and reference/wait for CrmDb only.

CONSTRAINT: Use Aspire resource discovery and health ordering.

RESTRICTION: Do not give the API Service Bus credentials and do not add gateway routes yet.

USAGE: Follow add-aspire-resource skill.

BEHAVIOR: Build AppHost and verify Crm -> CrmDb dependency only. STOP.
```

## Prompt 051 — Wire Crm.Functions to CrmDb and Service Bus

```text
REQUIREMENTS:
  TRACEABILITY: ASYNC-001..008; OUTBOX-003..006; OTEL-001..006
  REQUIREMENT LINKS: [ASYNC-001..008](../requirements/lightweight-crm-product-and-system-requirements.md#async-001); [OUTBOX-003..006](../requirements/lightweight-crm-product-and-system-requirements.md#outbox-003); [OTEL-001..006](../requirements/lightweight-crm-product-and-system-requirements.md#otel-001)
  REQUIREMENT INTENT: Durable async work uses Azure Service Bus and Service Bus-triggered Functions with trace correlation, duplicate tolerance/idempotency, bounded retry behavior, and dead-letter visibility. Commit state and integration events atomically through a transactional outbox, then relay them with a timer-triggered Function and observable retry/backlog behavior. Every API/service/Function uses OpenTelemetry for traces, metrics, and log correlation, including dependency instrumentation and meaningful business spans where needed.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Configure Crm.Functions composition and AppHost wiring for ServiceDefaults/OpenTelemetry, CrmDb and the approved Service Bus resource only.

CONSTRAINT: Use the narrowest references needed by Functions; isolated worker; Flex Consumption-compatible.

RESTRICTION: Do not add triggers or business logic. Do not give the Crm API Service Bus credentials.

USAGE: Follow functions.md/aspire.md/messaging.md.

BEHAVIOR: Build Crm.Functions and AppHost; inspect resource references to prove least-privilege wiring. STOP.
```

## Prompt 052 — Add the Crm outbox timer trigger

```text
REQUIREMENTS:
  TRACEABILITY: OUTBOX-003..006; ASYNC-001..008
  REQUIREMENT LINKS: [OUTBOX-003..006](../requirements/lightweight-crm-product-and-system-requirements.md#outbox-003); [ASYNC-001..008](../requirements/lightweight-crm-product-and-system-requirements.md#async-001)
  REQUIREMENT INTENT: Commit state and integration events atomically through a transactional outbox, then relay them with a timer-triggered Function and observable retry/backlog behavior. Durable async work uses Azure Service Bus and Service Bus-triggered Functions with trace correlation, duplicate tolerance/idempotency, bounded retry behavior, and dead-letter visibility.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Add one timer-triggered Function to Crm.Functions that delegates to the shared IOutboxRelay.

CONSTRAINT: Schedule and batch settings come from configuration; trigger contains no polling SQL or event logic; cancellation is honored.

RESTRICTION: Do not add Service Bus consumer triggers, BackgroundService, or business logic.

USAGE: Use add-function-trigger skill.

BEHAVIOR: Add Function adapter tests proving exactly one relay call, cancellation propagation, and exception propagation. Run focused tests and STOP.
```

## Prompt 053 — Add stable Crm gateway route prefix

```text
REQUIREMENTS:
  TRACEABILITY: API-001..007; SEC-020..025
  REQUIREMENT LINKS: [API-001..007](../requirements/lightweight-crm-product-and-system-requirements.md#api-001); [SEC-020..025](../requirements/lightweight-crm-product-and-system-requirements.md#sec-020)
  REQUIREMENT INTENT: Use consistent REST routes, conventional HTTP verbs/status codes, bounded pagination, OpenAPI documentation, and versionable public contracts. Public API access goes through the Project Chicago gateway over HTTPS with validated inputs and safe logging that excludes credentials, tokens, secrets, and unnecessary PII.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Add one stable YARP route/cluster mapping for the approved public CRM API prefix to the Crm host.

CONSTRAINT: Use Aspire service discovery; preserve correlation/auth middleware; public route must not expose internal service naming.

RESTRICTION: Do not add controller actions or Identity routes.

USAGE: Follow gateway.md.

BEHAVIOR: Add gateway routing test and build Gateway. Verify no hardcoded host/port. STOP.
```

## Prompt 053A — Add Crm global exception handling and request context registration

```text
REQUIREMENTS:
  TRACEABILITY: ERROR-001..005; TRACE-001..007; LOG-001..006
  REQUIREMENT LINKS: [ERROR-001..005](../requirements/lightweight-crm-product-and-system-requirements.md#error-001); [TRACE-001..007](../requirements/lightweight-crm-product-and-system-requirements.md#trace-001); [LOG-001..006](../requirements/lightweight-crm-product-and-system-requirements.md#log-001)
  REQUIREMENT INTENT: Return safe errors that distinguish validation/auth/authz/not-found/concurrency/internal failures and provide a trace/support reference without exposing internals. Every inbound request participates in a trace propagated through gateway, services, SQL, HTTP, Service Bus, Functions, and downstream work with safe diagnostic metadata. Use structured trace-correlated logs without sensitive payload leakage or duplicate exception logging at every layer.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Register the shared ProblemDetails/exception handling and HTTP request/actor context adapter in the Crm HTTP host only.

CONSTRAINT: Expected validation/not-found/conflict/forbidden failures map to stable safe ProblemDetails; unexpected failures include a safe trace/support reference and remain visible in structured telemetry. Request context must come from trusted ASP.NET Core authentication/Activity state.

RESTRICTION: Do not add business exceptions, controller actions, authentication scheme configuration, or new logging framework.

USAGE: Follow backend.md and approved observability/auth ADRs.

BEHAVIOR: Add host integration tests for expected error mappings, unexpected 500 redaction and trace reference propagation; run focused tests and STOP.
```

## Prompt 054 — Create the Client entity

```text
REQUIREMENTS:
  TRACEABILITY: CLIENT-001..015; DATA-006..008
  REQUIREMENT LINKS: [CLIENT-001..015](../requirements/lightweight-crm-product-and-system-requirements.md#client-001); [DATA-006..008](../requirements/lightweight-crm-product-and-system-requirements.md#data-006)
  REQUIREMENT INTENT: Clients are the CRM anchor and must support the required data, lifecycle/archive behavior, searchable paginated lists, detail views, ownership, and auditable changes. Use SQL Server/Azure SQL with service-owned databases, enforced relationships, UTC timestamps, safe public IDs, optimistic concurrency, and archival over routine hard deletion.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Create the Crm Client data/domain entity and its invariant unit tests only.

CONSTRAINT: Include stable non-enumerable external ID strategy, name, primary contact fields, address fields, lifecycle status, description/notes, owner user reference, created/modified UTC metadata, archive state and an optimistic concurrency token where approved. Use the lifecycle statuses from requirements.

RESTRICTION: Do not add EF configuration, DbSet, repository, migration, DTO, event, or endpoint.

USAGE: Follow backend.md/database.md and CLIENT requirements.

BEHAVIOR: Run focused entity tests for required invariants/status values/UTC semantics and STOP.
```

## Prompt 055 — Configure Client EF persistence

```text
REQUIREMENTS:
  TRACEABILITY: CLIENT-002..004; DATA-004..008
  REQUIREMENT LINKS: [CLIENT-002..004](../requirements/lightweight-crm-product-and-system-requirements.md#client-002); [DATA-004..008](../requirements/lightweight-crm-product-and-system-requirements.md#data-004)
  REQUIREMENT INTENT: Clients are the CRM anchor and must support the required data, lifecycle/archive behavior, searchable paginated lists, detail views, ownership, and auditable changes. Use SQL Server/Azure SQL with service-owned databases, enforced relationships, UTC timestamps, safe public IDs, optimistic concurrency, and archival over routine hard deletion.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Add EF Core SQL Server configuration for Client only.

CONSTRAINT: Bound strings intentionally; index concrete list/search fields; configure concurrency and archive filtering according to approved data policy; avoid destructive cascades.

RESTRICTION: Do not add DbSet, repository methods, migration, or other entities.

USAGE: Follow database.md.

BEHAVIOR: Add model metadata tests and build Crm.Core. STOP.
```

## Prompt 056 — Add Client DbSet to CrmDbContext

```text
REQUIREMENTS:
  TRACEABILITY: CLIENT-001..015; DATA-004
  REQUIREMENT LINKS: [CLIENT-001..015](../requirements/lightweight-crm-product-and-system-requirements.md#client-001); [DATA-004](../requirements/lightweight-crm-product-and-system-requirements.md#data-004)
  REQUIREMENT INTENT: Clients are the CRM anchor and must support the required data, lifecycle/archive behavior, searchable paginated lists, detail views, ownership, and auditable changes. Use SQL Server/Azure SQL with service-owned databases, enforced relationships, UTC timestamps, safe public IDs, optimistic concurrency, and archival over routine hard deletion.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Add the Client DbSet and apply its EF configuration to CrmDbContext only.

CONSTRAINT: Do not add Project/Task DbSets or migration.

RESTRICTION: Preserve existing Outbox/Inbox mappings.

USAGE: Follow database.md.

BEHAVIOR: Run the CrmDbContext model test proving Client + Outbox + Inbox mappings. STOP.
```

## Prompt 057 — Create the Project entity

```text
REQUIREMENTS:
  TRACEABILITY: PROJECT-001..014; DATA-001..008
  REQUIREMENT LINKS: [PROJECT-001..014](../requirements/lightweight-crm-product-and-system-requirements.md#project-001); [DATA-001..008](../requirements/lightweight-crm-product-and-system-requirements.md#data-001)
  REQUIREMENT INTENT: Projects belong to one Client and must support the required metadata, statuses, filtering/search/detail behavior, completion rules, and non-destructive archival. Enforce Client→Project→Task relationships, validate before mutation, store UTC, use safe public IDs, and prevent silent concurrent overwrites.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Create the Crm Project entity and invariant tests only.

CONSTRAINT: Include ClientId, name, description, status, priority, owner, start/target/actual completion dates, notes, created/modified metadata, archive state and approved concurrency token. Use required statuses.

RESTRICTION: Do not add EF configuration, DbSet, repository, migration, Task, or endpoints.

USAGE: Follow backend.md/database.md.

BEHAVIOR: Run focused Project invariant/status/date tests and STOP.
```

## Prompt 058 — Configure Project EF persistence

```text
REQUIREMENTS:
  TRACEABILITY: PROJECT-001..023; DATA-002..005
  REQUIREMENT LINKS: [PROJECT-001..023](../requirements/lightweight-crm-product-and-system-requirements.md#project-001); [DATA-002..005](../requirements/lightweight-crm-product-and-system-requirements.md#data-002)
  REQUIREMENT INTENT: Projects belong to one Client and must support the required metadata, statuses, filtering/search/detail behavior, completion rules, and non-destructive archival. Use SQL Server/Azure SQL with service-owned databases, enforced relationships, UTC timestamps, safe public IDs, optimistic concurrency, and archival over routine hard deletion.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Add EF Core configuration for Project and its required Client foreign key only.

CONSTRAINT: Enforce Project -> Client ownership, indexes for Client/status/owner/priority/dates, bounded strings and non-destructive delete behavior.

RESTRICTION: Do not add DbSet, migration, Task relationship, or queries.

USAGE: Follow database.md.

BEHAVIOR: Add model metadata tests proving required FK/index/delete behavior. STOP.
```

## Prompt 059 — Add Project DbSet to CrmDbContext

```text
REQUIREMENTS:
  TRACEABILITY: PROJECT-001..023; DATA-002
  REQUIREMENT LINKS: [PROJECT-001..023](../requirements/lightweight-crm-product-and-system-requirements.md#project-001); [DATA-002](../requirements/lightweight-crm-product-and-system-requirements.md#data-002)
  REQUIREMENT INTENT: Projects belong to one Client and must support the required metadata, statuses, filtering/search/detail behavior, completion rules, and non-destructive archival. Use SQL Server/Azure SQL with service-owned databases, enforced relationships, UTC timestamps, safe public IDs, optimistic concurrency, and archival over routine hard deletion.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Add the Project DbSet/configuration to CrmDbContext only.

CONSTRAINT: Preserve Client/Outbox/Inbox mappings.

RESTRICTION: Do not add Task or migration.

USAGE: Follow database.md.

BEHAVIOR: Run model tests verifying Project cannot exist without Client at the EF model level. STOP.
```

## Prompt 060 — Create the Task entity

```text
REQUIREMENTS:
  TRACEABILITY: TASK-001..016; DATA-003..008
  REQUIREMENT LINKS: [TASK-001..016](../requirements/lightweight-crm-product-and-system-requirements.md#task-001); [DATA-003..008](../requirements/lightweight-crm-product-and-system-requirements.md#data-003)
  REQUIREMENT INTENT: Authorized users can create Tasks for one Project with the required metadata, statuses, priorities, assignment, completion, reopen, and overdue behavior. Use SQL Server/Azure SQL with service-owned databases, enforced relationships, UTC timestamps, safe public IDs, optimistic concurrency, and archival over routine hard deletion.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Create the Crm Task entity and invariant tests only.

CONSTRAINT: Include ProjectId, title, description, status, priority, assigned user, creator, start/due/completed timestamps, notes, created/modified metadata and approved concurrency token. Use required statuses/priorities.

RESTRICTION: Do not add EF configuration, DbSet, repository, migration, or endpoint.

USAGE: Follow backend.md/database.md.

BEHAVIOR: Run focused Task invariant/status/priority/completion tests and STOP.
```

## Prompt 061 — Configure Task EF persistence

```text
REQUIREMENTS:
  TRACEABILITY: TASK-001..022; DATA-003..005
  REQUIREMENT LINKS: [TASK-001..022](../requirements/lightweight-crm-product-and-system-requirements.md#task-001); [DATA-003..005](../requirements/lightweight-crm-product-and-system-requirements.md#data-003)
  REQUIREMENT INTENT: Tasks belong to one Project and must support assignment, priority, status/completion/reopen behavior, overdue detection, and filterable task views. Use SQL Server/Azure SQL with service-owned databases, enforced relationships, UTC timestamps, safe public IDs, optimistic concurrency, and archival over routine hard deletion.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Add EF Core configuration for Task and its required Project foreign key only.

CONSTRAINT: Index Project/status/priority/assignee/due date and protect historical data from cascading physical deletion.

RESTRICTION: Do not add DbSet, migration, queries, or API code.

USAGE: Follow database.md.

BEHAVIOR: Add model metadata tests proving required FK/index/delete behavior. STOP.
```

## Prompt 062 — Add Task DbSet to CrmDbContext

```text
REQUIREMENTS:
  TRACEABILITY: TASK-001..022; DATA-003
  REQUIREMENT LINKS: [TASK-001..022](../requirements/lightweight-crm-product-and-system-requirements.md#task-001); [DATA-003](../requirements/lightweight-crm-product-and-system-requirements.md#data-003)
  REQUIREMENT INTENT: Tasks belong to one Project and must support assignment, priority, status/completion/reopen behavior, overdue detection, and filterable task views. Use SQL Server/Azure SQL with service-owned databases, enforced relationships, UTC timestamps, safe public IDs, optimistic concurrency, and archival over routine hard deletion.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Add Task DbSet/configuration to CrmDbContext only.

CONSTRAINT: Preserve all prior mappings.

RESTRICTION: Do not create migration or repository methods.

USAGE: Follow database.md.

BEHAVIOR: Run model tests proving Task -> Project -> Client required relationships exist. STOP.
```

## Prompt 063 — Generate the initial Crm SQL Server migration

```text
REQUIREMENTS:
  TRACEABILITY: DATA-030..034; TEST-004
  REQUIREMENT LINKS: [DATA-030..034](../requirements/lightweight-crm-product-and-system-requirements.md#data-030); [TEST-004](../requirements/lightweight-crm-product-and-system-requirements.md#test-004)
  REQUIREMENT INTENT: Use Microsoft SQL Server/Azure SQL, one database per bounded service, no cross-service database queries, and controlled schema migrations. Automate business, authorization, API, SQL, messaging, audit, tracing, Function, and UI behavior at the boundary that can actually prove it.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Generate one EF Core migration for the current CrmDbContext model.

CONSTRAINT: Migration must include Client, Project, Task, Outbox and Inbox only and be SQL Server compatible.

RESTRICTION: Do not apply the migration. Do not hand-edit generated files. Do not add seed business data.

USAGE: Use the repository's approved EF startup-project convention and database.md.

BEHAVIOR: Generate migration, inspect generated operations for tables/FKs/indexes/concurrency types, verify no PostgreSQL artifacts, and STOP.
```

## Prompt 064 — Apply the initial Crm migration locally

```text
REQUIREMENTS:
  TRACEABILITY: DATA-030..034; DEPLOY-001
  REQUIREMENT LINKS: [DATA-030..034](../requirements/lightweight-crm-product-and-system-requirements.md#data-030); [DEPLOY-001](../requirements/lightweight-crm-product-and-system-requirements.md#deploy-001)
  REQUIREMENT INTENT: Use Microsoft SQL Server/Azure SQL, one database per bounded service, no cross-service database queries, and controlled schema migrations. Support environment-specific configuration, externalized secrets, Flex Consumption Functions, and consistent deployment/telemetry metadata.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Apply only the already-reviewed initial Crm migration to the local Aspire-managed CrmDb.

CONSTRAINT: Confirm target resource/connection is local development.

RESTRICTION: Do not alter migration, create another migration, or touch shared/production databases.

USAGE: Use the approved EF command with Aspire-provided connection configuration.

BEHAVIOR: Verify applied migration list and query SQL metadata for expected tables/FKs. STOP.
```

---

# Part 3 — Client business requirements

## Prompt 065 — Define Client create API contract

```text
REQUIREMENTS:
  TRACEABILITY: CLIENT-001..004; API-001..007; SEC-012..013
  REQUIREMENT LINKS: [CLIENT-001..004](../requirements/lightweight-crm-product-and-system-requirements.md#client-001); [API-001..007](../requirements/lightweight-crm-product-and-system-requirements.md#api-001); [SEC-012..013](../requirements/lightweight-crm-product-and-system-requirements.md#sec-012)
  REQUIREMENT INTENT: Authorized users can create Clients with the required CRM/contact/ownership metadata; names are searchable and likely duplicates are detected without silent merging. Use consistent REST routes, conventional HTTP verbs/status codes, bounded pagination, OpenAPI documentation, and versionable public contracts. Use ASP.NET Core Identity and server-side role/policy authorization with least privilege; protect APIs and never expose passwords, tokens, secrets, or sensitive internals.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Define the public POST Client request/response contracts only.

CONSTRAINT: Contract includes requirement-supported fields, concurrency/public ID semantics where relevant, validation shape and success/error status expectations; no EF types.

RESTRICTION: Do not implement controller, facade, business, repository, audit event, or React.

USAGE: Use add-endpoint skill contract-first step.

BEHAVIOR: Build Crm host and add serialization/contract tests. STOP.
```

## Prompt 066 — Implement Client create repository operation

```text
REQUIREMENTS:
  TRACEABILITY: CLIENT-001..004; DATA-004..005
  REQUIREMENT LINKS: [CLIENT-001..004](../requirements/lightweight-crm-product-and-system-requirements.md#client-001); [DATA-004..005](../requirements/lightweight-crm-product-and-system-requirements.md#data-004)
  REQUIREMENT INTENT: Authorized users can create Clients with the required CRM/contact/ownership metadata; names are searchable and likely duplicates are detected without silent merging. Use SQL Server/Azure SQL with service-owned databases, enforced relationships, UTC timestamps, safe public IDs, optimistic concurrency, and archival over routine hard deletion.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Add only the repository operation needed to insert a Client and query duplicate-detection candidates by normalized name/email/phone.

CONSTRAINT: Repository uses CrmDbContext only and exposes materialized results.

RESTRICTION: Do not open transactions, decide duplicate policy, create outbox messages, or call Business.

USAGE: Follow backend.md/database.md.

BEHAVIOR: Add SQL Server integration tests for insert and duplicate candidate query. STOP.
```

## Prompt 067 — Implement Client create Data transaction

```text
REQUIREMENTS:
  TRACEABILITY: CLIENT-001..004; AUDIT-001..008; OUTBOX-001..002
  REQUIREMENT LINKS: [CLIENT-001..004](../requirements/lightweight-crm-product-and-system-requirements.md#client-001); [AUDIT-001..008](../requirements/lightweight-crm-product-and-system-requirements.md#audit-001); [OUTBOX-001..002](../requirements/lightweight-crm-product-and-system-requirements.md#outbox-001)
  REQUIREMENT INTENT: Authorized users can create Clients with the required CRM/contact/ownership metadata; names are searchable and likely duplicates are detected without silent merging. Every Client/Project/Task mutation produces append-only audit evidence with entity/action/time/actor/source, trace/correlation, and applicable before/after values; secrets are redacted. Commit state and integration events atomically through a transactional outbox, then relay them with a timer-triggered Function and observable retry/backlog behavior.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Implement the Data-layer Client create transaction only.

CONSTRAINT: Persist the Client and the approved audit/integration event outbox row atomically; accept prepared Client/audit facts from Business; preserve actor/correlation metadata.

RESTRICTION: Do not decide validation, duplicate warning policy, lifecycle rules, send Service Bus messages, or add HTTP.

USAGE: Follow backend.md/messaging.md and approved audit ADR.

BEHAVIOR: Add SQL integration tests proving state + outbox commit together and both roll back on failure. STOP.
```

## Prompt 068 — Implement Client create Business behavior

```text
REQUIREMENTS:
  TRACEABILITY: CLIENT-001..004; AUDIT-001..003
  REQUIREMENT LINKS: [CLIENT-001..004](../requirements/lightweight-crm-product-and-system-requirements.md#client-001); [AUDIT-001..003](../requirements/lightweight-crm-product-and-system-requirements.md#audit-001)
  REQUIREMENT INTENT: Authorized users can create Clients with the required CRM/contact/ownership metadata; names are searchable and likely duplicates are detected without silent merging. Every business mutation must create append-only audit evidence describing what changed, when, who caused it, and applicable before/after values while preserving trace correlation and redacting secrets.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Implement Business-layer Client creation rules/model translation only.

CONSTRAINT: Set initial lifecycle according to requirements/approved rule, normalize business values, build the audit/business fact with safe changed-field metadata and call only Client Data.

RESTRICTION: Do not access EF, cache, HttpContext, Service Bus, or implement Facade/controller.

USAGE: Follow backend.md.

BEHAVIOR: Add unit tests for initial state, model translation and emitted audit fact. STOP.
```

## Prompt 069 — Implement Client create Facade behavior

```text
REQUIREMENTS:
  TRACEABILITY: CLIENT-001..004; SEC-010..013
  REQUIREMENT LINKS: [CLIENT-001..004](../requirements/lightweight-crm-product-and-system-requirements.md#client-001); [SEC-010..013](../requirements/lightweight-crm-product-and-system-requirements.md#sec-010)
  REQUIREMENT INTENT: Authorized users can create Clients with the required CRM/contact/ownership metadata; names are searchable and likely duplicates are detected without silent merging. Use ASP.NET Core Identity and server-side role/policy authorization with least privilege; protect APIs and never expose passwords, tokens, secrets, or sensitive internals.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Implement Facade validation, duplicate-warning evaluation and authorization call for Client create only.

CONSTRAINT: Facade calls only approved validators/context abstractions and Business. Duplicate matches warn/reject only according to the requirements/approved policy; never silently merge.

RESTRICTION: Do not access Data/EF, add controller, or create UI.

USAGE: Follow add-endpoint skill.

BEHAVIOR: Add unit tests for valid create, validation failure, duplicate warning path and unauthorized path. STOP.
```

## Prompt 070 — Add POST /clients controller action

```text
REQUIREMENTS:
  TRACEABILITY: CLIENT-001..004; API-001..007; SEC-010..013; ERROR-001..005
  REQUIREMENT LINKS: [CLIENT-001..004](../requirements/lightweight-crm-product-and-system-requirements.md#client-001); [API-001..007](../requirements/lightweight-crm-product-and-system-requirements.md#api-001); [SEC-010..013](../requirements/lightweight-crm-product-and-system-requirements.md#sec-010); [ERROR-001..005](../requirements/lightweight-crm-product-and-system-requirements.md#error-001)
  REQUIREMENT INTENT: Authorized users can create Clients with the required CRM/contact/ownership metadata; names are searchable and likely duplicates are detected without silent merging. Use consistent REST routes, conventional HTTP verbs/status codes, bounded pagination, OpenAPI documentation, and versionable public contracts. Also satisfy the remaining linked cross-cutting constraints that apply to this atomic step.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Add one POST Client controller action only.

CONSTRAINT: Transport-only: bind contract, capture trusted actor/correlation context, call Client Facade, map typed result to standard HTTP/ProblemDetails.

RESTRICTION: Do not inject Business/Data/Repository/DbContext or publish directly.

USAGE: Use add-endpoint skill.

BEHAVIOR: Add API tests for success, validation, duplicate-policy result, 401 and 403. Verify OpenAPI operation. STOP.
```

## Prompt 071 — Define paginated Client list contract

```text
REQUIREMENTS:
  TRACEABILITY: CLIENT-020..024; API-005
  REQUIREMENT LINKS: [CLIENT-020..024](../requirements/lightweight-crm-product-and-system-requirements.md#client-020); [API-005](../requirements/lightweight-crm-product-and-system-requirements.md#api-005)
  REQUIREMENT INTENT: Client collections require server-side pagination plus the specified search, filters, and sorts; unbounded result sets are prohibited. Expose consistent REST-oriented, documented, versionable APIs using conventional HTTP verbs/status codes and bounded pagination for collections.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Define only the gateway-visible Client list/search/filter/sort/pagination request and response contracts.

CONSTRAINT: Support name/contact/email/phone search, lifecycle/owner/active filters, approved sorts and bounded page size.

RESTRICTION: Do not implement query, controller or UI.

USAGE: Use add-endpoint contract step.

BEHAVIOR: Build and contract-test default/max pagination and enum validation. STOP.
```

## Prompt 072 — Implement Client list repository query

```text
REQUIREMENTS:
  TRACEABILITY: CLIENT-020..024; PERF-001..004
  REQUIREMENT LINKS: [CLIENT-020..024](../requirements/lightweight-crm-product-and-system-requirements.md#client-020); [PERF-001..004](../requirements/lightweight-crm-product-and-system-requirements.md#perf-001)
  REQUIREMENT INTENT: Client collections require server-side pagination plus the specified search, filters, and sorts; unbounded result sets are prohibited. Interactive APIs target responsive p95 behavior, bounded collections, efficient indexed searches, and no unnecessary N+1 query patterns.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Implement only the SQL-translatable repository query for Client list/search/filter/sort/pagination.

CONSTRAINT: Project only required fields; parameterize; use deterministic ordering/tie-breaker; enforce page bounds above repository if that is established.

RESTRICTION: Do not add Facade/Business/controller/cache/UI.

USAGE: Follow database.md.

BEHAVIOR: Add SQL integration tests for each filter class, search, sort, page boundaries and archived-default exclusion. STOP.
```

## Prompt 073 — Implement Client list Data/Business query

```text
REQUIREMENTS:
  TRACEABILITY: CLIENT-020..024
  REQUIREMENT LINKS: [CLIENT-020..024](../requirements/lightweight-crm-product-and-system-requirements.md#client-020)
  REQUIREMENT INTENT: Client collections require server-side pagination plus the specified search, filters, and sorts; unbounded result sets are prohibited.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Add the Client list Data operation and Business translation only.

CONSTRAINT: Data calls repository; Business translates to service results without HTTP/EF leakage.

RESTRICTION: Do not add Facade, cache, controller or React.

USAGE: Follow backend.md.

BEHAVIOR: Add unit tests for translation and integration test for returned pagination metadata. STOP.
```

## Prompt 074 — Implement Client list Facade query

```text
REQUIREMENTS:
  TRACEABILITY: CLIENT-020..024; SEC-010..013
  REQUIREMENT LINKS: [CLIENT-020..024](../requirements/lightweight-crm-product-and-system-requirements.md#client-020); [SEC-010..013](../requirements/lightweight-crm-product-and-system-requirements.md#sec-010)
  REQUIREMENT INTENT: Client collections require server-side pagination plus the specified search, filters, and sorts; unbounded result sets are prohibited. Use ASP.NET Core Identity and server-side role/policy authorization with least privilege; protect APIs and never expose passwords, tokens, secrets, or sensitive internals.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Implement Client list Facade validation/authorization only.

CONSTRAINT: Validate page/sort/filter values and enforce current user's authorized scope before Business call.

RESTRICTION: Do not add controller or repository changes.

USAGE: Follow add-endpoint skill.

BEHAVIOR: Add unit tests proving invalid paging fails before Business and authorization scope is passed correctly. STOP.
```

## Prompt 075 — Add GET /clients controller action

```text
REQUIREMENTS:
  TRACEABILITY: CLIENT-020..024; API-001..007; SEC-012
  REQUIREMENT LINKS: [CLIENT-020..024](../requirements/lightweight-crm-product-and-system-requirements.md#client-020); [API-001..007](../requirements/lightweight-crm-product-and-system-requirements.md#api-001); [SEC-012](../requirements/lightweight-crm-product-and-system-requirements.md#sec-012)
  REQUIREMENT INTENT: Client collections require server-side pagination plus the specified search, filters, and sorts; unbounded result sets are prohibited. Use consistent REST routes, conventional HTTP verbs/status codes, bounded pagination, OpenAPI documentation, and versionable public contracts. Use ASP.NET Core Identity and server-side role/policy authorization with least privilege; protect APIs and never expose passwords, tokens, secrets, or sensitive internals.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Add one GET Client collection action only.

CONSTRAINT: Map query-string contract to Facade, return paginated public response and standard errors.

RESTRICTION: Do not add UI or other Client actions.

USAGE: Use add-endpoint skill.

BEHAVIOR: Add API tests for default page, search/filter/sort, invalid query, 401/403. STOP.
```

## Prompt 076 — Implement Client detail query through Core

```text
REQUIREMENTS:
  TRACEABILITY: CLIENT-030..032; SEARCH-004
  REQUIREMENT LINKS: [CLIENT-030..032](../requirements/lightweight-crm-product-and-system-requirements.md#client-030); [SEARCH-004](../requirements/lightweight-crm-product-and-system-requirements.md#search-004)
  REQUIREMENT INTENT: Client detail must expose Client/lifecycle/owner information plus related Projects, Tasks, recent activity, and authorized audit history/navigation. Global search must find Clients, Projects, and Tasks, identify result type, and never reveal unauthorized data.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Implement the service-owned Client detail query from Repository -> Data -> Business -> Facade as one read-only seam.

CONSTRAINT: Return Client fields, lifecycle/owner and the requirement-defined project/task summaries using only CrmDb. Keep query efficient and authorization-aware.

RESTRICTION: Do not add controller, audit mutation, or UI.

USAGE: Follow backend.md; because this is one read-only use case, adjacent Core layers may be changed together but no transport/UI.

BEHAVIOR: Add SQL integration tests for existing/missing/archived/authorization-scoped Client and projection shape. STOP.
```

## Prompt 077 — Add GET /clients/{clientId} controller action

```text
REQUIREMENTS:
  TRACEABILITY: CLIENT-030..032; API-001..007; SEC-012
  REQUIREMENT LINKS: [CLIENT-030..032](../requirements/lightweight-crm-product-and-system-requirements.md#client-030); [API-001..007](../requirements/lightweight-crm-product-and-system-requirements.md#api-001); [SEC-012](../requirements/lightweight-crm-product-and-system-requirements.md#sec-012)
  REQUIREMENT INTENT: Client detail must expose Client/lifecycle/owner information plus related Projects, Tasks, recent activity, and authorized audit history/navigation. Use consistent REST routes, conventional HTTP verbs/status codes, bounded pagination, OpenAPI documentation, and versionable public contracts. Use ASP.NET Core Identity and server-side role/policy authorization with least privilege; protect APIs and never expose passwords, tokens, secrets, or sensitive internals.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Add one Client detail controller action only.

CONSTRAINT: Call only the Client detail Facade and map not-found/forbidden correctly.

RESTRICTION: Do not add UI or modify Core behavior.

USAGE: Use add-endpoint skill.

BEHAVIOR: Add API tests for 200, 404, 401 and 403. STOP.
```

## Prompt 078 — Implement Client lifecycle transition Core behavior

```text
REQUIREMENTS:
  TRACEABILITY: CLIENT-010..015; AUDIT-001..008; DATA-008
  REQUIREMENT LINKS: [CLIENT-010..015](../requirements/lightweight-crm-product-and-system-requirements.md#client-010); [AUDIT-001..008](../requirements/lightweight-crm-product-and-system-requirements.md#audit-001); [DATA-008](../requirements/lightweight-crm-product-and-system-requirements.md#data-008)
  REQUIREMENT INTENT: Each Client has one lifecycle status; lifecycle changes are audited, archived Clients are excluded by default, and archival preserves history. Every Client/Project/Task mutation produces append-only audit evidence with entity/action/time/actor/source, trace/correlation, and applicable before/after values; secrets are redacted. Use SQL Server/Azure SQL with service-owned databases, enforced relationships, UTC timestamps, safe public IDs, optimistic concurrency, and archival over routine hard deletion.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Implement one Client lifecycle-transition use case inside Core.

CONSTRAINT: Business owns allowed transition rules; Data uses optimistic concurrency and writes state + audit/outbox atomically; archived handling follows requirements; correlation/actor preserved.

RESTRICTION: Do not add controller or UI. Do not invent transitions beyond the allowed statuses/rules.

USAGE: Follow backend.md/messaging.md.

BEHAVIOR: Add unit tests for allowed/rejected transitions and SQL integration tests for concurrency plus state/outbox atomicity. STOP.
```

## Prompt 079 — Add Client lifecycle transition API action

```text
REQUIREMENTS:
  TRACEABILITY: CLIENT-010..015; API-001..007; SEC-012..013
  REQUIREMENT LINKS: [CLIENT-010..015](../requirements/lightweight-crm-product-and-system-requirements.md#client-010); [API-001..007](../requirements/lightweight-crm-product-and-system-requirements.md#api-001); [SEC-012..013](../requirements/lightweight-crm-product-and-system-requirements.md#sec-012)
  REQUIREMENT INTENT: Each Client has one lifecycle status; lifecycle changes are audited, archived Clients are excluded by default, and archival preserves history. Use consistent REST routes, conventional HTTP verbs/status codes, bounded pagination, OpenAPI documentation, and versionable public contracts. Use ASP.NET Core Identity and server-side role/policy authorization with least privilege; protect APIs and never expose passwords, tokens, secrets, or sensitive internals.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Add the single public Client lifecycle transition endpoint and contract only.

CONSTRAINT: Require expected concurrency token/version if approved; map conflict/validation/forbidden/not-found through standard ProblemDetails.

RESTRICTION: Do not add another Client update action or UI.

USAGE: Use add-endpoint skill.

BEHAVIOR: Add API tests for success, invalid transition, stale version conflict, 401 and 403. STOP.
```

## Prompt 080 — Implement Client archive/restore Core behavior

```text
REQUIREMENTS:
  TRACEABILITY: CLIENT-013..015; DATA-020..023; AUDIT-001..008
  REQUIREMENT LINKS: [CLIENT-013..015](../requirements/lightweight-crm-product-and-system-requirements.md#client-013); [DATA-020..023](../requirements/lightweight-crm-product-and-system-requirements.md#data-020); [AUDIT-001..008](../requirements/lightweight-crm-product-and-system-requirements.md#audit-001)
  REQUIREMENT INTENT: Client archival is non-destructive; archived Clients are excluded from normal active lists and Clients with active Projects cannot be permanently removed. Normal workflows archive rather than destructively delete records; history remains available and permanent purge is privileged and retention/privacy governed. Every Client/Project/Task mutation produces append-only audit evidence with entity/action/time/actor/source, trace/correlation, and applicable before/after values; secrets are redacted.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Implement Client archive and restore as one paired state-management use case in Core.

CONSTRAINT: Archive is non-destructive; enforce active-project restriction from requirements; each successful mutation writes the approved audit/outbox fact atomically.

RESTRICTION: Do not add physical purge, controller or UI.

USAGE: Follow backend.md.

BEHAVIOR: Add unit/integration tests for archive allowed, archive blocked by active projects, restore, history preserved and outbox atomicity. STOP.
```

## Prompt 081 — Add Client archive/restore API actions

```text
REQUIREMENTS:
  TRACEABILITY: CLIENT-013..015; API-001..007; SEC-012..013
  REQUIREMENT LINKS: [CLIENT-013..015](../requirements/lightweight-crm-product-and-system-requirements.md#client-013); [API-001..007](../requirements/lightweight-crm-product-and-system-requirements.md#api-001); [SEC-012..013](../requirements/lightweight-crm-product-and-system-requirements.md#sec-012)
  REQUIREMENT INTENT: Client archival is non-destructive; archived Clients are excluded from normal active lists and Clients with active Projects cannot be permanently removed. Use consistent REST routes, conventional HTTP verbs/status codes, bounded pagination, OpenAPI documentation, and versionable public contracts. Use ASP.NET Core Identity and server-side role/policy authorization with least privilege; protect APIs and never expose passwords, tokens, secrets, or sensitive internals.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Add only the public archive and restore transport actions using the already-implemented Facade.

CONSTRAINT: Keep controllers transport-only and authorization explicit.

RESTRICTION: Do not implement purge or UI.

USAGE: Use add-endpoint skill.

BEHAVIOR: Add API tests for authorized success, blocked archive, 404, 401 and 403. STOP.
```

## Prompt 081A — Implement Client profile update Core behavior

```text
REQUIREMENTS:
  TRACEABILITY: CLIENT-002; AUDIT-001..008; DATA-008; SEC-010..013
  REQUIREMENT LINKS: [CLIENT-002](../requirements/lightweight-crm-product-and-system-requirements.md#client-002); [AUDIT-001..008](../requirements/lightweight-crm-product-and-system-requirements.md#audit-001); [DATA-008](../requirements/lightweight-crm-product-and-system-requirements.md#data-008); [SEC-010..013](../requirements/lightweight-crm-product-and-system-requirements.md#sec-010)
  REQUIREMENT INTENT: Clients are the CRM anchor and must support the required data, lifecycle/archive behavior, searchable paginated lists, detail views, ownership, and auditable changes. Every Client/Project/Task mutation produces append-only audit evidence with entity/action/time/actor/source, trace/correlation, and applicable before/after values; secrets are redacted. Also satisfy the remaining linked cross-cutting constraints that apply to this atomic step.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Implement the ordinary editable Client profile update use case inside Crm.Core only.

CONSTRAINT: Update only user-editable Client contact/address/website/description/owner fields defined by the requirements; lifecycle and archive state remain dedicated operations. Business owns normalization/rules; Facade validates/authorizes; Data persists with optimistic concurrency and audit/outbox atomically.

RESTRICTION: Do not change lifecycle status, archive state, Projects, Tasks, controller, or UI.

USAGE: Follow backend.md, messaging.md and add-endpoint skill.

BEHAVIOR: Add unit tests for editable/non-editable fields and SQL integration tests for concurrency plus before/after audit change-set atomicity; STOP.
```

## Prompt 081B — Add Client profile update API action

```text
REQUIREMENTS:
  TRACEABILITY: CLIENT-002; API-001..007; SEC-012..013
  REQUIREMENT LINKS: [CLIENT-002](../requirements/lightweight-crm-product-and-system-requirements.md#client-002); [API-001..007](../requirements/lightweight-crm-product-and-system-requirements.md#api-001); [SEC-012..013](../requirements/lightweight-crm-product-and-system-requirements.md#sec-012)
  REQUIREMENT INTENT: Clients are the CRM anchor and must support the required data, lifecycle/archive behavior, searchable paginated lists, detail views, ownership, and auditable changes. Use consistent REST routes, conventional HTTP verbs/status codes, bounded pagination, OpenAPI documentation, and versionable public contracts. Use ASP.NET Core Identity and server-side role/policy authorization with least privilege; protect APIs and never expose passwords, tokens, secrets, or sensitive internals.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Add one Client profile update HTTP contract/action only.

CONSTRAINT: Require the approved concurrency token/version; map validation/not-found/conflict/forbidden through standard ProblemDetails; controller calls only Facade.

RESTRICTION: Do not combine lifecycle/archive mutations or add UI.

USAGE: Use add-endpoint skill.

BEHAVIOR: Add API tests for success, invalid field, stale version, 401 and 403; verify OpenAPI operation and STOP.
```

---

# Part 4 — Project business requirements

## Prompt 082 — Define Project create API contract

```text
REQUIREMENTS:
  TRACEABILITY: PROJECT-001..003; API-001..007
  REQUIREMENT LINKS: [PROJECT-001..003](../requirements/lightweight-crm-product-and-system-requirements.md#project-001); [API-001..007](../requirements/lightweight-crm-product-and-system-requirements.md#api-001)
  REQUIREMENT INTENT: Authorized users can create a Project for exactly one Client using the required Project metadata. Use consistent REST routes, conventional HTTP verbs/status codes, bounded pagination, OpenAPI documentation, and versionable public contracts.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Define only the public Project create request/response contract.

CONSTRAINT: Require ClientId and requirement-supported fields/status defaults; no EF entity leakage.

RESTRICTION: Do not implement Core/controller/UI.

USAGE: Use add-endpoint contract step.

BEHAVIOR: Build and add contract tests. STOP.
```

## Prompt 083 — Implement Project create repository/Data

```text
REQUIREMENTS:
  TRACEABILITY: PROJECT-001..003; DATA-002; AUDIT-001..008; OUTBOX-001..002
  REQUIREMENT LINKS: [PROJECT-001..003](../requirements/lightweight-crm-product-and-system-requirements.md#project-001); [DATA-002](../requirements/lightweight-crm-product-and-system-requirements.md#data-002); [AUDIT-001..008](../requirements/lightweight-crm-product-and-system-requirements.md#audit-001); [OUTBOX-001..002](../requirements/lightweight-crm-product-and-system-requirements.md#outbox-001)
  REQUIREMENT INTENT: Authorized users can create a Project for exactly one Client using the required Project metadata. Use SQL Server/Azure SQL with service-owned databases, enforced relationships, UTC timestamps, safe public IDs, optimistic concurrency, and archival over routine hard deletion. Also satisfy the remaining linked cross-cutting constraints that apply to this atomic step.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Implement Project repository insert plus Data transaction only.

CONSTRAINT: Verify Client exists in the same Crm database; persist Project + approved audit/outbox atomically.

RESTRICTION: Do not add Business/Facade/controller or cross-service validation.

USAGE: Follow backend.md/database.md/messaging.md.

BEHAVIOR: Add SQL integration tests for valid Client, missing Client, and rollback/outbox atomicity. STOP.
```

## Prompt 084 — Implement Project create Business/Facade

```text
REQUIREMENTS:
  TRACEABILITY: PROJECT-001..003; SEC-010..013
  REQUIREMENT LINKS: [PROJECT-001..003](../requirements/lightweight-crm-product-and-system-requirements.md#project-001); [SEC-010..013](../requirements/lightweight-crm-product-and-system-requirements.md#sec-010)
  REQUIREMENT INTENT: Authorized users can create a Project for exactly one Client using the required Project metadata. Use ASP.NET Core Identity and server-side role/policy authorization with least privilege; protect APIs and never expose passwords, tokens, secrets, or sensitive internals.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Implement Project create Business and Facade behavior only.

CONSTRAINT: Business applies defaults/model translation/audit fact; Facade validates and authorizes against Client scope, then calls Business.

RESTRICTION: Do not add controller or UI.

USAGE: Follow add-endpoint skill.

BEHAVIOR: Add unit tests for defaults, validation, authorized/forbidden Client ownership and Business invocation. STOP.
```

## Prompt 085 — Add POST Project controller action

```text
REQUIREMENTS:
  TRACEABILITY: PROJECT-001..003; API-001..007; SEC-012..013
  REQUIREMENT LINKS: [PROJECT-001..003](../requirements/lightweight-crm-product-and-system-requirements.md#project-001); [API-001..007](../requirements/lightweight-crm-product-and-system-requirements.md#api-001); [SEC-012..013](../requirements/lightweight-crm-product-and-system-requirements.md#sec-012)
  REQUIREMENT INTENT: Authorized users can create a Project for exactly one Client using the required Project metadata. Use consistent REST routes, conventional HTTP verbs/status codes, bounded pagination, OpenAPI documentation, and versionable public contracts. Use ASP.NET Core Identity and server-side role/policy authorization with least privilege; protect APIs and never expose passwords, tokens, secrets, or sensitive internals.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Add the single Project create controller action under the stable public route.

CONSTRAINT: Transport-only mapping to Facade and standard ProblemDetails.

RESTRICTION: Do not add list/detail/status actions.

USAGE: Use add-endpoint skill.

BEHAVIOR: Add API tests for 201/200 per contract, missing Client, invalid input, 401/403. STOP.
```

## Prompt 086 — Implement Project list/search Core query

```text
REQUIREMENTS:
  TRACEABILITY: PROJECT-020..023; PERF-001..004
  REQUIREMENT LINKS: [PROJECT-020..023](../requirements/lightweight-crm-product-and-system-requirements.md#project-020); [PERF-001..004](../requirements/lightweight-crm-product-and-system-requirements.md#perf-001)
  REQUIREMENT INTENT: Project collections require the specified views, filters/search, and server-side pagination. Interactive APIs target responsive p95 behavior, bounded collections, efficient indexed searches, and no unnecessary N+1 query patterns.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Implement Project repository/Data/Business/Facade list query as one read-only seam.

CONSTRAINT: Support Client/status/owner/priority/start/target filters, name/client-name/description search, server pagination and deterministic sorting. Respect authorization scope.

RESTRICTION: Do not add controller/UI.

USAGE: Follow backend.md/database.md.

BEHAVIOR: Add SQL integration tests for filters/search/pagination and unit tests for scope/translation. STOP.
```

## Prompt 087 — Add GET /projects controller action

```text
REQUIREMENTS:
  TRACEABILITY: PROJECT-020..023; API-005; SEC-012
  REQUIREMENT LINKS: [PROJECT-020..023](../requirements/lightweight-crm-product-and-system-requirements.md#project-020); [API-005](../requirements/lightweight-crm-product-and-system-requirements.md#api-005); [SEC-012](../requirements/lightweight-crm-product-and-system-requirements.md#sec-012)
  REQUIREMENT INTENT: Project collections require the specified views, filters/search, and server-side pagination. Expose consistent REST-oriented, documented, versionable APIs using conventional HTTP verbs/status codes and bounded pagination for collections. Use ASP.NET Core Identity and server-side role/policy authorization with least privilege; protect APIs and never expose passwords, tokens, secrets, or sensitive internals.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Add one Project collection controller action only.

CONSTRAINT: Map approved query contract to Facade; return paginated public model.

RESTRICTION: Do not add detail/status/UI.

USAGE: Use add-endpoint skill.

BEHAVIOR: Add API tests for key filters, pagination, invalid query, 401/403. STOP.
```

## Prompt 088 — Implement Project detail Core query

```text
REQUIREMENTS:
  TRACEABILITY: PROJECT-030..031
  REQUIREMENT LINKS: [PROJECT-030..031](../requirements/lightweight-crm-product-and-system-requirements.md#project-030)
  REQUIREMENT INTENT: Project detail must show Client, status/owner/priority/dates, open/completed Tasks, recent activity, authorized audit history, and Task-creation navigation.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Implement Project detail query through Repository -> Data -> Business -> Facade only.

CONSTRAINT: Include Client reference, status/owner/priority/dates, open tasks, completed tasks, recent activity/audit link metadata as available without querying AuditDb directly.

RESTRICTION: Do not add controller/UI or direct Audit database access.

USAGE: Follow backend.md and audit boundary rules.

BEHAVIOR: Add SQL integration tests for projection and authorization-scoped not-found/forbidden behavior. STOP.
```

## Prompt 089 — Add GET /projects/{projectId} controller action

```text
REQUIREMENTS:
  TRACEABILITY: PROJECT-030..031; API-001..007
  REQUIREMENT LINKS: [PROJECT-030..031](../requirements/lightweight-crm-product-and-system-requirements.md#project-030); [API-001..007](../requirements/lightweight-crm-product-and-system-requirements.md#api-001)
  REQUIREMENT INTENT: Project detail must show Client, status/owner/priority/dates, open/completed Tasks, recent activity, authorized audit history, and Task-creation navigation. Use consistent REST routes, conventional HTTP verbs/status codes, bounded pagination, OpenAPI documentation, and versionable public contracts.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Add one Project detail controller action only.

CONSTRAINT: Call only Facade and map 200/404/401/403.

RESTRICTION: Do not add other Project actions or UI.

USAGE: Use add-endpoint skill.

BEHAVIOR: Run focused API tests and STOP.
```

## Prompt 090 — Implement Project status transition Core behavior

```text
REQUIREMENTS:
  TRACEABILITY: PROJECT-010..014; AUDIT-001..008; DATA-008
  REQUIREMENT LINKS: [PROJECT-010..014](../requirements/lightweight-crm-product-and-system-requirements.md#project-010); [AUDIT-001..008](../requirements/lightweight-crm-product-and-system-requirements.md#audit-001); [DATA-008](../requirements/lightweight-crm-product-and-system-requirements.md#data-008)
  REQUIREMENT INTENT: Project status changes are auditable; completion records actual completion time and requires acknowledgement when open Tasks remain; archival is non-destructive. Every Client/Project/Task mutation produces append-only audit evidence with entity/action/time/actor/source, trace/correlation, and applicable before/after values; secrets are redacted. Use SQL Server/Azure SQL with service-owned databases, enforced relationships, UTC timestamps, safe public IDs, optimistic concurrency, and archival over routine hard deletion.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Implement Project status transition inside Core only.

CONSTRAINT: Business owns Planned/Active/On Hold/Completed/Cancelled/Archived rules; completing records actual completion UTC and requires explicit acknowledgement when open Tasks exist; Data persists state + audit/outbox atomically with concurrency protection.

RESTRICTION: Do not add controller/UI or auto-complete Tasks.

USAGE: Follow backend.md/messaging.md.

BEHAVIOR: Add unit tests for allowed/rejected transitions/open-task acknowledgement and SQL tests for completion timestamp/concurrency/outbox atomicity. STOP.
```

## Prompt 091 — Add Project status transition API action

```text
REQUIREMENTS:
  TRACEABILITY: PROJECT-010..014; API-001..007; SEC-012..013
  REQUIREMENT LINKS: [PROJECT-010..014](../requirements/lightweight-crm-product-and-system-requirements.md#project-010); [API-001..007](../requirements/lightweight-crm-product-and-system-requirements.md#api-001); [SEC-012..013](../requirements/lightweight-crm-product-and-system-requirements.md#sec-012)
  REQUIREMENT INTENT: Project status changes are auditable; completion records actual completion time and requires acknowledgement when open Tasks remain; archival is non-destructive. Use consistent REST routes, conventional HTTP verbs/status codes, bounded pagination, OpenAPI documentation, and versionable public contracts. Use ASP.NET Core Identity and server-side role/policy authorization with least privilege; protect APIs and never expose passwords, tokens, secrets, or sensitive internals.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Add the Project status transition transport contract/action only.

CONSTRAINT: Expose explicit open-task acknowledgement and expected concurrency token/version if required.

RESTRICTION: Do not add UI or archive-specific extra endpoint.

USAGE: Use add-endpoint skill.

BEHAVIOR: Add API tests for success, missing acknowledgement, invalid transition, stale version, 401/403. STOP.
```

## Prompt 092 — Implement Project archive behavior

```text
REQUIREMENTS:
  TRACEABILITY: PROJECT-014; DATA-020..023; AUDIT-001..008
  REQUIREMENT LINKS: [PROJECT-014](../requirements/lightweight-crm-product-and-system-requirements.md#project-014); [DATA-020..023](../requirements/lightweight-crm-product-and-system-requirements.md#data-020); [AUDIT-001..008](../requirements/lightweight-crm-product-and-system-requirements.md#audit-001)
  REQUIREMENT INTENT: Projects belong to one Client and must support the required metadata, statuses, filtering/search/detail behavior, completion rules, and non-destructive archival. Normal workflows archive rather than destructively delete records; history remains available and permanent purge is privileged and retention/privacy governed. Every Client/Project/Task mutation produces append-only audit evidence with entity/action/time/actor/source, trace/correlation, and applicable before/after values; secrets are redacted.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Implement Project archival in Core only.

CONSTRAINT: Archive is non-destructive and writes audit/outbox atomically; historical Tasks remain.

RESTRICTION: Do not physically delete, add purge, controller or UI.

USAGE: Follow backend.md.

BEHAVIOR: Add tests proving archived Project excluded from default active queries and history preserved. STOP.
```

## Prompt 093 — Add Project archive API action

```text
REQUIREMENTS:
  TRACEABILITY: PROJECT-014; API-001..007
  REQUIREMENT LINKS: [PROJECT-014](../requirements/lightweight-crm-product-and-system-requirements.md#project-014); [API-001..007](../requirements/lightweight-crm-product-and-system-requirements.md#api-001)
  REQUIREMENT INTENT: Projects belong to one Client and must support the required metadata, statuses, filtering/search/detail behavior, completion rules, and non-destructive archival. Use consistent REST routes, conventional HTTP verbs/status codes, bounded pagination, OpenAPI documentation, and versionable public contracts.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Add one Project archive controller action only.

CONSTRAINT: Call existing Facade; map authorization/conflict/errors consistently.

RESTRICTION: Do not add restore unless requirements/ADR explicitly call for it.

USAGE: Use add-endpoint skill.

BEHAVIOR: Run focused API tests and STOP.
```

## Prompt 093A — Implement Project details update Core behavior

```text
REQUIREMENTS:
  TRACEABILITY: PROJECT-002; AUDIT-001..008; DATA-008; SEC-010..013
  REQUIREMENT LINKS: [PROJECT-002](../requirements/lightweight-crm-product-and-system-requirements.md#project-002); [AUDIT-001..008](../requirements/lightweight-crm-product-and-system-requirements.md#audit-001); [DATA-008](../requirements/lightweight-crm-product-and-system-requirements.md#data-008); [SEC-010..013](../requirements/lightweight-crm-product-and-system-requirements.md#sec-010)
  REQUIREMENT INTENT: Projects belong to one Client and must support the required metadata, statuses, filtering/search/detail behavior, completion rules, and non-destructive archival. Every Client/Project/Task mutation produces append-only audit evidence with entity/action/time/actor/source, trace/correlation, and applicable before/after values; secrets are redacted. Also satisfy the remaining linked cross-cutting constraints that apply to this atomic step.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Implement ordinary Project detail editing inside Crm.Core only.

CONSTRAINT: Update name, description, priority, owner, start/target dates and notes as allowed; Project status/completion/archive remain dedicated operations. Use concurrency protection and atomic audit/outbox persistence.

RESTRICTION: Do not change Client ownership, Project status, actual completion date, archive state, controller or UI.

USAGE: Follow backend.md and add-endpoint skill.

BEHAVIOR: Add rule/unit tests and SQL integration tests for concurrency and audit before/after values; STOP.
```

## Prompt 093B — Add Project details update API action

```text
REQUIREMENTS:
  TRACEABILITY: PROJECT-002; API-001..007; SEC-012..013
  REQUIREMENT LINKS: [PROJECT-002](../requirements/lightweight-crm-product-and-system-requirements.md#project-002); [API-001..007](../requirements/lightweight-crm-product-and-system-requirements.md#api-001); [SEC-012..013](../requirements/lightweight-crm-product-and-system-requirements.md#sec-012)
  REQUIREMENT INTENT: Projects belong to one Client and must support the required metadata, statuses, filtering/search/detail behavior, completion rules, and non-destructive archival. Use consistent REST routes, conventional HTTP verbs/status codes, bounded pagination, OpenAPI documentation, and versionable public contracts. Use ASP.NET Core Identity and server-side role/policy authorization with least privilege; protect APIs and never expose passwords, tokens, secrets, or sensitive internals.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Add one Project details update HTTP contract/action only.

CONSTRAINT: Transport-only; require approved concurrency token/version and map typed errors consistently.

RESTRICTION: Do not combine status/archive changes or add UI.

USAGE: Use add-endpoint skill.

BEHAVIOR: Add API tests for success, invalid dates, stale version, 401/403 and STOP.
```

---

# Part 5 — Task business requirements

## Prompt 094 — Define Task create API contract

```text
REQUIREMENTS:
  TRACEABILITY: TASK-001..016; API-001..007
  REQUIREMENT LINKS: [TASK-001..016](../requirements/lightweight-crm-product-and-system-requirements.md#task-001); [API-001..007](../requirements/lightweight-crm-product-and-system-requirements.md#api-001)
  REQUIREMENT INTENT: Authorized users can create Tasks for one Project with the required metadata, statuses, priorities, assignment, completion, reopen, and overdue behavior. Use consistent REST routes, conventional HTTP verbs/status codes, bounded pagination, OpenAPI documentation, and versionable public contracts.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Define only the public Task create request/response contract.

CONSTRAINT: Require ProjectId and supported fields; status/priority defaults must follow requirements; no EF types.

RESTRICTION: Do not implement Core/controller/UI.

USAGE: Use add-endpoint contract step.

BEHAVIOR: Build and contract-test. STOP.
```

## Prompt 095 — Implement Task create repository/Data

```text
REQUIREMENTS:
  TRACEABILITY: TASK-001..016; DATA-003; AUDIT-001..008
  REQUIREMENT LINKS: [TASK-001..016](../requirements/lightweight-crm-product-and-system-requirements.md#task-001); [DATA-003](../requirements/lightweight-crm-product-and-system-requirements.md#data-003); [AUDIT-001..008](../requirements/lightweight-crm-product-and-system-requirements.md#audit-001)
  REQUIREMENT INTENT: Authorized users can create Tasks for one Project with the required metadata, statuses, priorities, assignment, completion, reopen, and overdue behavior. Use SQL Server/Azure SQL with service-owned databases, enforced relationships, UTC timestamps, safe public IDs, optimistic concurrency, and archival over routine hard deletion. Every Client/Project/Task mutation produces append-only audit evidence with entity/action/time/actor/source, trace/correlation, and applicable before/after values; secrets are redacted.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Implement Task insert repository operation and Data transaction only.

CONSTRAINT: Verify owning Project exists in CrmDb; persist Task + audit/outbox atomically.

RESTRICTION: Do not add Business/Facade/controller.

USAGE: Follow backend.md/database.md/messaging.md.

BEHAVIOR: Add SQL integration tests for valid/missing Project and rollback/outbox atomicity. STOP.
```

## Prompt 096 — Implement Task create Business/Facade

```text
REQUIREMENTS:
  TRACEABILITY: TASK-001..016; SEC-010..013
  REQUIREMENT LINKS: [TASK-001..016](../requirements/lightweight-crm-product-and-system-requirements.md#task-001); [SEC-010..013](../requirements/lightweight-crm-product-and-system-requirements.md#sec-010)
  REQUIREMENT INTENT: Authorized users can create Tasks for one Project with the required metadata, statuses, priorities, assignment, completion, reopen, and overdue behavior. Use ASP.NET Core Identity and server-side role/policy authorization with least privilege; protect APIs and never expose passwords, tokens, secrets, or sensitive internals.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Implement Task create Business and Facade only.

CONSTRAINT: Business applies default status/priority and audit fact; Facade validates, authorizes Project scope and assignee reference format/allowance according to approved identity-reference policy.

RESTRICTION: Do not query IdentityDb directly and do not add controller/UI.

USAGE: Follow add-endpoint skill and identity boundary rules.

BEHAVIOR: Add unit tests for defaults, validation, authorization and no cross-service Identity access. STOP.
```

## Prompt 097 — Add POST Task controller action

```text
REQUIREMENTS:
  TRACEABILITY: TASK-001..016; API-001..007
  REQUIREMENT LINKS: [TASK-001..016](../requirements/lightweight-crm-product-and-system-requirements.md#task-001); [API-001..007](../requirements/lightweight-crm-product-and-system-requirements.md#api-001)
  REQUIREMENT INTENT: Authorized users can create Tasks for one Project with the required metadata, statuses, priorities, assignment, completion, reopen, and overdue behavior. Use consistent REST routes, conventional HTTP verbs/status codes, bounded pagination, OpenAPI documentation, and versionable public contracts.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Add the single Task create controller action only.

CONSTRAINT: Transport-only mapping to Facade and standard errors.

RESTRICTION: Do not add list/status/assignment actions.

USAGE: Use add-endpoint skill.

BEHAVIOR: Add API tests for success, invalid/missing Project, unauthorized and forbidden. STOP.
```

## Prompt 098 — Implement Task list/filter Core query

```text
REQUIREMENTS:
  TRACEABILITY: TASK-020..022; PERF-001..004
  REQUIREMENT LINKS: [TASK-020..022](../requirements/lightweight-crm-product-and-system-requirements.md#task-020); [PERF-001..004](../requirements/lightweight-crm-product-and-system-requirements.md#perf-001)
  REQUIREMENT INTENT: Task collections support My Tasks/project/open/completed/overdue views plus the required filters and sorts. Interactive APIs target responsive p95 behavior, bounded collections, efficient indexed searches, and no unnecessary N+1 query patterns.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Implement Task list query through Repository/Data/Business/Facade only.

CONSTRAINT: Support status, priority, assignee, Project, Client and due-date filters; open/completed/overdue/current-user views; deterministic sorting by due/priority/created/modified; bounded pagination.

RESTRICTION: Do not add controller/UI.

USAGE: Follow backend.md/database.md.

BEHAVIOR: Add SQL integration tests for each required view/filter and overdue semantics. STOP.
```

## Prompt 099 — Add GET /tasks controller action

```text
REQUIREMENTS:
  TRACEABILITY: TASK-020..022; API-005
  REQUIREMENT LINKS: [TASK-020..022](../requirements/lightweight-crm-product-and-system-requirements.md#task-020); [API-005](../requirements/lightweight-crm-product-and-system-requirements.md#api-005)
  REQUIREMENT INTENT: Task collections support My Tasks/project/open/completed/overdue views plus the required filters and sorts. Expose consistent REST-oriented, documented, versionable APIs using conventional HTTP verbs/status codes and bounded pagination for collections.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Add one Task collection controller action only.

CONSTRAINT: Map approved query contract to Facade and return paginated response.

RESTRICTION: Do not add mutations or UI.

USAGE: Use add-endpoint skill.

BEHAVIOR: Add API tests for My Tasks, overdue, status/priority filters, pagination, 401/403. STOP.
```

## Prompt 100 — Implement Task assignment Core behavior

```text
REQUIREMENTS:
  TRACEABILITY: TASK-013..014; AUDIT-001..008
  REQUIREMENT LINKS: [TASK-013..014](../requirements/lightweight-crm-product-and-system-requirements.md#task-013); [AUDIT-001..008](../requirements/lightweight-crm-product-and-system-requirements.md#audit-001)
  REQUIREMENT INTENT: Tasks can be assigned/reassigned and each assignment change is auditable. Every Client/Project/Task mutation produces append-only audit evidence with entity/action/time/actor/source, trace/correlation, and applicable before/after values; secrets are redacted.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Implement assign/reassign Task use case in Core only.

CONSTRAINT: Business validates state rules; Facade authorizes; Data persists assignee change + audit/outbox atomically with concurrency protection.

RESTRICTION: Do not query IdentityDb directly, add controller, or change task status.

USAGE: Follow backend.md/identity.md/messaging.md.

BEHAVIOR: Add unit tests for assign/reassign and SQL tests for audit/outbox/concurrency. STOP.
```

## Prompt 101 — Add Task assignment API action

```text
REQUIREMENTS:
  TRACEABILITY: TASK-013..014; API-001..007; SEC-012..013
  REQUIREMENT LINKS: [TASK-013..014](../requirements/lightweight-crm-product-and-system-requirements.md#task-013); [API-001..007](../requirements/lightweight-crm-product-and-system-requirements.md#api-001); [SEC-012..013](../requirements/lightweight-crm-product-and-system-requirements.md#sec-012)
  REQUIREMENT INTENT: Tasks can be assigned/reassigned and each assignment change is auditable. Use consistent REST routes, conventional HTTP verbs/status codes, bounded pagination, OpenAPI documentation, and versionable public contracts. Use ASP.NET Core Identity and server-side role/policy authorization with least privilege; protect APIs and never expose passwords, tokens, secrets, or sensitive internals.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Add one Task assign/reassign controller action only.

CONSTRAINT: Use typed contract and expected version/token if approved.

RESTRICTION: Do not add status/priority UI.

USAGE: Use add-endpoint skill.

BEHAVIOR: Add API tests for assign, reassign, stale version, 401/403. STOP.
```

## Prompt 102 — Implement Task priority change Core behavior

```text
REQUIREMENTS:
  TRACEABILITY: TASK-015; AUDIT-001..008
  REQUIREMENT LINKS: [TASK-015](../requirements/lightweight-crm-product-and-system-requirements.md#task-015); [AUDIT-001..008](../requirements/lightweight-crm-product-and-system-requirements.md#audit-001)
  REQUIREMENT INTENT: Task priority is limited to Low, Normal, High, or Critical. Every Client/Project/Task mutation produces append-only audit evidence with entity/action/time/actor/source, trace/correlation, and applicable before/after values; secrets are redacted.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Implement Task priority change in Core only.

CONSTRAINT: Allow Low/Normal/High/Critical only; persist + audit/outbox atomically with concurrency protection.

RESTRICTION: Do not change assignment/status or add controller/UI.

USAGE: Follow backend.md.

BEHAVIOR: Add unit/integration tests for valid priorities, invalid value and concurrency. STOP.
```

## Prompt 103 — Add Task priority API action

```text
REQUIREMENTS:
  TRACEABILITY: TASK-015; API-001..007
  REQUIREMENT LINKS: [TASK-015](../requirements/lightweight-crm-product-and-system-requirements.md#task-015); [API-001..007](../requirements/lightweight-crm-product-and-system-requirements.md#api-001)
  REQUIREMENT INTENT: Task priority is limited to Low, Normal, High, or Critical. Use consistent REST routes, conventional HTTP verbs/status codes, bounded pagination, OpenAPI documentation, and versionable public contracts.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Add one Task priority update controller action only.

CONSTRAINT: Transport-only and authorization-aware.

RESTRICTION: Do not add status or assignment behavior.

USAGE: Use add-endpoint skill.

BEHAVIOR: Run focused API tests and STOP.
```

## Prompt 104 — Implement Task status transition Core behavior

```text
REQUIREMENTS:
  TRACEABILITY: TASK-010..016; AUDIT-001..008
  REQUIREMENT LINKS: [TASK-010..016](../requirements/lightweight-crm-product-and-system-requirements.md#task-010); [AUDIT-001..008](../requirements/lightweight-crm-product-and-system-requirements.md#audit-001)
  REQUIREMENT INTENT: Task workflow includes required statuses, assignment/reassignment, priority changes, completion timestamps, explicit reopen, and overdue semantics with auditable mutations. Every Client/Project/Task mutation produces append-only audit evidence with entity/action/time/actor/source, trace/correlation, and applicable before/after values; secrets are redacted.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Implement Task status transition in Core only.

CONSTRAINT: Business owns Backlog/To Do/In Progress/Blocked/Completed/Cancelled transition rules; completed records UTC completion timestamp; non-completed state clears/preserves completion timestamp only according to explicit rules; persist + audit/outbox atomically.

RESTRICTION: Do not add controller/UI or reopen special behavior beyond the approved state machine.

USAGE: Follow backend.md.

BEHAVIOR: Add state-machine unit tests and SQL atomicity/concurrency tests. STOP.
```

## Prompt 105 — Add Task status transition API action

```text
REQUIREMENTS:
  TRACEABILITY: TASK-010..016; API-001..007
  REQUIREMENT LINKS: [TASK-010..016](../requirements/lightweight-crm-product-and-system-requirements.md#task-010); [API-001..007](../requirements/lightweight-crm-product-and-system-requirements.md#api-001)
  REQUIREMENT INTENT: Task workflow includes required statuses, assignment/reassignment, priority changes, completion timestamps, explicit reopen, and overdue semantics with auditable mutations. Use consistent REST routes, conventional HTTP verbs/status codes, bounded pagination, OpenAPI documentation, and versionable public contracts.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Add one Task status transition controller action only.

CONSTRAINT: Map domain rejection/conflict/forbidden consistently.

RESTRICTION: Do not add UI.

USAGE: Use add-endpoint skill.

BEHAVIOR: Add API tests for allowed/rejected transitions, completion timestamp exposure, stale version, 401/403. STOP.
```

## Prompt 106 — Implement Task reopen Core behavior

```text
REQUIREMENTS:
  TRACEABILITY: TASK-012; AUDIT-001..008
  REQUIREMENT LINKS: [TASK-012](../requirements/lightweight-crm-product-and-system-requirements.md#task-012); [AUDIT-001..008](../requirements/lightweight-crm-product-and-system-requirements.md#audit-001)
  REQUIREMENT INTENT: Authorized users can reopen completed Tasks and the reopen action must be audited. Every Client/Project/Task mutation produces append-only audit evidence with entity/action/time/actor/source, trace/correlation, and applicable before/after values; secrets are redacted.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Implement explicit reopen-completed-Task use case in Core only.

CONSTRAINT: Authorized reopen returns Task to the approved open state, clears completion timestamp as defined, and writes audit/outbox atomically.

RESTRICTION: Do not reuse a generic status endpoint if requirements/contract call for an explicit reopen operation; do not add controller/UI.

USAGE: Follow backend.md.

BEHAVIOR: Add tests for completed->reopened, non-completed rejection, concurrency and audit. STOP.
```

## Prompt 107 — Add Task reopen API action

```text
REQUIREMENTS:
  TRACEABILITY: TASK-012; API-001..007
  REQUIREMENT LINKS: [TASK-012](../requirements/lightweight-crm-product-and-system-requirements.md#task-012); [API-001..007](../requirements/lightweight-crm-product-and-system-requirements.md#api-001)
  REQUIREMENT INTENT: Authorized users can reopen completed Tasks and the reopen action must be audited. Use consistent REST routes, conventional HTTP verbs/status codes, bounded pagination, OpenAPI documentation, and versionable public contracts.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Add one Task reopen controller action only.

CONSTRAINT: Transport-only; call Facade and map expected errors.

RESTRICTION: Do not add UI.

USAGE: Use add-endpoint skill.

BEHAVIOR: Run focused API tests and STOP.
```

## Prompt 107A — Implement Task details update Core behavior

```text
REQUIREMENTS:
  TRACEABILITY: TASK-002; AUDIT-001..008; DATA-008; SEC-010..013
  REQUIREMENT LINKS: [TASK-002](../requirements/lightweight-crm-product-and-system-requirements.md#task-002); [AUDIT-001..008](../requirements/lightweight-crm-product-and-system-requirements.md#audit-001); [DATA-008](../requirements/lightweight-crm-product-and-system-requirements.md#data-008); [SEC-010..013](../requirements/lightweight-crm-product-and-system-requirements.md#sec-010)
  REQUIREMENT INTENT: Tasks belong to one Project and must support assignment, priority, status/completion/reopen behavior, overdue detection, and filterable task views. Every Client/Project/Task mutation produces append-only audit evidence with entity/action/time/actor/source, trace/correlation, and applicable before/after values; secrets are redacted. Also satisfy the remaining linked cross-cutting constraints that apply to this atomic step.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Implement ordinary Task details editing inside Crm.Core only.

CONSTRAINT: Update title, description, start/due dates and notes as allowed. Assignment, priority, status/completion/reopen remain dedicated operations. Use concurrency protection and atomic audit/outbox persistence.

RESTRICTION: Do not change Project ownership, assignee, priority, status, completed timestamp, controller or UI.

USAGE: Follow backend.md and add-endpoint skill.

BEHAVIOR: Add unit tests and SQL integration tests for editable fields, due-date rules, concurrency and audit change sets; STOP.
```

## Prompt 107B — Add Task details update API action

```text
REQUIREMENTS:
  TRACEABILITY: TASK-002; API-001..007; SEC-012..013
  REQUIREMENT LINKS: [TASK-002](../requirements/lightweight-crm-product-and-system-requirements.md#task-002); [API-001..007](../requirements/lightweight-crm-product-and-system-requirements.md#api-001); [SEC-012..013](../requirements/lightweight-crm-product-and-system-requirements.md#sec-012)
  REQUIREMENT INTENT: Tasks belong to one Project and must support assignment, priority, status/completion/reopen behavior, overdue detection, and filterable task views. Use consistent REST routes, conventional HTTP verbs/status codes, bounded pagination, OpenAPI documentation, and versionable public contracts. Use ASP.NET Core Identity and server-side role/policy authorization with least privilege; protect APIs and never expose passwords, tokens, secrets, or sensitive internals.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Add one Task details update HTTP contract/action only.

CONSTRAINT: Require approved concurrency token/version; controller calls only Facade and maps standard ProblemDetails.

RESTRICTION: Do not combine assignment/priority/status/reopen mutations or add UI.

USAGE: Use add-endpoint skill.

BEHAVIOR: Add API tests for success, invalid dates, stale version, 401/403 and STOP.
```

---

# Part 6 — Identity, authorization, and account security

## Prompt 108 — Create Identity service projects

```text
REQUIREMENTS:
  TRACEABILITY: SEC-001..016
  REQUIREMENT LINKS: [SEC-001..016](../requirements/lightweight-crm-product-and-system-requirements.md#sec-001)
  REQUIREMENT INTENT: Use ASP.NET Core Identity and server-side role/policy authorization with least privilege; protect APIs and never expose passwords, tokens, secrets, or sensitive internals.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Create the approved `ProjectChicago.Identity`, `.Core`, and `.Functions` projects and only their required project references.

CONSTRAINT: Use the same three-project shape even if Functions initially has no triggers. Identity owns exactly one SQL database.

RESTRICTION: Do not add Identity schema, auth endpoints, Service Bus triggers, or user model.

USAGE: Follow backend.md/functions.md/identity.md.

BEHAVIOR: Add projects to solution, build all three and verify no reference to Crm.Core/Audit.Core. STOP.
```

## Prompt 109 — Create Identity test projects

```text
REQUIREMENTS:
  TRACEABILITY: TEST-001..007; SEC-001..016
  REQUIREMENT LINKS: [TEST-001..007](../requirements/lightweight-crm-product-and-system-requirements.md#test-001); [SEC-001..016](../requirements/lightweight-crm-product-and-system-requirements.md#sec-001)
  REQUIREMENT INTENT: Automated tests cover business rules, authorization, APIs, SQL-compatible persistence, message consumers, audit generation, and representative distributed tracing. Use ASP.NET Core Identity and server-side role/policy authorization with least privilege; protect APIs and never expose passwords, tokens, secrets, or sensitive internals.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Create Identity Core, API and Functions test projects only.

CONSTRAINT: Use repository test framework and correct references.

RESTRICTION: Do not add tests/fixtures yet.

USAGE: Use dotnet CLI.

BEHAVIOR: Build all test projects and STOP.
```

## Prompt 110 — Create IdentityDbContext and application user

```text
REQUIREMENTS:
  TRACEABILITY: SEC-001..009; DATA-031
  REQUIREMENT LINKS: [SEC-001..009](../requirements/lightweight-crm-product-and-system-requirements.md#sec-001); [DATA-031](../requirements/lightweight-crm-product-and-system-requirements.md#data-031)
  REQUIREMENT INTENT: Authentication/account security uses ASP.NET Core Identity for account and password operations, and authentication events are audited without logging credentials. Use SQL Server/Azure SQL with service-owned databases, enforced relationships, UTC timestamps, safe public IDs, optimistic concurrency, and archival over routine hard deletion.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Create the ASP.NET Core Identity DbContext/application user model and register ASP.NET Core Identity framework services inside Identity.Core/host as appropriate.

CONSTRAINT: Use supported ASP.NET Core Identity APIs and Guid/non-enumerable public identifiers as approved; Identity tables remain in IdentityDb only.

RESTRICTION: Do not create login/register endpoints, roles, migration, custom password hashing, or token/session code.

USAGE: Follow identity.md/backend.md and current official ASP.NET Core Identity guidance.

BEHAVIOR: Add focused model/DI tests proving UserManager/RoleManager stores resolve against IdentityDbContext. STOP.
```

## Prompt 110A — Add Shared Outbox/Inbox mappings to IdentityDbContext

```text
REQUIREMENTS:
  TRACEABILITY: SEC-005; AUDIT-001..008; OUTBOX-001..006; ASYNC-005
  REQUIREMENT LINKS: [SEC-005](../requirements/lightweight-crm-product-and-system-requirements.md#sec-005); [AUDIT-001..008](../requirements/lightweight-crm-product-and-system-requirements.md#audit-001); [OUTBOX-001..006](../requirements/lightweight-crm-product-and-system-requirements.md#outbox-001); [ASYNC-005](../requirements/lightweight-crm-product-and-system-requirements.md#async-005)
  REQUIREMENT INTENT: Use ASP.NET Core Identity and server-side role/policy authorization with least privilege; protect APIs and never expose passwords, tokens, secrets, or sensitive internals. Every Client/Project/Task mutation produces append-only audit evidence with entity/action/time/actor/source, trace/correlation, and applicable before/after values; secrets are redacted. Also satisfy the remaining linked cross-cutting constraints that apply to this atomic step.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Add the Shared OutboxMessage and InboxMessage mappings to IdentityDbContext only.

CONSTRAINT: Identity security/account mutations and auditable authentication events use IdentityDb's own transactional outbox; inbox is available only for future approved Identity consumers. Preserve ASP.NET Core Identity schema ownership.

RESTRICTION: Do not add Service Bus publishing, timer Functions, audit event generation, migration, or auth endpoints.

USAGE: Follow identity.md, database.md and messaging.md.

BEHAVIOR: Add model tests proving Identity tables + Outbox/Inbox map in IdentityDbContext and no CRM/Audit domain tables appear; STOP.
```

## Prompt 111 — Register IdentityDb in AppHost and wire Identity projects

```text
REQUIREMENTS:
  TRACEABILITY: SEC-001..016; DATA-031..034; OTEL-001..006
  REQUIREMENT LINKS: [SEC-001..016](../requirements/lightweight-crm-product-and-system-requirements.md#sec-001); [DATA-031..034](../requirements/lightweight-crm-product-and-system-requirements.md#data-031); [OTEL-001..006](../requirements/lightweight-crm-product-and-system-requirements.md#otel-001)
  REQUIREMENT INTENT: Use ASP.NET Core Identity and server-side role/policy authorization with least privilege; protect APIs and never expose passwords, tokens, secrets, or sensitive internals. Use SQL Server/Azure SQL with service-owned databases, enforced relationships, UTC timestamps, safe public IDs, optimistic concurrency, and archival over routine hard deletion. Every API/service/Function uses OpenTelemetry for traces, metrics, and log correlation, including dependency instrumentation and meaningful business spans where needed.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Add IdentityDb under the existing SQL Server resource, wire Identity host to ServiceDefaults+IdentityDb, wire Identity.Functions only to resources it needs, and register the host/Functions projects in AppHost.

CONSTRAINT: Preserve one DB per service and least privilege.

RESTRICTION: Do not give the Identity HTTP host Service Bus credentials; only Identity.Functions receives the send capability required by its outbox relay. Do not add routes or triggers yet.

USAGE: Use add-aspire-resource skill.

BEHAVIOR: Build AppHost and inspect dependency graph. STOP.
```

## Prompt 111A — Add the Identity outbox timer trigger

```text
REQUIREMENTS:
  TRACEABILITY: SEC-005; OUTBOX-003..006; ASYNC-001..008
  REQUIREMENT LINKS: [SEC-005](../requirements/lightweight-crm-product-and-system-requirements.md#sec-005); [OUTBOX-003..006](../requirements/lightweight-crm-product-and-system-requirements.md#outbox-003); [ASYNC-001..008](../requirements/lightweight-crm-product-and-system-requirements.md#async-001)
  REQUIREMENT INTENT: Use ASP.NET Core Identity and server-side role/policy authorization with least privilege; protect APIs and never expose passwords, tokens, secrets, or sensitive internals. Commit state and integration events atomically through a transactional outbox, then relay them with a timer-triggered Function and observable retry/backlog behavior. Durable async work uses Azure Service Bus and Service Bus-triggered Functions with trace correlation, duplicate tolerance/idempotency, bounded retry behavior, and dead-letter visibility.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Add one timer-triggered Function in Identity.Functions that delegates to the shared IOutboxRelay for IdentityDb.

CONSTRAINT: Configuration-controlled schedule, isolated worker, no polling SQL/event logic in Function, no HTTP trigger, failures remain visible.

RESTRICTION: Do not add Service Bus consumer triggers, BackgroundService/IHostedService, auth business logic, or direct ServiceBusSender calls.

USAGE: Use add-function-trigger skill and functions.md/messaging.md.

BEHAVIOR: Add Function adapter tests for relay delegation, cancellation and exception propagation; run focused tests and STOP.
```

## Prompt 112 — Generate and apply initial Identity migration locally

```text
REQUIREMENTS:
  TRACEABILITY: SEC-001..009; DATA-030..034
  REQUIREMENT LINKS: [SEC-001..009](../requirements/lightweight-crm-product-and-system-requirements.md#sec-001); [DATA-030..034](../requirements/lightweight-crm-product-and-system-requirements.md#data-030)
  REQUIREMENT INTENT: Authentication/account security uses ASP.NET Core Identity for account and password operations, and authentication events are audited without logging credentials. Use Microsoft SQL Server/Azure SQL, one database per bounded service, no cross-service database queries, and controlled schema migrations.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Generate then apply the initial Identity migration to local IdentityDb as one schema-establishment operation.

CONSTRAINT: Review generated SQL intent before applying; use SQL Server provider; only Identity/outbox/inbox tables approved for Identity service.

RESTRICTION: Do not hand-edit migration or touch CrmDb/AuditDb.

USAGE: Follow database.md and approved migration convention.

BEHAVIOR: Verify applied migration and Identity tables in local SQL metadata. STOP.
```

## Prompt 113 — Seed application roles

```text
REQUIREMENTS:
  TRACEABILITY: SEC-010..016
  REQUIREMENT LINKS: [SEC-010..016](../requirements/lightweight-crm-product-and-system-requirements.md#sec-010)
  REQUIREMENT INTENT: Authorization is server-side with roles/claims/policies and least privilege; protected reads/mutations require explicit authorization and system work uses service identities.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Add deterministic role seeding for Administrator, Manager, Contributor and ReadOnly only.

CONSTRAINT: Use RoleManager/Identity APIs; seed idempotently.

RESTRICTION: Do not create users, permissions beyond role names, or authorization policies yet.

USAGE: Follow identity.md.

BEHAVIOR: Add integration test proving repeated seed does not duplicate roles. STOP.
```

## Prompt 114 — Implement Identity login/logout/current-user endpoints

```text
REQUIREMENTS:
  TRACEABILITY: SEC-001..009; SEC-020..025
  REQUIREMENT LINKS: [SEC-001..009](../requirements/lightweight-crm-product-and-system-requirements.md#sec-001); [SEC-020..025](../requirements/lightweight-crm-product-and-system-requirements.md#sec-020)
  REQUIREMENT INTENT: Authentication/account security uses ASP.NET Core Identity for account and password operations, and authentication events are audited without logging credentials.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Implement the smallest authentication surface required by the approved auth ADR: login, logout and current-user/session inspection.

CONSTRAINT: Use ASP.NET Core Identity managers/sign-in APIs and the approved transport/session strategy; log auth events safely; no credential material in logs.

RESTRICTION: Do not implement user administration, password recovery, MFA, external providers, or CRM authorization.

USAGE: Follow identity.md, add-endpoint skill and approved auth ADR.

BEHAVIOR: Add API integration tests for successful login, failed login, lockout behavior if enabled, logout invalidation, current-user 200 and unauthenticated 401. STOP.
```

## Prompt 114A — Emit auditable authentication/security events through Identity outbox

```text
REQUIREMENTS:
  TRACEABILITY: SEC-005; AUDIT-001..008; OUTBOX-001..006; TRACE-003..007
  REQUIREMENT LINKS: [SEC-005](../requirements/lightweight-crm-product-and-system-requirements.md#sec-005); [AUDIT-001..008](../requirements/lightweight-crm-product-and-system-requirements.md#audit-001); [OUTBOX-001..006](../requirements/lightweight-crm-product-and-system-requirements.md#outbox-001); [TRACE-003..007](../requirements/lightweight-crm-product-and-system-requirements.md#trace-003)
  REQUIREMENT INTENT: Use ASP.NET Core Identity and server-side role/policy authorization with least privilege; protect APIs and never expose passwords, tokens, secrets, or sensitive internals. Every Client/Project/Task mutation produces append-only audit evidence with entity/action/time/actor/source, trace/correlation, and applicable before/after values; secrets are redacted. Also satisfy the remaining linked cross-cutting constraints that apply to this atomic step.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Add the approved business-audit event generation to successful login, failed login, lockout, logout and other already-implemented authentication security events only.

CONSTRAINT: Use IdentityDb's outbox and the approved audit event contract; capture actor/user identifier when safely known, event action, occurred-at UTC and correlation/trace metadata. Never record passwords, tokens, password hashes or credential secrets. A failed login audit fact may require its own small IdentityDb transaction because the credential operation itself failed.

RESTRICTION: Do not add new authentication features, direct writes to AuditDb, or direct Service Bus sends from the HTTP request path.

USAGE: Follow add-audit-event, identity.md and messaging.md.

BEHAVIOR: Add tests proving each security event writes exactly one safe audit outbox row and no credential material is serialized; STOP.
```

## Prompt 114B — Implement administrator user creation and role assignment

```text
REQUIREMENTS:
  TRACEABILITY: SEC-004; SEC-010..016; primary-user Administrator requirements
  REQUIREMENT LINKS: [SEC-004](../requirements/lightweight-crm-product-and-system-requirements.md#sec-004); [SEC-010..016](../requirements/lightweight-crm-product-and-system-requirements.md#sec-010); [Administrator requirements](../requirements/lightweight-crm-product-and-system-requirements.md#31-administrator)
  REQUIREMENT INTENT: Use ASP.NET Core Identity and server-side role/policy authorization with least privilege; protect APIs and never expose passwords, tokens, secrets, or sensitive internals. Administrators can manage application users, roles, and permissions, with server-side authorization and auditable changes.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Implement the Administrator-only user creation and initial role assignment use case through Identity Controller -> Facade -> Business/Data/Identity framework APIs.

CONSTRAINT: Use UserManager/RoleManager; honor approved password/account-confirmation policy; write required security audit event through Identity outbox; never expose password hashes or reset tokens in normal responses.

RESTRICTION: Do not add activation/deactivation, password reset/change, bulk import, UI or custom credential code.

USAGE: Follow identity.md, add-endpoint and add-audit-event skills.

BEHAVIOR: Add API tests for authorized create, duplicate user, invalid password policy, invalid role, 401/403 and audit-outbox creation; STOP.
```

## Prompt 114C — Implement account activation and deactivation

```text
REQUIREMENTS:
  TRACEABILITY: SEC-004; SEC-005; SEC-010..016
  REQUIREMENT LINKS: [SEC-004](../requirements/lightweight-crm-product-and-system-requirements.md#sec-004); [SEC-005](../requirements/lightweight-crm-product-and-system-requirements.md#sec-005); [SEC-010..016](../requirements/lightweight-crm-product-and-system-requirements.md#sec-010)
  REQUIREMENT INTENT: Use ASP.NET Core Identity and server-side role/policy authorization with least privilege; protect APIs and never expose passwords, tokens, secrets, or sensitive internals.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Implement Administrator-only account activation/deactivation only.

CONSTRAINT: Use supported ASP.NET Core Identity state/security-stamp/lockout mechanisms selected by the auth ADR; deactivation must prevent future authenticated use and invalidate/revoke active session behavior as approved; write an audit event.

RESTRICTION: Do not delete users, change roles, reset passwords, or add UI.

USAGE: Follow identity.md and approved auth ADR.

BEHAVIOR: Add tests for deactivate prevents access, activate restores eligibility, session invalidation behavior, 401/403 and audit event; STOP.
```

## Prompt 114D — Implement role management for existing users

```text
REQUIREMENTS:
  TRACEABILITY: SEC-010..016; Administrator user-management requirements
  REQUIREMENT LINKS: [SEC-010..016](../requirements/lightweight-crm-product-and-system-requirements.md#sec-010); [Administrator requirements](../requirements/lightweight-crm-product-and-system-requirements.md#31-administrator)
  REQUIREMENT INTENT: Authorization is server-side with roles/claims/policies and least privilege; protected reads/mutations require explicit authorization and system work uses service identities. Administrators can manage application users, roles, and permissions, with server-side authorization and auditable changes.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Implement Administrator-only add/remove role operations for existing users only.

CONSTRAINT: Use RoleManager/UserManager; restrict roles to the approved role catalog; prevent an invalid zero-admin condition if the approved security policy requires it; emit audit event with changed role names only.

RESTRICTION: Do not create/delete users, define new roles, or add UI.

USAGE: Follow identity.md and add-endpoint skill.

BEHAVIOR: Add API tests for add/remove, invalid role, protected last-admin rule if applicable, 401/403 and audit-outbox creation; STOP.
```

## Prompt 114E — Implement authenticated password change

```text
REQUIREMENTS:
  TRACEABILITY: SEC-004; SEC-005
  REQUIREMENT LINKS: [SEC-004](../requirements/lightweight-crm-product-and-system-requirements.md#sec-004); [SEC-005](../requirements/lightweight-crm-product-and-system-requirements.md#sec-005)
  REQUIREMENT INTENT: Use ASP.NET Core Identity and server-side role/policy authorization with least privilege; protect APIs and never expose passwords, tokens, secrets, or sensitive internals.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Implement the authenticated current-user password change operation only.

CONSTRAINT: Use supported ASP.NET Core Identity ChangePassword APIs and approved session/security-stamp behavior; never log or audit password values; audit only the fact that a password change occurred.

RESTRICTION: Do not implement forgotten-password reset, admin reset, recovery delivery, or UI.

USAGE: Follow identity.md and approved auth ADR.

BEHAVIOR: Add tests for correct current password, wrong current password, password policy rejection, post-change session behavior and safe audit event; STOP.
```

## Prompt 114F — Implement approved password reset/recovery flow

```text
REQUIREMENTS:
  TRACEABILITY: SEC-004; SEC-005
  REQUIREMENT LINKS: [SEC-004](../requirements/lightweight-crm-product-and-system-requirements.md#sec-004); [SEC-005](../requirements/lightweight-crm-product-and-system-requirements.md#sec-005)
  REQUIREMENT INTENT: Use ASP.NET Core Identity and server-side role/policy authorization with least privilege; protect APIs and never expose passwords, tokens, secrets, or sensitive internals.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Implement only the password reset/recovery flow defined by the approved authentication ADR.

CONSTRAINT: Use ASP.NET Core Identity reset token providers and the approved secure delivery/administrative recovery policy. Tokens are time-bound/one-purpose according to framework policy and never logged or written into business audit payloads. Audit request/completion facts without the token.

RESTRICTION: Do not invent email/SMS infrastructure if the ADR chose a different recovery channel; do not return production reset tokens to arbitrary callers; do not add MFA/passkeys.

USAGE: Follow identity.md and current official ASP.NET Core Identity recovery guidance.

BEHAVIOR: Add integration tests for valid reset, invalid/expired/reused token, unknown-user non-enumeration behavior, resulting login behavior and safe audit events; STOP.
```

## Prompt 115 — Add Identity routes through YARP

```text
REQUIREMENTS:
  TRACEABILITY: SEC-020..025; API-001..007
  REQUIREMENT LINKS: [SEC-020..025](../requirements/lightweight-crm-product-and-system-requirements.md#sec-020); [API-001..007](../requirements/lightweight-crm-product-and-system-requirements.md#api-001)
  REQUIREMENT INTENT: Public API access goes through the Project Chicago gateway over HTTPS with validated inputs and safe logging that excludes credentials, tokens, secrets, and unnecessary PII. Use consistent REST routes, conventional HTTP verbs/status codes, bounded pagination, OpenAPI documentation, and versionable public contracts.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Add only the approved public authentication/account route prefix from Gateway to Identity host.

CONSTRAINT: Preserve correlation/security headers and approved cookie/token behavior.

RESTRICTION: Do not add CRM routes or move auth logic into Gateway.

USAGE: Follow gateway.md/identity.md.

BEHAVIOR: Add gateway routing/auth transport test and STOP.
```

## Prompt 116 — Add CRM authorization policies

```text
REQUIREMENTS:
  TRACEABILITY: SEC-010..016
  REQUIREMENT LINKS: [SEC-010..016](../requirements/lightweight-crm-product-and-system-requirements.md#sec-010)
  REQUIREMENT INTENT: Authorization is server-side with roles/claims/policies and least privilege; protected reads/mutations require explicit authorization and system work uses service identities.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Define and register Crm service authorization policies for Administrator, Manager, Contributor and ReadOnly behaviors required by the product roles.

CONSTRAINT: Authorization is enforced server-side in Crm; policies use trusted authenticated context from the approved auth architecture.

RESTRICTION: Do not change Identity role storage, controller business logic, or React visibility yet.

USAGE: Follow identity.md/backend.md and the role requirements.

BEHAVIOR: Add policy unit/integration tests covering allowed/denied representative actions and 401 vs 403. STOP.
```

## Prompt 117 — Apply authorization policies to CRM endpoints

```text
REQUIREMENTS:
  TRACEABILITY: SEC-010..016; SEARCH-004; DASH-003
  REQUIREMENT LINKS: [SEC-010..016](../requirements/lightweight-crm-product-and-system-requirements.md#sec-010); [SEARCH-004](../requirements/lightweight-crm-product-and-system-requirements.md#search-004); [DASH-003](../requirements/lightweight-crm-product-and-system-requirements.md#dash-003)
  REQUIREMENT INTENT: Authorization is server-side with roles/claims/policies and least privilege; protected reads/mutations require explicit authorization and system work uses service identities. Global search must find Clients, Projects, and Tasks, identify result type, and never reveal unauthorized data. The dashboard must show the required CRM summaries only within the current user's authorization scope.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Apply the already-defined authorization policies to existing Client/Project/Task endpoints only.

CONSTRAINT: Use the narrowest policy per action; preserve resource-level checks in Facades.

RESTRICTION: Do not create new endpoints or React changes.

USAGE: Follow backend.md/identity.md.

BEHAVIOR: Run full Crm API authorization tests and prove ReadOnly cannot mutate while authorized readers can query. STOP.
```

## Prompt 117A — Add Administrator user-management API coverage

```text
REQUIREMENTS:
  TRACEABILITY: Administrator user-management requirements; SEC-004; SEC-010..016
  REQUIREMENT LINKS: [Administrator requirements](../requirements/lightweight-crm-product-and-system-requirements.md#31-administrator); [SEC-004](../requirements/lightweight-crm-product-and-system-requirements.md#sec-004); [SEC-010..016](../requirements/lightweight-crm-product-and-system-requirements.md#sec-010)
  REQUIREMENT INTENT: Administrators can manage application users, roles, and permissions, with server-side authorization and auditable changes. Use ASP.NET Core Identity and server-side role/policy authorization with least privilege; protect APIs and never expose passwords, tokens, secrets, or sensitive internals.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Add/read the minimal Administrator user listing/detail endpoints needed to manage existing application users and roles.

CONSTRAINT: Responses expose only support-safe account metadata and roles; pagination required; authorization is Administrator-only; all writes remain in the dedicated operations already implemented.

RESTRICTION: Do not expose password/security token data, direct Identity EF entities, or CRM data.

USAGE: Follow identity.md and add-endpoint skill.

BEHAVIOR: Add API tests for list/detail pagination, role display, 401/403 and sensitive-field absence; STOP.
```

---

# Part 7 — Durable Audit bounded service

## Prompt 118 — Create Audit service projects

```text
REQUIREMENTS:
  TRACEABILITY: AUDIT-001..008
  REQUIREMENT LINKS: [AUDIT-001..008](../requirements/lightweight-crm-product-and-system-requirements.md#audit-001)
  REQUIREMENT INTENT: Every Client/Project/Task mutation produces append-only audit evidence with entity/action/time/actor/source, trace/correlation, and applicable before/after values; secrets are redacted.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Create `ProjectChicago.Audit`, `.Core`, `.Functions` and their required project references only.

CONSTRAINT: Audit is its own bounded context/database; Functions handles Service Bus ingestion; API is read-only support/query surface.

RESTRICTION: Do not create audit entity, DB, triggers, routes, or business logic.

USAGE: Follow audit.md/functions.md/backend.md.

BEHAVIOR: Add to solution, build and verify no reference to Crm.Core/Identity.Core. STOP.
```

## Prompt 119 — Create Audit test projects

```text
REQUIREMENTS:
  TRACEABILITY: TEST-001..007; AUDIT-001..008
  REQUIREMENT LINKS: [TEST-001..007](../requirements/lightweight-crm-product-and-system-requirements.md#test-001); [AUDIT-001..008](../requirements/lightweight-crm-product-and-system-requirements.md#audit-001)
  REQUIREMENT INTENT: Automated tests cover business rules, authorization, APIs, SQL-compatible persistence, message consumers, audit generation, and representative distributed tracing. Every Client/Project/Task mutation produces append-only audit evidence with entity/action/time/actor/source, trace/correlation, and applicable before/after values; secrets are redacted.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Create Audit Core, API and Functions test projects only.

CONSTRAINT: Use repository test framework.

RESTRICTION: Do not add tests/fixtures.

USAGE: Use dotnet CLI.

BEHAVIOR: Build and STOP.
```

## Prompt 120 — Create AuditEntry entity and EF configuration

```text
REQUIREMENTS:
  TRACEABILITY: AUDIT-001..008; PRIV-001..005
  REQUIREMENT LINKS: [AUDIT-001..008](../requirements/lightweight-crm-product-and-system-requirements.md#audit-001); [PRIV-001..005](../requirements/lightweight-crm-product-and-system-requirements.md#priv-001)
  REQUIREMENT INTENT: Every Client/Project/Task mutation produces append-only audit evidence with entity/action/time/actor/source, trace/correlation, and applicable before/after values; secrets are redacted. Collect only necessary CRM data, minimize sensitive duplication and PII in telemetry, enforce authorization, and document retention before production.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Create the append-only AuditEntry model and SQL Server EF configuration only.

CONSTRAINT: Persist audit event ID, entity type/ID, action, occurred/recorded UTC, actor ID/type, owning service, TraceId, CorrelationId, CausationId, changed-field metadata and safe previous/new values/payload representation according to ADR. Include uniqueness on event ID and indexes for entity/time/trace/correlation.

RESTRICTION: Do not add DbContext, repository, API or Service Bus trigger. Never store credentials/tokens.

USAGE: Follow audit.md/database.md.

BEHAVIOR: Add model/config tests proving append-only shape, indexes and SQL Server compatibility. STOP.
```

## Prompt 121 — Create AuditDbContext with Inbox and AuditEntry

```text
REQUIREMENTS:
  TRACEABILITY: AUDIT-001..008; ASYNC-005
  REQUIREMENT LINKS: [AUDIT-001..008](../requirements/lightweight-crm-product-and-system-requirements.md#audit-001); [ASYNC-005](../requirements/lightweight-crm-product-and-system-requirements.md#async-005)
  REQUIREMENT INTENT: Every Client/Project/Task mutation produces append-only audit evidence with entity/action/time/actor/source, trace/correlation, and applicable before/after values; secrets are redacted. Use Azure Service Bus and Azure Functions for durable async work with idempotent/duplicate-tolerant consumers, bounded retries, and dead-letter visibility.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Create AuditDbContext with AuditEntry plus Shared InboxMessage mapping only.

CONSTRAINT: Audit is a consumer; include Outbox only if an explicit approved Audit-publishing use case exists.

RESTRICTION: Do not add consumer logic or migration.

USAGE: Follow audit.md/database.md.

BEHAVIOR: Add model tests and build Audit.Core. STOP.
```

## Prompt 122 — Register AuditDb and wire Audit projects in AppHost

```text
REQUIREMENTS:
  TRACEABILITY: AUDIT-001..008; ASYNC-001..008; OTEL-001..006
  REQUIREMENT LINKS: [AUDIT-001..008](../requirements/lightweight-crm-product-and-system-requirements.md#audit-001); [ASYNC-001..008](../requirements/lightweight-crm-product-and-system-requirements.md#async-001); [OTEL-001..006](../requirements/lightweight-crm-product-and-system-requirements.md#otel-001)
  REQUIREMENT INTENT: Every Client/Project/Task mutation produces append-only audit evidence with entity/action/time/actor/source, trace/correlation, and applicable before/after values; secrets are redacted. Durable async work uses Azure Service Bus and Service Bus-triggered Functions with trace correlation, duplicate tolerance/idempotency, bounded retry behavior, and dead-letter visibility. Every API/service/Function uses OpenTelemetry for traces, metrics, and log correlation, including dependency instrumentation and meaningful business spans where needed.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Add AuditDb under SQL Server; wire Audit host to AuditDb+ServiceDefaults; wire Audit.Functions to AuditDb+Service Bus+ServiceDefaults; register both projects.

CONSTRAINT: Use least-privilege Service Bus receive permissions/configuration from topology ADR.

RESTRICTION: Do not add Function trigger or gateway route.

USAGE: Use add-aspire-resource skill.

BEHAVIOR: Build AppHost and inspect resource references. STOP.
```

## Prompt 123 — Generate and apply initial Audit migration locally

```text
REQUIREMENTS:
  TRACEABILITY: AUDIT-001..008; DATA-030..034
  REQUIREMENT LINKS: [AUDIT-001..008](../requirements/lightweight-crm-product-and-system-requirements.md#audit-001); [DATA-030..034](../requirements/lightweight-crm-product-and-system-requirements.md#data-030)
  REQUIREMENT INTENT: Every Client/Project/Task mutation produces append-only audit evidence with entity/action/time/actor/source, trace/correlation, and applicable before/after values; secrets are redacted. Use Microsoft SQL Server/Azure SQL, one database per bounded service, no cross-service database queries, and controlled schema migrations.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Generate, review and apply the initial AuditDb migration to local AuditDb.

CONSTRAINT: Only AuditEntry and Inbox tables plus required indexes/constraints.

RESTRICTION: Do not hand-edit migration or touch other service DBs.

USAGE: Follow database.md.

BEHAVIOR: Verify applied migration and table/index metadata. STOP.
```

## Prompt 124 — Implement Audit append repository/Data path

```text
REQUIREMENTS:
  TRACEABILITY: AUDIT-001..008; ASYNC-005..008
  REQUIREMENT LINKS: [AUDIT-001..008](../requirements/lightweight-crm-product-and-system-requirements.md#audit-001); [ASYNC-005..008](../requirements/lightweight-crm-product-and-system-requirements.md#async-005)
  REQUIREMENT INTENT: Every Client/Project/Task mutation produces append-only audit evidence with entity/action/time/actor/source, trace/correlation, and applicable before/after values; secrets are redacted. Use Azure Service Bus and Azure Functions for durable async work with idempotent/duplicate-tolerant consumers, bounded retries, and dead-letter visibility.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Implement only the Audit repository and Data transaction that idempotently appends an AuditEntry and marks the Inbox message complete.

CONSTRAINT: First delivery appends once; duplicate completed delivery no-ops; failure must not complete inbox; append is not updateable through normal path.

RESTRICTION: Do not add Function trigger, Facade/controller or direct calls back to publisher.

USAGE: Follow audit.md/messaging.md.

BEHAVIOR: Add SQL integration tests for first delivery, duplicate, failure rollback and immutable-update absence. STOP.
```

## Prompt 125 — Implement Audit Facade/Business ingestion

```text
REQUIREMENTS:
  TRACEABILITY: AUDIT-001..008; PRIV-001..005
  REQUIREMENT LINKS: [AUDIT-001..008](../requirements/lightweight-crm-product-and-system-requirements.md#audit-001); [PRIV-001..005](../requirements/lightweight-crm-product-and-system-requirements.md#priv-001)
  REQUIREMENT INTENT: Every Client/Project/Task mutation produces append-only audit evidence with entity/action/time/actor/source, trace/correlation, and applicable before/after values; secrets are redacted. Collect only necessary CRM data, minimize sensitive duplication and PII in telemetry, enforce authorization, and document retention before production.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Implement Audit Core ingestion translation/validation only.

CONSTRAINT: Validate approved event/envelope version, redact/limit sensitive data, map actor/entity/change metadata and delegate to Data.

RESTRICTION: Do not read CrmDb/IdentityDb, add Function trigger or query endpoint.

USAGE: Follow add-audit-event skill.

BEHAVIOR: Add unit tests for supported event, redaction, malformed/unsupported event and duplicate result mapping. STOP.
```

## Prompt 126 — Add Audit Service Bus trigger

```text
REQUIREMENTS:
  TRACEABILITY: AUDIT-001..008; ASYNC-001..008; TRACE-003..007
  REQUIREMENT LINKS: [AUDIT-001..008](../requirements/lightweight-crm-product-and-system-requirements.md#audit-001); [ASYNC-001..008](../requirements/lightweight-crm-product-and-system-requirements.md#async-001); [TRACE-003..007](../requirements/lightweight-crm-product-and-system-requirements.md#trace-003)
  REQUIREMENT INTENT: Every Client/Project/Task mutation produces append-only audit evidence with entity/action/time/actor/source, trace/correlation, and applicable before/after values; secrets are redacted. Durable async work uses Azure Service Bus and Service Bus-triggered Functions with trace correlation, duplicate tolerance/idempotency, bounded retry behavior, and dead-letter visibility. Propagate W3C distributed trace context across gateway, APIs, SQL, Service Bus, and Functions so an operation can be followed cradle to grave.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Add the Audit.Functions Service Bus trigger for the approved audit-event subscription only.

CONSTRAINT: Trigger binds/deserializes envelope, establishes trace/correlation/causation context, calls Audit Facade, and fails invocation on unexpected/transient failure.

RESTRICTION: Do not contain audit mapping/persistence logic or catch-and-return-success.

USAGE: Use add-function-trigger and add-audit-event skills.

BEHAVIOR: Add Function tests for valid delegation, invalid contract policy, exception propagation and correlation propagation. STOP.
```

## Prompt 127 — Implement Audit query Core path

```text
REQUIREMENTS:
  TRACEABILITY: AUDIT-001..008; ACTIVITY-001..003
  REQUIREMENT LINKS: [AUDIT-001..008](../requirements/lightweight-crm-product-and-system-requirements.md#audit-001); [ACTIVITY-001..003](../requirements/lightweight-crm-product-and-system-requirements.md#activity-001)
  REQUIREMENT INTENT: Every Client/Project/Task mutation produces append-only audit evidence with entity/action/time/actor/source, trace/correlation, and applicable before/after values; secrets are redacted. Recent activity is derived from significant audit events and shown in user-friendly form with underlying audit links where authorized.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Implement read-only Audit query by entity and by Trace/Correlation ID through Audit Repository/Data/Business/Facade.

CONSTRAINT: Return ordered append-only entries with pagination and support-safe fields only.

RESTRICTION: Do not query other service DBs or add controller yet.

USAGE: Follow audit.md/backend.md.

BEHAVIOR: Add SQL integration tests for ordering, pagination, entity filter and trace/correlation lookup. STOP.
```

## Prompt 128 — Add Audit query API endpoints and YARP route

```text
REQUIREMENTS:
  TRACEABILITY: AUDIT-001..008; ACTIVITY-001..003; SEC-012
  REQUIREMENT LINKS: [AUDIT-001..008](../requirements/lightweight-crm-product-and-system-requirements.md#audit-001); [ACTIVITY-001..003](../requirements/lightweight-crm-product-and-system-requirements.md#activity-001); [SEC-012](../requirements/lightweight-crm-product-and-system-requirements.md#sec-012)
  REQUIREMENT INTENT: Every Client/Project/Task mutation produces append-only audit evidence with entity/action/time/actor/source, trace/correlation, and applicable before/after values; secrets are redacted. Recent activity is derived from significant audit events and shown in user-friendly form with underlying audit links where authorized. Use ASP.NET Core Identity and server-side role/policy authorization with least privilege; protect APIs and never expose passwords, tokens, secrets, or sensitive internals.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Add the minimal read-only Audit API actions and stable Gateway route required for authorized support/audit viewing.

CONSTRAINT: Restrict to approved privileged roles; controllers call only Audit Facade; gateway route uses service discovery.

RESTRICTION: Do not add audit mutation endpoints or direct DB access from Crm.

USAGE: Use add-endpoint skill and gateway.md.

BEHAVIOR: Add API/gateway tests for authorized query, pagination, 401, 403 and absence of mutation routes. STOP.
```

## Prompt 128A — Register standard ProblemDetails/exception handling in Identity and Audit hosts

```text
REQUIREMENTS:
  TRACEABILITY: ERROR-001..005; TRACE-001..007; LOG-001..006
  REQUIREMENT LINKS: [ERROR-001..005](../requirements/lightweight-crm-product-and-system-requirements.md#error-001); [TRACE-001..007](../requirements/lightweight-crm-product-and-system-requirements.md#trace-001); [LOG-001..006](../requirements/lightweight-crm-product-and-system-requirements.md#log-001)
  REQUIREMENT INTENT: Return safe errors that distinguish validation/auth/authz/not-found/concurrency/internal failures and provide a trace/support reference without exposing internals. Every inbound request participates in a trace propagated through gateway, services, SQL, HTTP, Service Bus, Functions, and downstream work with safe diagnostic metadata. Use structured trace-correlated logs without sensitive payload leakage or duplicate exception logging at every layer.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Register the shared ProblemDetails/exception handling and request-context plumbing in the Identity and Audit HTTP hosts only.

CONSTRAINT: Preserve safe trace/support references and typed expected errors; unexpected exceptions remain observable without leaking internals.

RESTRICTION: Do not change business logic, auth rules, routes or public contracts beyond standard error metadata.

USAGE: Follow backend.md and the approved observability/auth ADRs.

BEHAVIOR: Add host integration tests for error redaction and trace-reference propagation in each host; STOP.
```

## Prompt 128B — Configure OpenAPI for all public service APIs

```text
REQUIREMENTS:
  TRACEABILITY: API-006..007; ERROR-001..005; SEC-012
  REQUIREMENT LINKS: [API-006..007](../requirements/lightweight-crm-product-and-system-requirements.md#api-006); [ERROR-001..005](../requirements/lightweight-crm-product-and-system-requirements.md#error-001); [SEC-012](../requirements/lightweight-crm-product-and-system-requirements.md#sec-012)
  REQUIREMENT INTENT: Expose consistent REST-oriented, documented, versionable APIs using conventional HTTP verbs/status codes and bounded pagination for collections. Return safe errors that distinguish validation/auth/authz/not-found/concurrency/internal failures and provide a trace/support reference without exposing internals. Use ASP.NET Core Identity and server-side role/policy authorization with least privilege; protect APIs and never expose passwords, tokens, secrets, or sensitive internals.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Configure OpenAPI generation for Crm, Identity and Audit HTTP hosts only.

CONSTRAINT: Document stable operation IDs, public schemas, ProblemDetails, auth requirements and versioning conventions. Keep internal .Core/Data/EF types out of schemas.

RESTRICTION: Do not generate a client, rename routes, add endpoints, or expose Function triggers/internal service topology.

USAGE: Use current official ASP.NET Core OpenAPI APIs and api-contract-checker.

BEHAVIOR: Build each host, generate/inspect each OpenAPI document, run contract checks for schema leakage/duplicate operation IDs and STOP.
```

---

# Part 8 — React client using local PCDS

## Prompt 129 — Create the shared typed Gateway API client

```text
REQUIREMENTS:
  TRACEABILITY: API-001..007; ERROR-001..005; TRACE-001..007; SEC-020..025
  REQUIREMENT LINKS: [API-001..007](../requirements/lightweight-crm-product-and-system-requirements.md#api-001); [ERROR-001..005](../requirements/lightweight-crm-product-and-system-requirements.md#error-001); [TRACE-001..007](../requirements/lightweight-crm-product-and-system-requirements.md#trace-001); [SEC-020..025](../requirements/lightweight-crm-product-and-system-requirements.md#sec-020)
  REQUIREMENT INTENT: Use consistent REST routes, conventional HTTP verbs/status codes, bounded pagination, OpenAPI documentation, and versionable public contracts. Return safe errors that distinguish validation/auth/authz/not-found/concurrency/internal failures and provide a trace/support reference without exposing internals. Also satisfy the remaining linked cross-cutting constraints that apply to this atomic step.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Create the React shared typed HTTP client targeting the YARP base URL only.

CONSTRAINT: Centralize base URL/config, approved auth transport behavior, ProblemDetails mapping, cancellation and correlation/support-reference capture.

RESTRICTION: Do not add Client/Project/Task APIs or raw service URLs.

USAGE: Follow frontend.md/add-component skill.

BEHAVIOR: Add unit tests for base URL, 401 vs 403 mapping, ProblemDetails and cancellation; run web tests/build. STOP.
```

## Prompt 130 — Create React authentication state and protected routing

```text
REQUIREMENTS:
  TRACEABILITY: SEC-001..016; UX-003..005
  REQUIREMENT LINKS: [SEC-001..016](../requirements/lightweight-crm-product-and-system-requirements.md#sec-001); [UX-003..005](../requirements/lightweight-crm-product-and-system-requirements.md#ux-003)
  REQUIREMENT INTENT: Use ASP.NET Core Identity and server-side role/policy authorization with least privilege; protect APIs and never expose passwords, tokens, secrets, or sensitive internals. Keep workflows simple with deliberate loading, empty, validation, success, failure, conflict, and unauthorized states.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Implement client auth/session state and protected-route behavior using the approved Identity current-user/login/logout contract.

CONSTRAINT: Do not store forbidden long-lived credentials in browser storage; use PCDS loading/error patterns.

RESTRICTION: Do not build user administration or CRM pages.

USAGE: Follow frontend.md/identity.md.

BEHAVIOR: Add component/router tests for unauthenticated redirect, authenticated access, logout, 401 handling and 403 distinction. STOP.
```

## Prompt 131 — Create the login page with PCDS

```text
REQUIREMENTS:
  TRACEABILITY: SEC-001..009; DESIGN-001..004; ACCESS-001..005
  REQUIREMENT LINKS: [SEC-001..009](../requirements/lightweight-crm-product-and-system-requirements.md#sec-001); [DESIGN-001..004](../requirements/lightweight-crm-product-and-system-requirements.md#design-001); [ACCESS-001..005](../requirements/lightweight-crm-product-and-system-requirements.md#access-001)
  REQUIREMENT INTENT: Authentication/account security uses ASP.NET Core Identity for account and password operations, and authentication events are audited without logging credentials. Frontend features use local PCDS components and shared typography/spacing/color/border/elevation/state/layout tokens instead of recreating them. Frontend behavior targets WCAG 2.2 AA with keyboard access, labels, associated validation messages, and non-color-only state.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Create the login page only using copied local PCDS primitives/recipes.

CONSTRAINT: Accessible labels, keyboard behavior, pending/error state, no custom credential validation beyond safe client hints.

RESTRICTION: Do not add registration/recovery UI or duplicate PCDS styles.

USAGE: Use add-component skill.

BEHAVIOR: Run component tests, accessibility checks available in repo, lint and build. STOP.
```

## Prompt 132 — Create typed Client API module

```text
REQUIREMENTS:
  TRACEABILITY: CLIENT-001..032; API-001..007
  REQUIREMENT LINKS: [CLIENT-001..032](../requirements/lightweight-crm-product-and-system-requirements.md#client-001); [API-001..007](../requirements/lightweight-crm-product-and-system-requirements.md#api-001)
  REQUIREMENT INTENT: Clients are the CRM anchor and must support the required data, lifecycle/archive behavior, searchable paginated lists, detail views, ownership, and auditable changes. Use consistent REST routes, conventional HTTP verbs/status codes, bounded pagination, OpenAPI documentation, and versionable public contracts.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Create the React typed Client API/model module only for existing public Client endpoints.

CONSTRAINT: Mirror public API contracts, not Crm.Core models; all calls use shared Gateway client.

RESTRICTION: Do not create pages/components.

USAGE: Follow frontend.md.

BEHAVIOR: Add API-module tests with mocked Gateway client and run TypeScript build. STOP.
```

## Prompt 133 — Create Clients list page

```text
REQUIREMENTS:
  TRACEABILITY: CLIENT-020..024; UX-001..006; ACCESS-001..005
  REQUIREMENT LINKS: [CLIENT-020..024](../requirements/lightweight-crm-product-and-system-requirements.md#client-020); [UX-001..006](../requirements/lightweight-crm-product-and-system-requirements.md#ux-001); [ACCESS-001..005](../requirements/lightweight-crm-product-and-system-requirements.md#access-001)
  REQUIREMENT INTENT: Client collections require server-side pagination plus the specified search, filters, and sorts; unbounded result sets are prohibited. The UI prioritizes simple workflows with clear validation/save/failure/loading/empty/unauthorized states, explicit destructive intent, and responsive desktop/tablet behavior. Frontend behavior targets WCAG 2.2 AA with keyboard access, labels, associated validation messages, and non-color-only state.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Create the Clients list/search/filter/sort/pagination page only.

CONSTRAINT: Use local PCDS table/card/form/loading/empty/error patterns; URL query state where established; accessible filters; archived excluded by default.

RESTRICTION: Do not add create/detail form behavior except navigation.

USAGE: Use add-component skill.

BEHAVIOR: Add component tests for loading, empty, error, search/filter/pagination and keyboard behavior; lint/build. STOP.
```

## Prompt 134 — Create Client create form

```text
REQUIREMENTS:
  TRACEABILITY: CLIENT-001..004; UX-003..005; ACCESS-001..005
  REQUIREMENT LINKS: [CLIENT-001..004](../requirements/lightweight-crm-product-and-system-requirements.md#client-001); [UX-003..005](../requirements/lightweight-crm-product-and-system-requirements.md#ux-003); [ACCESS-001..005](../requirements/lightweight-crm-product-and-system-requirements.md#access-001)
  REQUIREMENT INTENT: Authorized users can create Clients with the required CRM/contact/ownership metadata; names are searchable and likely duplicates are detected without silent merging. Keep workflows simple with deliberate loading, empty, validation, success, failure, conflict, and unauthorized states. Frontend behavior targets WCAG 2.2 AA with keyboard access, labels, associated validation messages, and non-color-only state.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Create the Client create form/page only.

CONSTRAINT: Use PCDS Field/Input/Button patterns; render server validation and duplicate warning behavior; successful save navigates to Client detail according to router convention.

RESTRICTION: Do not add edit/lifecycle/archive controls.

USAGE: Use add-component skill.

BEHAVIOR: Add form tests for required validation display, pending, server error, duplicate warning and success. STOP.
```

## Prompt 135 — Create Client detail page

```text
REQUIREMENTS:
  TRACEABILITY: CLIENT-030..032; ACTIVITY-001..003
  REQUIREMENT LINKS: [CLIENT-030..032](../requirements/lightweight-crm-product-and-system-requirements.md#client-030); [ACTIVITY-001..003](../requirements/lightweight-crm-product-and-system-requirements.md#activity-001)
  REQUIREMENT INTENT: Client detail must expose Client/lifecycle/owner information plus related Projects, Tasks, recent activity, and authorized audit history/navigation. Recent activity is derived from significant audit events and shown in user-friendly form with underlying audit links where authorized.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Create the Client detail page only.

CONSTRAINT: Show Client information, lifecycle, owner, active/historical Project summaries, open/recently completed Task summaries and recent activity/audit link when authorized, using existing public APIs.

RESTRICTION: Do not add lifecycle/archive mutations yet.

USAGE: Use add-component skill.

BEHAVIOR: Add loading/empty/error/authorization and data-render tests; lint/build. STOP.
```

## Prompt 136 — Add Client lifecycle control

```text
REQUIREMENTS:
  TRACEABILITY: CLIENT-010..015; ACCESS-001..005
  REQUIREMENT LINKS: [CLIENT-010..015](../requirements/lightweight-crm-product-and-system-requirements.md#client-010); [ACCESS-001..005](../requirements/lightweight-crm-product-and-system-requirements.md#access-001)
  REQUIREMENT INTENT: Each Client has one lifecycle status; lifecycle changes are audited, archived Clients are excluded by default, and archival preserves history. Frontend behavior targets WCAG 2.2 AA with keyboard access, labels, associated validation messages, and non-color-only state.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Add only the lifecycle status-change control to Client detail.

CONSTRAINT: Use PCDS accessible select/dialog/confirmation patterns as appropriate; display stale-concurrency conflict without overwriting newer data.

RESTRICTION: Do not add archive controls.

USAGE: Use add-component skill.

BEHAVIOR: Add component tests for allowed options, rejected response, conflict, pending and keyboard operation. STOP.
```

## Prompt 137 — Add Client archive/restore controls

```text
REQUIREMENTS:
  TRACEABILITY: CLIENT-013..015; UX-004
  REQUIREMENT LINKS: [CLIENT-013..015](../requirements/lightweight-crm-product-and-system-requirements.md#client-013); [UX-004](../requirements/lightweight-crm-product-and-system-requirements.md#ux-004)
  REQUIREMENT INTENT: Client archival is non-destructive; archived Clients are excluded from normal active lists and Clients with active Projects cannot be permanently removed. Keep workflows simple with deliberate loading, empty, validation, success, failure, conflict, and unauthorized states.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Add only Client archive/restore controls to the detail experience.

CONSTRAINT: Require explicit confirmation for archive; show active-Project blocking message; restore only when authorized.

RESTRICTION: Do not add permanent delete.

USAGE: Use add-component skill.

BEHAVIOR: Add component tests for confirm/cancel/blocked/success/authorization. STOP.
```

## Prompt 137A — Add Client profile edit UI

```text
REQUIREMENTS:
  TRACEABILITY: CLIENT-002; UX-003..005; ACCESS-001..005
  REQUIREMENT LINKS: [CLIENT-002](../requirements/lightweight-crm-product-and-system-requirements.md#client-002); [UX-003..005](../requirements/lightweight-crm-product-and-system-requirements.md#ux-003); [ACCESS-001..005](../requirements/lightweight-crm-product-and-system-requirements.md#access-001)
  REQUIREMENT INTENT: Clients are the CRM anchor and must support the required data, lifecycle/archive behavior, searchable paginated lists, detail views, ownership, and auditable changes. Keep workflows simple with deliberate loading, empty, validation, success, failure, conflict, and unauthorized states. Frontend behavior targets WCAG 2.2 AA with keyboard access, labels, associated validation messages, and non-color-only state.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Add only the Client profile edit form/action to the Client detail experience.

CONSTRAINT: Edit only fields allowed by the public Client update contract; use PCDS; preserve lifecycle/archive as separate controls; surface concurrency conflict without overwriting newer data.

RESTRICTION: Do not combine lifecycle/archive changes or redesign the detail page.

USAGE: Use add-component skill.

BEHAVIOR: Add tests for edit success, validation, stale conflict, cancel and keyboard/accessibility behavior; lint/build and STOP.
```

## Prompt 138 — Create typed Project API module

```text
REQUIREMENTS:
  TRACEABILITY: PROJECT-001..031; API-001..007
  REQUIREMENT LINKS: [PROJECT-001..031](../requirements/lightweight-crm-product-and-system-requirements.md#project-001); [API-001..007](../requirements/lightweight-crm-product-and-system-requirements.md#api-001)
  REQUIREMENT INTENT: Projects belong to one Client and must support the required metadata, statuses, filtering/search/detail behavior, completion rules, and non-destructive archival. Use consistent REST routes, conventional HTTP verbs/status codes, bounded pagination, OpenAPI documentation, and versionable public contracts.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Create the React typed Project API/model module only.

CONSTRAINT: Use shared Gateway client and mirror public contracts.

RESTRICTION: Do not create UI.

USAGE: Follow frontend.md.

BEHAVIOR: Add API-module tests and TypeScript build. STOP.
```

## Prompt 139 — Create Project list and detail pages

```text
REQUIREMENTS:
  TRACEABILITY: PROJECT-020..031; UX-001..006; ACCESS-001..005
  REQUIREMENT LINKS: [PROJECT-020..031](../requirements/lightweight-crm-product-and-system-requirements.md#project-020); [UX-001..006](../requirements/lightweight-crm-product-and-system-requirements.md#ux-001); [ACCESS-001..005](../requirements/lightweight-crm-product-and-system-requirements.md#access-001)
  REQUIREMENT INTENT: Projects belong to one Client and must support the required metadata, statuses, filtering/search/detail behavior, completion rules, and non-destructive archival. The UI prioritizes simple workflows with clear validation/save/failure/loading/empty/unauthorized states, explicit destructive intent, and responsive desktop/tablet behavior. Frontend behavior targets WCAG 2.2 AA with keyboard access, labels, associated validation messages, and non-color-only state.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Create Project list and Project detail pages only.

CONSTRAINT: Use PCDS; support required filters/search/pagination and show Client/status/owner/priority/dates/open/completed Tasks.

RESTRICTION: Do not add create/status/archive mutations yet.

USAGE: Use add-component skill.

BEHAVIOR: Add component tests for list filters, detail loading/error/empty and keyboard/accessibility behavior. STOP.
```

## Prompt 140 — Create Project create form

```text
REQUIREMENTS:
  TRACEABILITY: PROJECT-001..003; UX-003..005
  REQUIREMENT LINKS: [PROJECT-001..003](../requirements/lightweight-crm-product-and-system-requirements.md#project-001); [UX-003..005](../requirements/lightweight-crm-product-and-system-requirements.md#ux-003)
  REQUIREMENT INTENT: Authorized users can create a Project for exactly one Client using the required Project metadata. Keep workflows simple with deliberate loading, empty, validation, success, failure, conflict, and unauthorized states.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Create the Project create form only.

CONSTRAINT: Require/select owning Client from authorized data, use PCDS fields and server error mapping.

RESTRICTION: Do not add status/archive controls.

USAGE: Use add-component skill.

BEHAVIOR: Add pending/validation/authorization/success component tests. STOP.
```

## Prompt 141 — Add Project status and archive controls

```text
REQUIREMENTS:
  TRACEABILITY: PROJECT-010..014; UX-004
  REQUIREMENT LINKS: [PROJECT-010..014](../requirements/lightweight-crm-product-and-system-requirements.md#project-010); [UX-004](../requirements/lightweight-crm-product-and-system-requirements.md#ux-004)
  REQUIREMENT INTENT: Project status changes are auditable; completion records actual completion time and requires acknowledgement when open Tasks remain; archival is non-destructive. Keep workflows simple with deliberate loading, empty, validation, success, failure, conflict, and unauthorized states.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Add only Project status-transition and archive controls to Project detail.

CONSTRAINT: Explicitly surface open-Task acknowledgement before completion and stale concurrency conflicts.

RESTRICTION: Do not auto-complete Tasks.

USAGE: Use add-component skill.

BEHAVIOR: Add tests for completion acknowledgement, rejected transition, conflict, archive confirmation and authorization. STOP.
```

## Prompt 141A — Add Project details edit UI

```text
REQUIREMENTS:
  TRACEABILITY: PROJECT-002; UX-003..005; ACCESS-001..005
  REQUIREMENT LINKS: [PROJECT-002](../requirements/lightweight-crm-product-and-system-requirements.md#project-002); [UX-003..005](../requirements/lightweight-crm-product-and-system-requirements.md#ux-003); [ACCESS-001..005](../requirements/lightweight-crm-product-and-system-requirements.md#access-001)
  REQUIREMENT INTENT: Projects belong to one Client and must support the required metadata, statuses, filtering/search/detail behavior, completion rules, and non-destructive archival. Keep workflows simple with deliberate loading, empty, validation, success, failure, conflict, and unauthorized states. Frontend behavior targets WCAG 2.2 AA with keyboard access, labels, associated validation messages, and non-color-only state.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Add only ordinary Project details editing to Project detail.

CONSTRAINT: Use the dedicated Project update contract and PCDS; do not edit status/completion/archive in the same form; surface concurrency conflicts safely.

RESTRICTION: Do not change Client ownership or redesign Project pages.

USAGE: Use add-component skill.

BEHAVIOR: Add tests for edit success, invalid dates, stale conflict, cancel and accessibility; STOP.
```

## Prompt 142 — Create typed Task API module

```text
REQUIREMENTS:
  TRACEABILITY: TASK-001..022; API-001..007
  REQUIREMENT LINKS: [TASK-001..022](../requirements/lightweight-crm-product-and-system-requirements.md#task-001); [API-001..007](../requirements/lightweight-crm-product-and-system-requirements.md#api-001)
  REQUIREMENT INTENT: Tasks belong to one Project and must support assignment, priority, status/completion/reopen behavior, overdue detection, and filterable task views. Use consistent REST routes, conventional HTTP verbs/status codes, bounded pagination, OpenAPI documentation, and versionable public contracts.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Create the React typed Task API/model module only.

CONSTRAINT: Use shared Gateway client and public contracts.

RESTRICTION: Do not create UI.

USAGE: Follow frontend.md.

BEHAVIOR: Add API-module tests and build. STOP.
```

## Prompt 143 — Create Task list/My Tasks/overdue UI

```text
REQUIREMENTS:
  TRACEABILITY: TASK-020..022; UX-001..006; ACCESS-001..005
  REQUIREMENT LINKS: [TASK-020..022](../requirements/lightweight-crm-product-and-system-requirements.md#task-020); [UX-001..006](../requirements/lightweight-crm-product-and-system-requirements.md#ux-001); [ACCESS-001..005](../requirements/lightweight-crm-product-and-system-requirements.md#access-001)
  REQUIREMENT INTENT: Task collections support My Tasks/project/open/completed/overdue views plus the required filters and sorts. The UI prioritizes simple workflows with clear validation/save/failure/loading/empty/unauthorized states, explicit destructive intent, and responsive desktop/tablet behavior. Frontend behavior targets WCAG 2.2 AA with keyboard access, labels, associated validation messages, and non-color-only state.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Create the Task list experience supporting My Tasks, open/completed/overdue and required filters/sorts only.

CONSTRAINT: Use PCDS patterns and accessible controls; preserve URL/filter state according to app convention.

RESTRICTION: Do not add Task mutations.

USAGE: Use add-component skill.

BEHAVIOR: Add component tests for each required view, filter, overdue indication, empty/error/loading and keyboard behavior. STOP.
```

## Prompt 144 — Create Task create form

```text
REQUIREMENTS:
  TRACEABILITY: TASK-001..016; UX-003..005
  REQUIREMENT LINKS: [TASK-001..016](../requirements/lightweight-crm-product-and-system-requirements.md#task-001); [UX-003..005](../requirements/lightweight-crm-product-and-system-requirements.md#ux-003)
  REQUIREMENT INTENT: Authorized users can create Tasks for one Project with the required metadata, statuses, priorities, assignment, completion, reopen, and overdue behavior. Keep workflows simple with deliberate loading, empty, validation, success, failure, conflict, and unauthorized states.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Create the Task create form only.

CONSTRAINT: Use PCDS and existing authorized Project context; support assignee/priority/due date fields per public contract.

RESTRICTION: Do not add assignment/status/priority update controls.

USAGE: Use add-component skill.

BEHAVIOR: Add validation/pending/server-error/success tests and build. STOP.
```

## Prompt 145 — Add Task assignment/priority/status/reopen controls

```text
REQUIREMENTS:
  TRACEABILITY: TASK-010..016; UX-003..005
  REQUIREMENT LINKS: [TASK-010..016](../requirements/lightweight-crm-product-and-system-requirements.md#task-010); [UX-003..005](../requirements/lightweight-crm-product-and-system-requirements.md#ux-003)
  REQUIREMENT INTENT: Task workflow includes required statuses, assignment/reassignment, priority changes, completion timestamps, explicit reopen, and overdue semantics with auditable mutations. Keep workflows simple with deliberate loading, empty, validation, success, failure, conflict, and unauthorized states.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Add the existing Task mutation controls to Task detail/list as one cohesive Task-actions component.

CONSTRAINT: Each action uses its dedicated API contract, handles concurrency conflict, enforces role-based affordances, and is keyboard accessible.

RESTRICTION: Do not invent bulk editing or drag-and-drop Kanban behavior.

USAGE: Use add-component skill.

BEHAVIOR: Add component tests for assign, priority, status complete, reopen, conflict and unauthorized affordance. STOP.
```

## Prompt 145A — Add Task details edit UI

```text
REQUIREMENTS:
  TRACEABILITY: TASK-002; UX-003..005; ACCESS-001..005
  REQUIREMENT LINKS: [TASK-002](../requirements/lightweight-crm-product-and-system-requirements.md#task-002); [UX-003..005](../requirements/lightweight-crm-product-and-system-requirements.md#ux-003); [ACCESS-001..005](../requirements/lightweight-crm-product-and-system-requirements.md#access-001)
  REQUIREMENT INTENT: Tasks belong to one Project and must support assignment, priority, status/completion/reopen behavior, overdue detection, and filterable task views. Keep workflows simple with deliberate loading, empty, validation, success, failure, conflict, and unauthorized states. Frontend behavior targets WCAG 2.2 AA with keyboard access, labels, associated validation messages, and non-color-only state.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Add only ordinary Task details editing to the Task experience.

CONSTRAINT: Edit title/description/start/due/notes through the dedicated update contract; keep assignee/priority/status/reopen in existing action controls; handle concurrency conflict.

RESTRICTION: Do not combine other mutations or invent Kanban/bulk editing.

USAGE: Use add-component skill.

BEHAVIOR: Add tests for edit success, due-date validation, stale conflict, cancel and accessibility; STOP.
```

## Prompt 145B — Create Administrator user-management UI

```text
REQUIREMENTS:
  TRACEABILITY: Administrator user-management requirements; SEC-004; SEC-010..016; ACCESS-001..005
  REQUIREMENT LINKS: [Administrator requirements](../requirements/lightweight-crm-product-and-system-requirements.md#31-administrator); [SEC-004](../requirements/lightweight-crm-product-and-system-requirements.md#sec-004); [SEC-010..016](../requirements/lightweight-crm-product-and-system-requirements.md#sec-010); [ACCESS-001..005](../requirements/lightweight-crm-product-and-system-requirements.md#access-001)
  REQUIREMENT INTENT: Administrators can manage application users, roles, and permissions, with server-side authorization and auditable changes. Use ASP.NET Core Identity and server-side role/policy authorization with least privilege; protect APIs and never expose passwords, tokens, secrets, or sensitive internals. Frontend behavior targets WCAG 2.2 AA with keyboard access, labels, associated validation messages, and non-color-only state.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Create the Administrator-only user-management page using the existing Identity administration APIs.

CONSTRAINT: Support paginated user list/detail, create user, activate/deactivate and role assignment through existing endpoints; use PCDS and accessible confirmation/error patterns.

RESTRICTION: Do not expose password hashes/tokens, add new Identity behavior, or implement password reset UI unless the approved recovery ADR explicitly allows an admin surface.

USAGE: Use add-component skill and typed Gateway Identity API module.

BEHAVIOR: Add component tests for admin access, create, role change, deactivate/reactivate, 403 behavior and accessibility; lint/build and STOP.
```

## Prompt 145C — Create password change/recovery UI

```text
REQUIREMENTS:
  TRACEABILITY: SEC-004; ACCESS-001..005; UX-003..005
  REQUIREMENT LINKS: [SEC-004](../requirements/lightweight-crm-product-and-system-requirements.md#sec-004); [ACCESS-001..005](../requirements/lightweight-crm-product-and-system-requirements.md#access-001); [UX-003..005](../requirements/lightweight-crm-product-and-system-requirements.md#ux-003)
  REQUIREMENT INTENT: Use ASP.NET Core Identity and server-side role/policy authorization with least privilege; protect APIs and never expose passwords, tokens, secrets, or sensitive internals. Frontend behavior targets WCAG 2.2 AA with keyboard access, labels, associated validation messages, and non-color-only state. Keep workflows simple with deliberate loading, empty, validation, success, failure, conflict, and unauthorized states.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Create only the password change and approved recovery UI required by the authentication ADR.

CONSTRAINT: Use PCDS; never persist password/reset token values; do not reveal whether an unknown account exists if the recovery policy requires non-enumeration.

RESTRICTION: Do not add MFA/passkeys/external login or invent a recovery channel.

USAGE: Use add-component skill and existing Identity endpoints.

BEHAVIOR: Add tests for password change success/error, recovery request/complete behavior defined by ADR, pending states and accessibility; STOP.
```

---

# Part 9 — Dashboard, global search, activity, and operational observability

## Prompt 146 — Implement dashboard Core query

```text
REQUIREMENTS:
  TRACEABILITY: DASH-001..003; PERF-001..004
  REQUIREMENT LINKS: [DASH-001..003](../requirements/lightweight-crm-product-and-system-requirements.md#dash-001); [PERF-001..004](../requirements/lightweight-crm-product-and-system-requirements.md#perf-001)
  REQUIREMENT INTENT: The lightweight dashboard summarizes active Clients/Projects, approaching deadlines, open/current-user/overdue/recent Tasks, and recent Client activity within authorization scope. Interactive APIs target responsive p95 behavior, bounded collections, efficient indexed searches, and no unnecessary N+1 query patterns.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Implement the Crm dashboard read query through Repository/Data/Business/Facade only.

CONSTRAINT: Return active Clients, active Projects, Projects approaching target date, open Tasks, current-user Tasks, overdue Tasks, recently completed Tasks and recent Client activity summaries; respect authorization scope; keep SQL bounded.

RESTRICTION: Do not add controller/UI or cross-query AuditDb directly.

USAGE: Follow backend.md/database.md.

BEHAVIOR: Add SQL integration tests for each metric and authorization scope. STOP.
```

## Prompt 147 — Add dashboard API action

```text
REQUIREMENTS:
  TRACEABILITY: DASH-001..003; API-001..007
  REQUIREMENT LINKS: [DASH-001..003](../requirements/lightweight-crm-product-and-system-requirements.md#dash-001); [API-001..007](../requirements/lightweight-crm-product-and-system-requirements.md#api-001)
  REQUIREMENT INTENT: The lightweight dashboard summarizes active Clients/Projects, approaching deadlines, open/current-user/overdue/recent Tasks, and recent Client activity within authorization scope. Use consistent REST routes, conventional HTTP verbs/status codes, bounded pagination, OpenAPI documentation, and versionable public contracts.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Add one dashboard controller action only.

CONSTRAINT: Call Dashboard Facade and return typed summary.

RESTRICTION: Do not add UI.

USAGE: Use add-endpoint skill.

BEHAVIOR: Add API tests for authorized summary and 401/403. STOP.
```

## Prompt 148 — Create Dashboard page

```text
REQUIREMENTS:
  TRACEABILITY: DASH-001..003; DESIGN-001..004; ACCESS-001..005
  REQUIREMENT LINKS: [DASH-001..003](../requirements/lightweight-crm-product-and-system-requirements.md#dash-001); [DESIGN-001..004](../requirements/lightweight-crm-product-and-system-requirements.md#design-001); [ACCESS-001..005](../requirements/lightweight-crm-product-and-system-requirements.md#access-001)
  REQUIREMENT INTENT: The lightweight dashboard summarizes active Clients/Projects, approaching deadlines, open/current-user/overdue/recent Tasks, and recent Client activity within authorization scope. Frontend features use local PCDS components and shared typography/spacing/color/border/elevation/state/layout tokens instead of recreating them. Frontend behavior targets WCAG 2.2 AA with keyboard access, labels, associated validation messages, and non-color-only state.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Create the lightweight CRM dashboard page only.

CONSTRAINT: Use PCDS KPI/surface/list patterns; render all required summaries with loading/error/empty behavior.

RESTRICTION: Do not add analytics charts not required by the product.

USAGE: Use add-component skill.

BEHAVIOR: Add component/accessibility tests and web build. STOP.
```

## Prompt 149 — Implement global search Core query

```text
REQUIREMENTS:
  TRACEABILITY: SEARCH-001..004; PERF-001..004
  REQUIREMENT LINKS: [SEARCH-001..004](../requirements/lightweight-crm-product-and-system-requirements.md#search-001); [PERF-001..004](../requirements/lightweight-crm-product-and-system-requirements.md#perf-001)
  REQUIREMENT INTENT: Global search locates Clients, Projects, and Tasks, identifies result type, and never exposes data outside the user's authorization scope. Interactive APIs target responsive p95 behavior, bounded collections, efficient indexed searches, and no unnecessary N+1 query patterns.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Implement Crm global search across Clients, Projects and Tasks through Repository/Data/Business/Facade only.

CONSTRAINT: Return clearly typed result kinds, stable IDs, safe display fields, authorization trimming, bounded result count/pagination and SQL-translatable search.

RESTRICTION: Do not add controller/UI or external search engine.

USAGE: Follow backend.md/database.md.

BEHAVIOR: Add SQL integration tests for each entity type, mixed results, authorization trimming and result limits. STOP.
```

## Prompt 150 — Add global search API action

```text
REQUIREMENTS:
  TRACEABILITY: SEARCH-001..004; API-001..007
  REQUIREMENT LINKS: [SEARCH-001..004](../requirements/lightweight-crm-product-and-system-requirements.md#search-001); [API-001..007](../requirements/lightweight-crm-product-and-system-requirements.md#api-001)
  REQUIREMENT INTENT: Global search locates Clients, Projects, and Tasks, identifies result type, and never exposes data outside the user's authorization scope. Use consistent REST routes, conventional HTTP verbs/status codes, bounded pagination, OpenAPI documentation, and versionable public contracts.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Add one global search controller action only.

CONSTRAINT: Call Search Facade and return typed entity-discriminated results.

RESTRICTION: Do not add UI.

USAGE: Use add-endpoint skill.

BEHAVIOR: Add API tests for query, mixed result types, authorization, invalid/empty query. STOP.
```

## Prompt 151 — Create global search UI

```text
REQUIREMENTS:
  TRACEABILITY: SEARCH-001..004; ACCESS-001..005
  REQUIREMENT LINKS: [SEARCH-001..004](../requirements/lightweight-crm-product-and-system-requirements.md#search-001); [ACCESS-001..005](../requirements/lightweight-crm-product-and-system-requirements.md#access-001)
  REQUIREMENT INTENT: Global search locates Clients, Projects, and Tasks, identifies result type, and never exposes data outside the user's authorization scope. Frontend behavior targets WCAG 2.2 AA with keyboard access, labels, associated validation messages, and non-color-only state.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Create the global search interaction/page only.

CONSTRAINT: Use PCDS accessible search field/result list; label result entity type; navigate via stable public routes; handle loading/empty/error.

RESTRICTION: Do not add fuzzy search library or service-direct calls.

USAGE: Use add-component skill.

BEHAVIOR: Add keyboard, result-type, empty/error and navigation tests; lint/build. STOP.
```

## Prompt 152 — Create Client activity/audit timeline UI

```text
REQUIREMENTS:
  TRACEABILITY: ACTIVITY-001..003; AUDIT-001..008
  REQUIREMENT LINKS: [ACTIVITY-001..003](../requirements/lightweight-crm-product-and-system-requirements.md#activity-001); [AUDIT-001..008](../requirements/lightweight-crm-product-and-system-requirements.md#audit-001)
  REQUIREMENT INTENT: Recent activity is derived from significant audit events and shown in user-friendly form with underlying audit links where authorized. Every Client/Project/Task mutation produces append-only audit evidence with entity/action/time/actor/source, trace/correlation, and applicable before/after values; secrets are redacted.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Create the authorized Client activity timeline UI using the approved Audit API surface.

CONSTRAINT: Render user-friendly descriptions from audit events while preserving support metadata links such as TraceId/CorrelationId for authorized users.

RESTRICTION: Do not query AuditDb from Crm or expose sensitive before/after values without authorization.

USAGE: Use add-component skill and typed Gateway Audit API module.

BEHAVIOR: Add tests for ordered activity, redacted fields, authorization and trace-link rendering. STOP.
```

## Prompt 153 — Add custom business Activity spans

```text
REQUIREMENTS:
  TRACEABILITY: TRACE-001..007; OTEL-004
  REQUIREMENT LINKS: [TRACE-001..007](../requirements/lightweight-crm-product-and-system-requirements.md#trace-001); [OTEL-004](../requirements/lightweight-crm-product-and-system-requirements.md#otel-004)
  REQUIREMENT INTENT: Every inbound request participates in a trace propagated through gateway, services, SQL, HTTP, Service Bus, Functions, and downstream work with safe diagnostic metadata. Instrument APIs, services, Functions, SQL, HTTP, and Service Bus with OpenTelemetry for traces, metrics, and correlated structured logs.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Add custom OpenTelemetry ActivitySource spans around the most important Crm business operations only: Client.Create, Client.UpdateLifecycle, Project.Create, Project.ChangeStatus, Task.Assign, Task.ChangeStatus and Outbox.Publish/relay.

CONSTRAINT: Automatic ASP.NET/SQL/HTTP/Service Bus instrumentation remains intact; tags use stable IDs/statuses only and avoid sensitive payloads.

RESTRICTION: Do not add spans around every method or duplicate automatic dependency spans.

USAGE: Follow observability ADR and CLAUDE.md.

BEHAVIOR: Add unit/integration telemetry tests using an in-memory ActivityListener proving span names, parentage and safe tags. STOP.
```

## Prompt 154 — Add required operational metrics

```text
REQUIREMENTS:
  TRACEABILITY: OBS-003..005; OUTBOX-006; OPS-003
  REQUIREMENT LINKS: [OBS-003..005](../requirements/lightweight-crm-product-and-system-requirements.md#obs-003); [OUTBOX-006](../requirements/lightweight-crm-product-and-system-requirements.md#outbox-006); [OPS-003](../requirements/lightweight-crm-product-and-system-requirements.md#ops-003)
  REQUIREMENT INTENT: Centralize operational visibility in Azure Monitor/Application Insights for requests, dependencies, Functions, Service Bus, SQL, failures, and trace-based investigation. Commit state and integration events atomically through a transactional outbox, then relay them with a timer-triggered Function and observable retry/backlog behavior. Expose health and telemetry that detect errors, latency, dependency failures, authentication anomalies, dead letters, and outbox backlog.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Add OpenTelemetry metrics for request/Function outcomes where custom metrics are needed, outbox pending count, oldest pending age, relay publish failures/retries, audit consumer outcomes and Service Bus dead-letter signal integration point.

CONSTRAINT: Use low-cardinality dimensions only.

RESTRICTION: Do not put Client/Project/Task IDs into metric labels or duplicate standard ASP.NET metrics.

USAGE: Follow observability ADR and functions/messaging rules.

BEHAVIOR: Add metric-instrument tests for names/tags and document dashboard query names. STOP.
```

## Prompt 155 — Create single-pane operational dashboard definition

```text
REQUIREMENTS:
  TRACEABILITY: OBS-001..005; OPS-001..004
  REQUIREMENT LINKS: [OBS-001..005](../requirements/lightweight-crm-product-and-system-requirements.md#obs-001); [OPS-001..004](../requirements/lightweight-crm-product-and-system-requirements.md#ops-001)
  REQUIREMENT INTENT: Azure Monitor/Application Insights provides centralized investigation and dashboards for request/dependency/Function/Service Bus/SQL health, errors, latency, and trace/entity filtering. Operators can determine service health and detect rising errors/latency, SQL or Service Bus failures, Function failures, auth anomalies, dead letters, and outbox backlog.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Create infrastructure/documented dashboard definitions or workbook queries for the approved Azure Monitor/Application Insights single pane of glass.

CONSTRAINT: Include request rate/error/latency, dependency latency/failure, Functions success/failure, Service Bus processing/dead-letter signals, SQL dependencies, outbox backlog, audit consumer health and trace lookup by Correlation/Trace ID.

RESTRICTION: Do not hardcode environment-specific resource IDs/secrets or create unrelated business BI dashboards.

USAGE: Use the observability ADR and current Azure Monitor/Application Insights query conventions.

BEHAVIOR: Validate query syntax where tooling permits and document each panel's signal/source. STOP.
```

## Prompt 155A — Add production secret and managed-identity configuration

```text
REQUIREMENTS:
  TRACEABILITY: SEC-015..016; DEPLOY-002..003; PRIV-001..005
  REQUIREMENT LINKS: [SEC-015..016](../requirements/lightweight-crm-product-and-system-requirements.md#sec-015); [DEPLOY-002..003](../requirements/lightweight-crm-product-and-system-requirements.md#deploy-002); [PRIV-001..005](../requirements/lightweight-crm-product-and-system-requirements.md#priv-001)
  REQUIREMENT INTENT: Use ASP.NET Core Identity and server-side role/policy authorization with least privilege; protect APIs and never expose passwords, tokens, secrets, or sensitive internals. Support environment-specific configuration, externalized secrets, Flex Consumption Functions, and consistent deployment/telemetry metadata. Collect only necessary CRM data, minimize sensitive duplication and PII in telemetry, enforce authorization, and document retention before production.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Implement the production configuration/IaC delta for secret management and managed identity using the infrastructure technology already approved by the repository.

CONSTRAINT: Service/Function identities receive least-privilege SQL/Service Bus/Key Vault permissions; credentials are not committed; Service Bus and host-storage connections prefer identity-based configuration where supported; HTTP hosts do not receive broker rights they do not need.

RESTRICTION: If the repository has no approved production IaC/deployment technology, do not invent one in this prompt—stop and surface that architecture decision. Do not add plaintext secrets or local developer credentials to source.

USAGE: Read aspire.md/functions.md/identity.md and current official Azure managed identity/Key Vault guidance for the chosen IaC.

BEHAVIOR: Run the IaC validation/lint/what-if mechanism available, secret-scan the diff, report RBAC assignments by workload and STOP.
```

## Prompt 155B — Add operational alert definitions

```text
REQUIREMENTS:
  TRACEABILITY: OPS-003..004; OBS-003..005; OUTBOX-006
  REQUIREMENT LINKS: [OPS-003..004](../requirements/lightweight-crm-product-and-system-requirements.md#ops-003); [OBS-003..005](../requirements/lightweight-crm-product-and-system-requirements.md#obs-003); [OUTBOX-006](../requirements/lightweight-crm-product-and-system-requirements.md#outbox-006)
  REQUIREMENT INTENT: Expose health and telemetry that detect errors, latency, dependency failures, authentication anomalies, dead letters, and outbox backlog. Centralize operational visibility in Azure Monitor/Application Insights for requests, dependencies, Functions, Service Bus, SQL, failures, and trace-based investigation. Commit state and integration events atomically through a transactional outbox, then relay them with a timer-triggered Function and observable retry/backlog behavior.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Add only the production alert definitions supported by the approved observability/IaC approach.

CONSTRAINT: Cover sustained API error/latency degradation, Function failures, Service Bus dead-letter growth, outbox backlog/oldest-message age, audit-consumer failures and critical dependency health. Thresholds must be configuration-driven and documented.

RESTRICTION: Do not hardcode personal notification destinations, create business KPI alerts, or duplicate signals with noisy per-instance alerts.

USAGE: Use the observability ADR and existing deployment/IaC conventions.

BEHAVIOR: Validate alert definitions with the available IaC/query tooling and report signal, threshold, evaluation window and action-group placeholder/approved target for each alert; STOP.
```

---

# Part 10 — End-to-end proof, security, resilience, and release gates

## Prompt 156 — Prove one cradle-to-grave Client mutation trace

```text
REQUIREMENTS:
  TRACEABILITY: TRACE-001..007; AUDIT-001..008; OUTBOX-001..006; OBS-001..005
  REQUIREMENT LINKS: [TRACE-001..007](../requirements/lightweight-crm-product-and-system-requirements.md#trace-001); [AUDIT-001..008](../requirements/lightweight-crm-product-and-system-requirements.md#audit-001); [OUTBOX-001..006](../requirements/lightweight-crm-product-and-system-requirements.md#outbox-001); [OBS-001..005](../requirements/lightweight-crm-product-and-system-requirements.md#obs-001)
  REQUIREMENT INTENT: Every inbound request participates in a trace propagated through gateway, services, SQL, HTTP, Service Bus, Functions, and downstream work with safe diagnostic metadata. Every Client/Project/Task mutation produces append-only audit evidence with entity/action/time/actor/source, trace/correlation, and applicable before/after values; secrets are redacted. Also satisfy the remaining linked cross-cutting constraints that apply to this atomic step.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Run one end-to-end Client create through the complete local system and prove its technical and business trace.

CONSTRAINT: Expected chain: React or test client -> YARP -> Crm API -> Facade/Business/Data -> Crm SQL state + outbox -> Crm timer Function -> Service Bus -> Audit ServiceBusTrigger Function -> Audit Core -> AuditDb. Preserve W3C trace plus CorrelationId/CausationId and actor metadata.

RESTRICTION: Do not add features or refactor while proving the trace. Fix only wiring defects required for this exact flow and document each fix.

USAGE: Use aspire run/dashboard, trace-a-request skill, Audit query API and relevant tests.

BEHAVIOR: Provide evidence for one TraceId/CorrelationId across every hop, the matching Client row, outbox row dispatch state and exactly one AuditEntry. Redeliver the audit message if tooling permits and prove no duplicate AuditEntry. STOP.
```

## Prompt 157 — Verify API security controls

```text
REQUIREMENTS:
  TRACEABILITY: SEC-010..025; SEARCH-004; DASH-003
  REQUIREMENT LINKS: [SEC-010..025](../requirements/lightweight-crm-product-and-system-requirements.md#sec-010); [SEARCH-004](../requirements/lightweight-crm-product-and-system-requirements.md#search-004); [DASH-003](../requirements/lightweight-crm-product-and-system-requirements.md#dash-003)
  REQUIREMENT INTENT: Use ASP.NET Core Identity and server-side role/policy authorization with least privilege; protect APIs and never expose passwords, tokens, secrets, or sensitive internals. Global search must find Clients, Projects, and Tasks, identify result type, and never reveal unauthorized data. The dashboard must show the required CRM summaries only within the current user's authorization scope.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Run a focused security verification of all current public routes.

CONSTRAINT: Verify authentication required where intended, role/resource authorization, 401 vs 403, no direct service/Function browser routes, no sensitive errors/logs, HTTPS assumptions/config, input validation and search/dashboard authorization trimming.

RESTRICTION: Do not add new features. Make only minimal security fixes proven necessary by a failing test, one at a time.

USAGE: Use code-reviewer plus security tooling/tests available in repo.

BEHAVIOR: Produce a route-by-route pass/fail matrix and run the full API test suites. STOP.
```

## Prompt 158 — Verify SQL Server integration and concurrency

```text
REQUIREMENTS:
  TRACEABILITY: DATA-001..008; DATA-030..034; TEST-004
  REQUIREMENT LINKS: [DATA-001..008](../requirements/lightweight-crm-product-and-system-requirements.md#data-001); [DATA-030..034](../requirements/lightweight-crm-product-and-system-requirements.md#data-030); [TEST-004](../requirements/lightweight-crm-product-and-system-requirements.md#test-004)
  REQUIREMENT INTENT: Enforce Client→Project→Task relationships, validate before mutation, store UTC, use safe public IDs, and prevent silent concurrent overwrites. Automate business, authorization, API, SQL, messaging, audit, tracing, Function, and UI behavior at the boundary that can actually prove it.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Run the full SQL integration suite against SQL Server-compatible infrastructure.

CONSTRAINT: Cover Client/Project/Task relationships, archive semantics, unique/index invariants, optimistic concurrency conflicts, transaction rollback and outbox/inbox atomicity.

RESTRICTION: Do not use EF InMemory as proof of SQL behavior and do not modify schema unless a failing requirement demonstrates a defect.

USAGE: Use Crm/Audit/Identity integration test projects.

BEHAVIOR: Report exact test counts and any SQL-specific failures. STOP.
```

## Prompt 159 — Verify messaging failure and idempotency matrix

```text
REQUIREMENTS:
  TRACEABILITY: ASYNC-001..008; OUTBOX-001..006; TEST-005
  REQUIREMENT LINKS: [ASYNC-001..008](../requirements/lightweight-crm-product-and-system-requirements.md#async-001); [OUTBOX-001..006](../requirements/lightweight-crm-product-and-system-requirements.md#outbox-001); [TEST-005](../requirements/lightweight-crm-product-and-system-requirements.md#test-005)
  REQUIREMENT INTENT: Durable async work uses Azure Service Bus and Service Bus-triggered Functions with trace correlation, duplicate tolerance/idempotency, bounded retry behavior, and dead-letter visibility. When a transaction changes state and publishes an event, state and outbox commit together; a timer Function relays pending messages idempotently and exposes backlog/failure metrics. Automate business, authorization, API, SQL, messaging, audit, tracing, Function, and UI behavior at the boundary that can actually prove it.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Run the complete messaging reliability test matrix.

CONSTRAINT: Cover successful relay, failed relay remains pending, partial batch, duplicate consumer delivery, failed consumer does not complete inbox, unsupported contract poison policy, cancellation, correlation/causation propagation and follow-on outbox behavior if any.

RESTRICTION: Do not introduce application retry loops or hosted workers to make tests pass.

USAGE: Use test-gap-analyzer and function-boundary-checker.

BEHAVIOR: Report each matrix row pass/fail and prove no BackgroundService/IHostedService exists for bus/outbox work. STOP.
```

## Prompt 160 — Verify accessibility and responsive UI

```text
REQUIREMENTS:
  TRACEABILITY: ACCESS-001..005; UX-001..006; DESIGN-001..004
  REQUIREMENT LINKS: [ACCESS-001..005](../requirements/lightweight-crm-product-and-system-requirements.md#access-001); [UX-001..006](../requirements/lightweight-crm-product-and-system-requirements.md#ux-001); [DESIGN-001..004](../requirements/lightweight-crm-product-and-system-requirements.md#design-001)
  REQUIREMENT INTENT: Frontend behavior targets WCAG 2.2 AA with keyboard access, labels, associated validation messages, and non-color-only state. The UI prioritizes simple workflows with clear validation/save/failure/loading/empty/unauthorized states, explicit destructive intent, and responsive desktop/tablet behavior. Frontend features use local PCDS components and shared typography/spacing/color/border/elevation/state/layout tokens instead of recreating them.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Run an accessibility/responsive verification pass across login, dashboard, Clients, Projects, Tasks and global search.

CONSTRAINT: Verify semantic labels, keyboard navigation, visible focus, status announcements, no color-only state, light/dark mode, loading/empty/error/unauthorized states and common desktop/tablet layouts.

RESTRICTION: Do not redesign visual language or bypass PCDS. Make only requirement-backed fixes discovered by the verification.

USAGE: Use frontend rules/add-component patterns plus repository accessibility tooling.

BEHAVIOR: Run lint, component tests, accessibility checks and production web build; report pass/fail by page. STOP.
```

## Prompt 161 — Verify performance guardrails

```text
REQUIREMENTS:
  TRACEABILITY: PERF-001..004; CLIENT-024; PROJECT-023; API-005
  REQUIREMENT LINKS: [PERF-001..004](../requirements/lightweight-crm-product-and-system-requirements.md#perf-001); [CLIENT-024](../requirements/lightweight-crm-product-and-system-requirements.md#client-024); [PROJECT-023](../requirements/lightweight-crm-product-and-system-requirements.md#project-023); [API-005](../requirements/lightweight-crm-product-and-system-requirements.md#api-005)
  REQUIREMENT INTENT: Interactive APIs target responsive p95 behavior, bounded collections, efficient indexed searches, and no unnecessary N+1 query patterns. Clients are the CRM anchor and must support the required data, lifecycle/archive behavior, searchable paginated lists, detail views, ownership, and auditable changes. Also satisfy the remaining linked cross-cutting constraints that apply to this atomic step.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Measure representative Client, Project, Task, dashboard and search requests under a modest expected-load test and inspect SQL query behavior.

CONSTRAINT: Confirm bounded pagination, no obvious N+1 patterns, sensible indexes/query plans and p95 target intent under expected local/test conditions. Treat environment limits honestly.

RESTRICTION: Do not introduce caching, Redis, denormalized read models or new infrastructure unless a measured requirement failure justifies a separate ADR/prompt.

USAGE: Use existing load-test tooling or create a minimal test harness only if one is already approved by repo conventions.

BEHAVIOR: Report measured p50/p95, query counts and any requirement risk without speculative optimization. STOP.
```

## Prompt 162 — Run architecture guardrail review

```text
REQUIREMENTS:
  TRACEABILITY: All architecture constraints; TEST-001..007
  REQUIREMENT LINKS: [Project Chicago requirements](../requirements/lightweight-crm-product-and-system-requirements.md) and [CLAUDE.md](../../CLAUDE.md); [TEST-001..007](../requirements/lightweight-crm-product-and-system-requirements.md#test-001)
  REQUIREMENT INTENT: Preserve all Project Chicago architecture boundaries and prove them with automated tests. This includes service/database ownership, Controller/Function → Facade → Business → Data → Repository → DbContext layering, Functions-based async processing, YARP-only browser access, SQL Server, and PCDS reuse.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Run read-only architecture review across the completed solution.

CONSTRAINT: Verify project references, Controller/Function -> Facade -> Business -> Data -> Repository -> DbContext direction, one DB per service, no cross-service Core/DbContext access, no direct Service Bus send from request path, no HTTP-triggered Functions, React -> Gateway only, PCDS reuse and no PostgreSQL artifacts.

RESTRICTION: Do not modify code in this prompt.

USAGE: Delegate to code-reviewer, function-boundary-checker, api-contract-checker and test-gap-analyzer.

BEHAVIOR: Produce blocking/non-blocking findings with exact file references and requirement IDs. Verify git status unchanged. STOP.
```

## Prompt 163 — Run full solution release verification

```text
REQUIREMENTS:
  TRACEABILITY: All P0/P1 requirements
  REQUIREMENT LINKS: [P0/P1 requirement priorities](../requirements/lightweight-crm-product-and-system-requirements.md#47-requirement-priorities)
  REQUIREMENT INTENT: Verify every mandatory foundation and required product-experience requirement, including CRM behavior, Identity/authorization, SQL persistence, auditability, tracing/OpenTelemetry, dashboard/search, and UX. A requirement is complete only when implementation and the required automated/runtime evidence exist.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Run the final build/test/start verification without adding features.

CONSTRAINT: All .NET projects build; all .NET tests pass; React lint/tests/build pass; Aspire model starts required resources; Gateway routes work; Identity auth works; Crm flows work; Audit ingestion works; OpenTelemetry traces are visible locally; no pending destructive migration surprise.

RESTRICTION: Do not refactor or expand scope. Any failure becomes a separate micro-prompt, not a broad cleanup.

USAGE: Use canonical repository commands and Aspire dashboard.

BEHAVIOR: Produce final release-gate checklist with commands, results, failed/deferred requirement IDs and explicit statement whether the current branch satisfies the requirements baseline. STOP.
```

---

# Part 11 — Reusable atomic SCRUB templates after initial implementation

Use these only after the main sequence is complete. They preserve the same microstep discipline.

## Template A — One new HTTP query

```text
REQUIREMENTS:
  TRACEABILITY: <IDs>
  REQUIREMENT LINKS: [Project Chicago requirements](../requirements/lightweight-crm-product-and-system-requirements.md)
  REQUIREMENT INTENT: Before using this reusable template, replace the placeholder with the exact requirement IDs and a 2–4 sentence summary of the behavior they require.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Add exactly one read-only HTTP use case to <owning-service>.

CONSTRAINT: Follow CLAUDE.md and .claude/skills/add-endpoint. Public contract is gateway-visible; Controller -> Facade -> Business -> Data -> Repository -> owning DbContext. Query is bounded/paginated when it can return a collection.

RESTRICTION: Do not add mutations, schema changes, cross-service DB access, UI, events, or caching.

USAGE: Read backend.md, database.md, gateway.md and add-endpoint/SKILL.md.

BEHAVIOR: Define/adjust only the files required for this one query, run focused Core SQL tests + API contract tests, report requirement coverage and STOP.
```

## Template B — One new mutation

```text
REQUIREMENTS:
  TRACEABILITY: <IDs>
  REQUIREMENT LINKS: [Project Chicago requirements](../requirements/lightweight-crm-product-and-system-requirements.md)
  REQUIREMENT INTENT: Before using this reusable template, replace the placeholder with the exact requirement IDs and a 2–4 sentence summary of the behavior they require.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Add exactly one mutation to <owning-service>.

CONSTRAINT: Business owns the rule; Data owns the transaction; Repository owns persistence; state + required audit/integration outbox rows commit atomically; authorization is server-side; concurrency is handled when relevant.

RESTRICTION: Do not publish directly to Service Bus, write another service DB, add UI, or combine another mutation.

USAGE: Use add-endpoint and add-integration-event/add-audit-event when applicable.

BEHAVIOR: Implement only this mutation, add happy/validation/domain/authorization/concurrency/rollback/outbox tests that apply, run focused suites and STOP.
```

## Template C — One Service Bus consumer

```text
REQUIREMENTS:
  TRACEABILITY: <IDs>
  REQUIREMENT LINKS: [Project Chicago requirements](../requirements/lightweight-crm-product-and-system-requirements.md)
  REQUIREMENT INTENT: Before using this reusable template, replace the placeholder with the exact requirement IDs and a 2–4 sentence summary of the behavior they require.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Add exactly one Service Bus-triggered Function for <event> in <owning-service>.Functions.

CONSTRAINT: Function is transport-only and delegates to owning .Core. Persistent inbox idempotency is mandatory. Preserve trace/correlation/causation. Failure must remain visible to Functions/Service Bus retry behavior.

RESTRICTION: No BackgroundService/IHostedService, no direct DbContext/Repository in Function, no catch-and-return-success, no cross-service Core reference.

USAGE: Use add-function-trigger and add-integration-event skills.

BEHAVIOR: Implement the trigger + only the required existing Core seam delta, test valid bind, duplicate delivery, failure, correlation and cancellation, run function-boundary-checker and STOP.
```

## Template D — One schema change

```text
REQUIREMENTS:
  TRACEABILITY: <IDs>
  REQUIREMENT LINKS: [Project Chicago requirements](../requirements/lightweight-crm-product-and-system-requirements.md)
  REQUIREMENT INTENT: Before using this reusable template, replace the placeholder with the exact requirement IDs and a 2–4 sentence summary of the behavior they require.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Make exactly one schema change in <service>Db.

CONSTRAINT: SQL Server/Azure SQL compatible EF Core; migration stays with owning .Core; backward/rollback implications are stated before apply.

RESTRICTION: Do not touch another service DB, hand-edit generated migration without explicit reason, or apply to non-local environment in the same prompt.

USAGE: Read database.md and backend.md.

BEHAVIOR: Change model/config, generate migration, review generated operations, run SQL integration tests, STOP before database update unless the prompt explicitly says apply.
```

## Template E — One React feature delta

```text
REQUIREMENTS:
  TRACEABILITY: <IDs>
  REQUIREMENT LINKS: [Project Chicago requirements](../requirements/lightweight-crm-product-and-system-requirements.md)
  REQUIREMENT INTENT: Before using this reusable template, replace the placeholder with the exact requirement IDs and a 2–4 sentence summary of the behavior they require.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Implement exactly one user-visible React behavior.

CONSTRAINT: Use local copied PCDS, typed Gateway client, strict TypeScript, accessible interaction and existing route/state conventions.

RESTRICTION: No raw internal service URLs, duplicated PCDS recipes/tokens, unrelated page redesign, backend behavior invention, Next.js/SSR or new UI framework.

USAGE: Use add-component skill and frontend.md.

BEHAVIOR: Inspect local PCDS first, implement the smallest feature delta, add focused component/accessibility tests, run lint/tests/build and STOP.
```

## Template F — One defect

```text
REQUIREMENTS:
  TRACEABILITY: <IDs affected>
  REQUIREMENT LINKS: [Project Chicago requirements](../requirements/lightweight-crm-product-and-system-requirements.md)
  REQUIREMENT INTENT: Before using this reusable template, replace the placeholder with the exact requirement IDs and a 2–4 sentence summary of the behavior they require.
  SOURCE OF TRUTH: Read the linked requirement(s) before coding. If this prompt conflicts with the canonical requirement text, STOP and report the drift.

SCOPE: Reproduce and fix exactly one defect in <area>.

CONSTRAINT: Fix the root cause at the owning layer/boundary and add a regression test.

RESTRICTION: Do not refactor unrelated code, widen public contracts unnecessarily, add synchronous cross-service calls, suppress exceptions, or replace architecture to make the symptom disappear.

USAGE: Use read-only agents to locate the cause, then the matching implementation skill.

BEHAVIOR: First reproduce and state evidence. Apply the minimum fix. Run the regression + nearest affected suite. Report root cause, files, tests, trace/requirement IDs and STOP.
```

---

# Completion definition

The prompt library is successful only if running it produces a system where the **user experience remains simple**:

```text
Clients -> Projects -> Tasks
```

while the **engineering trace remains complete**:

```text
Identity
  -> YARP
  -> HTTP trace
  -> Crm Controller
  -> Facade
  -> Business
  -> Data transaction
  -> Repository / SQL Server
  -> Outbox
  -> Timer Function
  -> Service Bus
  -> Audit ServiceBusTrigger Function
  -> Audit Core / Inbox
  -> Audit SQL
  -> Azure Monitor / Application Insights trace + logs + metrics
```

A feature is not done because it compiles. It is done when the relevant requirement IDs are implemented, the smallest applicable tests are green, the audit path is correct for mutations, authorization is enforced, distributed trace context survives the boundary, and the change does not violate the architecture encoded in `CLAUDE.md`.

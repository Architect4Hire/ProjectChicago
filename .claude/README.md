# The `.claude/` folder — Project Chicago

This folder is the reusable Claude Code engineering toolkit for Project Chicago: an Aspire + .NET 10 CRM using ASP.NET Core service hosts, per-service Azure Functions on Flex Consumption, one Microsoft SQL database per bounded service, Azure Service Bus, YARP as the sole gateway, ASP.NET Core Identity, and a React 19 client using a copied local PCDS design system.

The root `CLAUDE.md` is the project memory and architecture constitution. This folder carries the more focused mechanisms that support it.

## What loads when

| Path | Purpose |
|---|---|
| `settings.json` | Shared project settings and deterministic hook wiring. |
| `rules/aspire.md` | AppHost/ServiceDefaults, SQL Server, Functions and local resource wiring. |
| `rules/backend.md` | API host + `.Core` layering and service boundaries. |
| `rules/functions.md` | .NET isolated Functions, Service Bus triggers and timer-triggered outbox relay. |
| `rules/messaging.md` | Events, outbox/inbox, idempotency, correlation and failure semantics. |
| `rules/database.md` | SQL Server/EF Core conventions and anti-PostgreSQL guardrails. |
| `rules/gateway.md` | YARP sole-edge conventions. |
| `rules/identity.md` | ASP.NET Core Identity boundaries and unresolved ownership/session decisions. |
| `rules/frontend.md` | React 19 + PCDS design-system conventions. |
| `rules/audit.md` | Conditional audit bounded-context conventions; does not authorize creating Audit. |
| `skills/add-endpoint/` | HTTP vertical-slice procedure preserving Controller → Facade → Business → Data → Repository. |
| `skills/add-function-trigger/` | Procedure for adding Service Bus or timer triggers. |
| `skills/add-integration-event/` | End-to-end publish/consume event procedure with outbox/inbox. |
| `skills/add-component/` | React 19 + PCDS UI procedure. |
| `skills/add-aspire-resource/` | Resource and deployable-project wiring procedure. |
| `skills/add-audit-event/` | Conditional audit event procedure. |
| `skills/trace-a-request/` | Read-only investigation procedure using correlation/telemetry/audit surfaces. |
| `agents/code-reviewer.md` | Read-only architecture/quality review. |
| `agents/test-gap-analyzer.md` | Read-only test-gap review, including Functions/idempotency. |
| `agents/api-contract-checker.md` | Read-only public API ↔ React and event ↔ Function contract drift check. |
| `agents/function-boundary-checker.md` | Read-only check for hosted workers, Function domain logic, wrong DB/bus wiring. |
| `agents/audit-coverage-checker.md` | Conditional read-only audit coverage check. |
| `hooks/format.sh` | Post-edit formatter for .NET and web files. |
| `hooks/secret-guard.sh` | Pre-edit credential-shaped-string guard. |

## Rule of thumb

- `CLAUDE.md` / rule = something Claude must **know and obey**.
- Skill = a repeatable procedure Claude should **follow**.
- Agent = analysis Claude should **delegate** to keep the main context focused.
- Hook = deterministic behavior that should **happen regardless of model judgment**.

## After adding to a repository

```bash
chmod +x .claude/hooks/*.sh
```

Then start Claude Code from the repository root and confirm the root `CLAUDE.md` is loaded and the agents/skills are visible.

## Local-only files

Do not commit:

- `.claude/settings.local.json`
- `local.settings.json` containing Function secrets
- `.env.local`/equivalent secret-bearing frontend files
- any `*.local.*` project override containing credentials

## Fast-moving APIs

Before generating version-sensitive code, verify official documentation for:

- Aspire Azure Functions integration
- Aspire SQL Server hosting/EF Core integration
- Azure Functions isolated worker packages and bindings
- Azure Service Bus trigger/binding extension
- Claude Code hook/settings/frontmatter syntax
- React/Tailwind/Vite patterns if dependencies have moved materially
- Azure Functions Flex Consumption limitations/deployment behavior
- ASP.NET Core Identity security/configuration APIs

This toolkit intentionally describes architectural intent more strongly than exact package versions.

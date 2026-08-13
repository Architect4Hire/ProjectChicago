# Why Project Chicago Is Built This Way

*Or: why a lightweight CRM has six service boundaries, three projects per service, a hand-rolled outbox, and more architectural guardrails than business entities*

Project Chicago manages three things:

```text
Clients → Projects → Tasks
```

That is not a complicated domain. It would fit comfortably in one ASP.NET Core application, one database, and a few React pages. I know that because I could have built exactly that.

I did not.

Here is the sentence I want you to hold onto through this document:

**Project Chicago spends complexity on evidence, not on business features.**

The service boundaries create ownership evidence. The outbox and inbox create delivery evidence. OpenTelemetry creates causal evidence. The Audit service creates business evidence. `CLAUDE.md`, skills, hooks, and SCRUB prompts create evidence that an AI-assisted change was constrained, reviewed, and tied back to a requirement.

That is the architecture. Everything else is implementation detail.

## First, the honest version of what exists

This repository is documentation-first and built in ordered microsteps. So there are three different things a document might mean when it says “Project Chicago does X”:

1. a requirement or ADR says the system **must** do it;
2. `CLAUDE.md` and `.claude/` constrain future work so it **will** be done that way;
3. code and tests prove it **does** it today.

Those are not interchangeable.

At HEAD, the code proves one bounded service: CRM. It has the thin HTTP host, layered `.Core`, SQL Server persistence, Client create/list/detail/lifecycle behavior, atomic audit-event outbox writes, and a timer-triggered Azure Function that relays the outbox. Shared projects contain the envelope, outbox/inbox, Service Bus publishing, error, correlation, and telemetry mechanisms. Aspire composes the local SQL Server, `CrmDb`, Service Bus emulator, CRM API, CRM Functions app, gateway, and Vite client. The React application contains the local design system source and an authenticated-shell placeholder.

Identity, Audit, Notification, Search, and Workflow do not exist as deployable projects yet. The gateway has correlation middleware but no YARP proxy routes. There is no complete browser-to-Audit journey to demo.

That is not a disclaimer buried at the bottom. It is part of the design. A reference architecture loses its value the moment a diagram starts impersonating running code.

## Why six services for three nouns?

The target catalog is CRM, Identity, Audit, Notification, Search, and Workflow. CRM permanently owns Clients, Projects, and Tasks. The other services own capabilities whose data and failure modes should not quietly collapse into CRM's database.

Every bounded service gets:

```text
ProjectChicago.<Service>/             # thin HTTP host
ProjectChicago.<Service>.Core/        # behavior and persistence
ProjectChicago.<Service>.Functions/   # asynchronous entry points
```

And every service owns exactly one Microsoft SQL database.

Here is the contrarian bit: **a modular monolith would be the cheaper product architecture today.** Six services are not required to make Clients, Projects, and Tasks work. If the goal were to ship a disposable CRM as quickly as possible, I would not defend this topology.

But Project Chicago is a reference build about disciplined distributed delivery. Database-per-service makes ownership impossible to hand-wave. CRM cannot join Identity tables because there is no shared `DbContext`. Search cannot become a grab bag of cross-database queries. Audit cannot be a trigger writing into a table everybody owns. Cross-service work has to travel through a stable HTTP contract or a versioned integration event.

That costs deployables, eventual consistency, and operational surface area. What it buys is one answer to the question that matters when a system changes: **who owns this fact?**

Aspire keeps that decision from making local development miserable. One local SQL Server resource can host several named databases while ownership stays separate. Today the AppHost creates only `CrmDb`, because CRM is the only implemented service. The other databases arrive with their owners, not as speculative empty schemas.

## The five-layer pipeline is not magic onion dust

Inside `.Core`, Project Chicago uses this straight-line call chain:

```text
Controller / Function → Facade → Business → Data → Repository → DbContext
```

The controller or Function binds transport and delegates. The Facade validates and orchestrates the use case. Business decides what the domain change means and which integration fact it emits. Data owns the transaction. Repository owns persistence operations. `DbContext` owns EF Core mapping and the unit of work.

This is where architecture discussions get sloppy, so let me be precise. The folders inside `.Core` are a layered pipeline. They are not six independently swappable architectural rings. The compiler-enforced boundary is one level up:

```text
Contracts ← Shared ← <Service>.Core ← HTTP host / Functions ← AppHost
```

One service has no project-reference path into another service's Core. `Contracts` contains versioned integration facts, not a communal domain model. `Shared` contains mechanism, not CRM behavior. The hosts are composition and transport, not alternate business layers.

So what earns the extra layers inside Core? Testability and legibility. A Client lifecycle rule can be tested without SQL. A controller test proves HTTP mapping without retesting domain policy. A Data test proves the business row and outbox fact commit together. The current test projects follow those seams; the layering is not just a diagram in `CLAUDE.md`.

The cost is translation and files. The payoff is that when an AI agent adds a feature, there is an obvious place for each decision—and an obvious review finding when it puts one in the wrong place.

## Two front doors, one Core

Project Chicago keeps synchronous and asynchronous entry points deliberately different at the edge and deliberately identical underneath.

HTTP requests enter through ASP.NET Core controllers. Timer and Service Bus work enter through .NET isolated-worker Azure Functions. Both delegate to the owning service's Core. A Function trigger is an adapter, not a second application architecture.

That is why `BackgroundService` and `IHostedService` consumers are explicitly banned from API projects. An always-running processor hidden inside an HTTP host muddies deployment, scaling, credentials, and failure ownership. Project Chicago puts asynchronous workloads in sibling Function apps and targets Azure Functions Flex Consumption in production.

The implemented `RelayOutboxFunction` shows the line clearly. It reads configuration, creates relay options, calls `IOutboxRelay`, and logs the result. It does not contain SQL polling, Service Bus branching, or CRM rules. The CRM API receives `CrmDb`; the Functions app receives `CrmDb` and messaging because it is the process that needs both. Least privilege is visible in the Aspire resource graph.

More deployables are not free. But a timer trigger and an HTTP request are two different front doors onto one behavior stack, not excuses to build two stacks.

## The outbox is the load-bearing part

A naive Client mutation does this:

```text
save Client → publish event
```

Two writes. Two systems. A crash in the middle. Now the CRM says the Client exists while every downstream consumer believes it does not.

Project Chicago saves the domain change and an outbox row in the same SQL transaction. A timer-triggered Function leases pending rows, publishes them to Azure Service Bus, and marks them dispatched only after the broker confirms the send. A consumer records inbox state so a redelivered message does not repeat the business effect.

Here is the honest guarantee: **the system is at-least-once, not exactly-once.**

A crash after send and before the dispatch mark can send the same fact twice. Service Bus can redeliver. A Function can retry. Correctness lives in deterministic message identity and idempotent handling, not in pretending duplicates can be prohibited across SQL and a broker.

The pattern adds a table, a relay, leases, retry behavior, dead-letter operations, and lag. It earns that complexity by solving the dual-write failure without distributed transactions.

Business audit rides the same durable path. The CRM Client slice already creates an `EntityMutationAudited` fact and stores it with the mutation. The planned Audit service consumes that fact into an append-only database. CRM never reaches across the boundary and writes `AuditDb` directly.

Technical logs tell me that code ran. Audit evidence tells me who changed what, when, from what to what, through which process. Those are different systems because they answer different questions.

## YARP is one door, not one brain

The React client is designed to know one backend address: `ProjectChicago.Gateway`. YARP will expose stable public routes and resolve internal services through Aspire and configuration. React never calls a service host or Function endpoint directly.

The gateway owns genuinely edge-wide work: routing, correlation, and broad authenticated-edge policy where appropriate. It does not own CRM authorization. A proxy can establish that a caller is authenticated; only the service that owns a Client can decide whether that caller may perform a particular transition.

One edge costs another hop and another component to operate. It buys a stable browser contract and keeps internal topology out of frontend code.

And, again, say where the pattern ends: the repository has the gateway host and correlation middleware today. It does not yet have YARP routes. “YARP-only edge” is a confirmed architectural rule, not a claim about current runtime behavior.

## Identity has an owner because credentials are not CRM data

Project Chicago uses ASP.NET Core Identity for password hashing, users, roles and claims, lockout, reset and confirmation tokens, and the account-security mechanics the framework already knows how to implement.

The target Identity service owns that store. CRM does not get a clever custom password table because somebody happened to need login during a Client feature.

This leaves real decisions open on purpose: cookie or token transport, session and refresh behavior, MFA/passkeys, external providers, and recovery policy. Those choices affect the security architecture and deserve their own decision. They should not sneak into the repository as side effects of a UI prompt.

The Identity service is not implemented yet. What exists is a hard ownership decision and a refusal to counterfeit framework security in domain code.

## Aspire makes the diagram executable

`ProjectChicago.AppHost` is not a dumping ground for setup logic. It declares resources and relationships: SQL Server, `CrmDb`, the Service Bus emulator, the shared event topic and Audit subscription, CRM API and Functions, gateway, and Vite client.

That graph answers questions a box diagram usually avoids:

- Which process receives a database connection?
- Which process receives Service Bus credentials?
- What has to be healthy before another workload starts?
- What does a developer actually run locally?

`ProjectChicago.ServiceDefaults` supplies the common runtime baseline—service discovery, resilience, health endpoints, and OpenTelemetry—without moving business logic into composition.

The benefit is local parity and visible dependencies. The tradeoff is another framework surface whose APIs move quickly enough that version-sensitive work has to be verified against current documentation. `CLAUDE.md` calls that out explicitly because “I remember how Aspire worked last year” is not a deployment strategy.

## OpenTelemetry is how the system tells the truth under failure

The operational requirement is not “we have logs.” It is this:



> Start with a support identifier and follow one operation from the browser edge through HTTP, SQL, outbox relay, Service Bus, consuming Function, and downstream persistence.



OpenTelemetry provides the common instrumentation model. W3C trace context connects spans. Correlation IDs group the operation. Causation IDs explain which durable fact produced the next one. Event and message IDs let an operator separate “committed but not published” from “published but not consumed.” Azure Monitor/Application Insights is the production view; Aspire provides the local dashboard.

Instrumentation costs engineering time and telemetry storage. Async trace propagation has sharp edges. Project Chicago pays that cost while the seam is built, because observability bolted on after an incident is usually a collection of unrelated timestamps pretending to be a story.

Shared telemetry configuration and tests exist now. Cradle-to-grave tracing through services that do not exist yet obviously does not.

## The design system is a guardrail against generated visual entropy

AI can generate a polished page quickly. It can also generate twelve slightly different buttons across twelve polished pages.

Project Chicago keeps the Project Chicago Design System under `src/web/design-system/` as the authoritative local source. Features compose its primitives, recipes, semantic tokens, layout, themes, and accessibility behavior. They do not invent a second design system in feature folders or paste a new Tailwind recipe every time a prompt needs a card.

Vendoring the design system makes the visual contract reproducible and reviewable with the application. It also means upgrades are deliberate and drift is our responsibility. That is an honest cost. The alternative is drift with no owner.

The local drop-in and authenticated shell exist. The complete CRM screens do not.

## The AI tooling is part of the architecture

The `.claude/` directory is not prompt decoration. It is a repository-owned control plane for delivery:

| Mechanism         | What it controls                                                                                                               |
| ----------------- | ------------------------------------------------------------------------------------------------------------------------------ |
| `CLAUDE.md`       | Enduring architecture and the shortcuts an agent may not take                                                                  |
| `.claude/rules/`  | Path-specific policy for backend, data, Functions, messaging, gateway, identity, frontend, audit, Aspire, and related concerns |
| `.claude/skills/` | Repeatable procedures for endpoints, events, Functions, migrations, Aspire resources, UI, tracing, and quality gates           |
| `.claude/agents/` | Read-only review perspectives for security, contracts, boundaries, audit, accessibility, integrity, and test gaps              |
| `.claude/hooks/`  | Deterministic formatting and secret-shaped-write protection                                                                    |

The division is deliberate. A model handles judgment. A hook handles a credential-shaped string. A skill makes a recurring workflow inspectable. A reviewer looks for violations without silently rewriting the change it is supposed to evaluate.

There is some archaeology in the toolkit. Several vendored skills retain generic CRM or Angular ancestry even though Project Chicago uses React. `VENDORED.md` records provenance, and repository-specific instructions override the inherited assumptions. That is a useful warning: prompts and skills accumulate maintenance debt exactly like source code. If an instruction no longer describes the repository, it is a bug with unusually persuasive output.

## SCRUB makes “stop” a delivery feature

SCRUB means **Scope, Constraints, Restrictions, Usage, Behavior**. The canonical implementation sequence contains 164 ordered micro-prompts tied to requirement IDs.

Each prompt changes one seam or proves one fact. It names the relevant rules and skills. It excludes adjacent work. It defines the verification. And it says where the agent must stop.

That stop condition is the part most AI-assisted workflows leave out. If a change would invent an authentication strategy, move data ownership, alter a public contract, or change deployment topology, Project Chicago does not reward the agent for making a plausible guess. It makes the decision visible.

The rhythm is simple:

```text
decide → record → implement one seam → prove it → review it → stop
```

That is slower than “build me a CRM” in the first hour. It is much faster than finding six architectural decisions hiding inside a 90-file pull request.

## The practical version

If you borrow anything from this architecture, borrow these:

**Name the implementation frontier out loud.** A selected technology is not a configured technology. A diagram is not a deployed service. Documentation stays credible only while it distinguishes those things.

**Put ownership where the compiler and resource graph can see it.** Database-per-service, project references, and narrow Aspire resource references are stronger than a paragraph asking developers to behave.

**Design for duplicates, not fantasies.** Outbox plus inbox gives durable at-least-once delivery. Say that plainly and make idempotency load-bearing.

**Use one behavior stack behind every entry point.** Controllers and Functions bind different transports. They should not create different business architectures.

**Treat agent instructions as production assets.** Version them, review them, specialize them, and delete stale assumptions. A confident obsolete skill is worse than no skill.

**Spend complexity only where it produces evidence.** Six services for a small CRUD app would be theater. Six explicit owners in a reference build about failure, audit, observability, and AI delivery are the lesson.

Project Chicago is not interesting because it has more architecture than Clients, Projects, and Tasks strictly need. It is interesting because every extra piece has to answer one question: **what does this let us prove?**

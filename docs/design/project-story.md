# The Repository Is the Prompt: Building Project Chicago So AI Cannot Quietly Redesign It

*Or: why the most important AI-assisted architecture in this CRM lives outside `src/`*

TL;DR — Project Chicago is a .NET 10, Azure, and React reference build for a lightweight CRM. The interesting part is not that Claude can generate a Controller, a Function, or an outbox. The interesting part is that the repository tells Claude where each one belongs, which shortcuts are forbidden, how small the change must be, which reviewer inspects it, and when the correct answer is to stop.

Here is the sentence I want you to keep for the whole article:

**The repository is not merely where AI-generated code lands. The repository is the control system that decides what the AI is allowed to build.**

That control system is `CLAUDE.md`, `.claude/rules`, `.claude/skills`, `.claude/agents`, `.claude/hooks`, ADRs, requirements, traceability matrices, and 164 SCRUB micro-prompts. The source code is the output. The architecture is the machinery that makes the output reviewable.

## The one small domain that exposes the whole problem

Project Chicago manages Clients, Projects, and Tasks.

```text
Clients → Projects → Tasks
```

You can fit the nouns on one line, which is exactly why the project works as a reference build. There is nowhere for architecture to hide behind domain complexity.

The target system has six bounded services: CRM, Identity, Audit, Notification, Search, and Workflow. Each service owns one SQL database, one ASP.NET Core HTTP host, one `.Core` implementation, and one Azure Functions project for asynchronous entry points. React sees a single YARP gateway. Service Bus carries integration facts. Transactional outboxes make publication durable. Inboxes absorb redelivery. OpenTelemetry carries the causal story. Aspire makes the local topology runnable. PCDS keeps the interface from becoming a gallery of unrelated generated components.

That sounds like a lot of machine for three nouns. It is.

But the CRM is not the only thing being demonstrated. Project Chicago is testing whether AI-assisted delivery can preserve ownership, failure semantics, auditability, and architectural intent across dozens of small changes.

## `CLAUDE.md` starts by banning the convenient mistakes

Open `CLAUDE.md` and the first surprise is what it does not spend much time on. There is very little “prefer this naming style” material. The file is mostly architecture and refusal.

Do not share a database. Do not introduce PostgreSQL into a SQL Server design. Do not let React call an internal service. Do not put Service Bus consumers in API-hosted background services. Do not publish a transactional event directly from Business or Repository. Do not turn a Function trigger into a second business layer. Do not replace ASP.NET Core Identity as a side effect. Do not rebuild Project Chicago Design System inside a feature folder.

Those restrictions are the negative space of the system. They identify the exact shortcuts a capable coding model will employ when asked to quickly make a test pass.

The file is written using SCRUB:

- **Scope** says what Project Chicago is and which boundaries are in play.
- **Constraints** define the runtime, service shape, layering, messaging, data, identity, gateway, UI, observability, and test model.
- **Restrictions** name the moves that are never locally convenient enough to justify.
- **Usage** tells the agent what to inspect before touching code.
- **Behavior** defines how to proceed when facts are missing, or decisions are still open.

This is project memory with teeth. A new session does not get to rediscover the architecture from filenames and make a different guess.

## The architecture is split across the same kinds of boundaries as the code

One giant system prompt would be easy to write and terrible to maintain. Project Chicago separates the AI tooling by responsibility:

```text
CLAUDE.md        enduring architecture
rules/           policy loaded for relevant paths
skills/          repeatable implementation procedures
agents/          read-only specialist review
hooks/           deterministic safeguards
```

That split is doing the same job as the application architecture. Stable policy stays central. Context-specific policy activates near the work. Procedures have names and completion criteria. Reviewers are separate from implementers. Mechanical checks do not depend on judgment.

The backend rule explains the Facade → Business → Data → Repository stack. The Functions rule keeps triggers thin and asynchronous. Messaging defines outbox, inbox, retry, and settlement behavior. Database rules preserve SQL Server and service ownership. Gateway rules keep YARP as the only browser edge. Identity rules keep credential mechanics in ASP.NET Core Identity. Frontend and accessibility rules make PCDS and usable interaction part of “done,” not polish for later.

There are specialist agents for code review, API contracts, Function boundaries, audit coverage, security, accessibility, lifecycle integrity, data integrity, and test gaps. They are read-only. That matters. A reviewer that fixes its own findings while reviewing can make the evidence disappear under it.

The hooks cover the boring, high-value end. One formats touched files. One rejects writes containing high-signal credential shapes. Claude can reason about whether a token belongs in a log; a shell guard can reject a private-key-shaped value every time without getting tired.

## Skills are where architecture becomes a repeatable move

“Follow the architecture” is not a procedure.

Project Chicago's skills turn recurring changes into named, source-controlled moves. `add-endpoint` walks one use case through contract, Controller, Facade, Business, Data, Repository, persistence, telemetry, and focused proof. `add-function-trigger` keeps transport binding in Functions and behavior in Core. `add-integration-event` forces the change to account for both sides of the broker, the envelope, outbox, inbox, topology, retries, and compatibility. `add-aspire-resource` makes resource ownership explicit. `trace-a-request` reconstructs a failure without rewarding somebody for querying every service database directly.

The important part is not that a skill tells Claude which files to create. It tells Claude what **not** to collapse.

An integration event is not finished when a record type compiles. A Function is not finished when the trigger fires. An endpoint is not finished when the controller returns 200. Each procedure carries the architecture's definition of completeness into the smallest unit of work.

There is honest maintenance debt here too. Some vendored skills carry generic CRM or Angular language from their ancestry, while Project Chicago's client is React. `VENDORED.md` records where they came from, and local instructions narrow them. That is not a minor housekeeping footnote. AI instructions rot. A stale skill still sounds authoritative, which makes it more dangerous than a stale comment.

## SCRUB changes what “make progress” means

The canonical prompt plan contains 164 micro-prompts mapped to requirement IDs. Run one. Prove it. Stop.

Every prompt uses the same five-part frame:

```text
SCOPE:       the one artifact or outcome allowed to move
CONSTRAINT:  architecture, invariants, stack, and proof
RESTRICTION: adjacent work and shortcuts excluded
USAGE:       rules, skills, requirements, and examples to inspect
BEHAVIOR:    the observable result and stop condition
```

The sequence starts with architecture gates because architecture decisions should not arrive disguised as implementation details. Later prompts add one seam at a time: a contract, a mapping, a model, a configuration, a transaction, an endpoint, a Function, a UI path, a verification.

This is deliberately less cinematic than “implement the feature end to end.” It is also much easier to review.

Here is the contrarian bit: **an AI agent that stops at an unresolved decision is making progress.**

If authentication transport is unsettled, choosing cookies because the sample was convenient is not initiative. If a change needs a new service, inventing one is not architecture. If a migration would destroy historical meaning, generating it successfully is not completion.

SCRUB makes “stop and surface the decision” an expected output, not a failure mode.

## The code path that proves the method

The current Client mutation is where the documents stop being theory.

An HTTP request enters the CRM controller and delegates into the Facade. The Facade owns use-case validation and orchestration. Business owns the domain decision and creates an `EntityMutationAudited` integration fact. Data composes the entity write and outbox enqueue. Repository and `CrmDbContext` perform SQL Server persistence.

The load-bearing part is the transaction:

```text
Client change + audit integration fact → one CrmDb transaction
```

Nothing publishes to Service Bus on the request path. The CRM Functions project has a timer-triggered `RelayOutboxFunction`. It binds configuration, constructs options, delegates to the shared `IOutboxRelay`, and logs the outcome. The shared relay leases rows, publishes, and settles them. The API host never receives Service Bus credentials; the Functions app does because Aspire's resource graph says it needs them.

This one slice demonstrates the larger method:

1. the requirement says mutations must be auditable;
2. the ADRs define durable event-driven audit;
3. `CLAUDE.md` forbids direct Audit database writes and direct request-path publication;
4. rules define the layer and messaging boundaries;
5. skills describe the implementation path;
6. focused tests prove Business, Data, Repository, outbox, API, and Function behavior separately.

The tooling did not generate architecture from vibes. The architecture narrowed the generator until the correct implementation path was the easy path.

## The honest version of the distributed architecture

Project Chicago uses the transactional outbox because saving SQL state and publishing to Service Bus are two operations that can fail independently. The domain row and outbox row commit together. A Function sends later. A consumer uses an inbox to make redelivery a no-op.

The guarantee is at-least-once. Never exactly-once.

If the relay sends and crashes before it marks the row dispatched, the message can be sent again. Service Bus can redeliver. Functions can retry. The design does not eliminate duplicates; it gives them one identity and makes every receiving boundary responsible for handling them safely.

That is more infrastructure than a direct broker call. It is also an architecture that continues telling the truth when a process dies between two lines of code.

Audit uses those same durable facts because logs and audit are not the same system. A trace can tell an operator where time went. An audit record has to tell the business who changed what, when, and through which process. The target Audit service owns an append-only database and consumes integration events; source services do not write into it.

And here is where the pattern currently ends: CRM produces the audit fact. The Audit service and consumer are not implemented yet. The repository describes the complete path, but HEAD proves only the producer side.

## One front door and one identity owner

The browser is designed to call YARP and nothing behind it. The gateway gives the frontend a stable public surface, establishes correlation, and owns truly edge-wide policy. Resource authorization remains inside the service that owns the resource.

ASP.NET Core Identity owns password hashing, credentials, lockout, users, roles and claims, reset and confirmation tokens, and the mechanics nobody should rewrite in a CRM Business class. A dedicated Identity service owns that database.

Both choices remove ambiguity. The browser has one backend door. Credentials have one owner.

Neither choice is fully implemented today. The gateway exists and correlation middleware is tested, but YARP routes are not configured. The Identity service remains planned. Naming that boundary is not underselling the project. It is the difference between an architecture case study and marketing copy.

## Aspire and OpenTelemetry make the failure story visible

The Aspire AppHost declares the local topology: one persistent SQL Server, `CrmDb`, the Service Bus emulator, the shared event topic and Audit subscription, CRM API and Functions, gateway, and Vite client.

Look at the references and the security model becomes visible. CRM gets its database. CRM Functions gets the database and broker. Gateway gets neither. AppHost contains composition, not business behavior.

`ProjectChicago.ServiceDefaults` applies service discovery, resilience, health endpoints, and OpenTelemetry conventions. W3C trace context, correlation IDs, causation IDs, and durable event/message IDs are meant to carry one operation across HTTP, SQL, outbox, Service Bus, Functions, and downstream persistence.

That lets an operator distinguish five failures that logs often flatten into one:

- the request never committed;
- it committed but the outbox did not relay;
- it relayed but the consumer did not run;
- the consumer ran and retried;
- the business action succeeded while a downstream projection lagged.

Observability is not a dashboard screenshot. It is the ability to tell those stories apart.

## The design system keeps AI from inventing a new product on every page

Generated UI has a predictable failure mode: every page looks reasonable by itself and unrelated to the page before it.

Project Chicago keeps PCDS as an authoritative local source tree under `src/web/design-system/`. React features are expected to compose its primitives, recipes, semantic tokens, layout, theme behavior, and accessibility patterns. They do not get to create a parallel button system because a prompt asked for a form.

The local copy makes the design contract reproducible and reviewable. It also creates deliberate upgrade work. Good. Drift should have an owner.

The repository currently proves the local PCDS drop-in and an authenticated shell placeholder, not the finished CRM experience. Again: say where the pattern ends.

## What the portfolio actually demonstrates

At HEAD, Project Chicago contains:

- a .NET 10 solution with CRM, Contracts, Shared, Gateway, AppHost, ServiceDefaults, and focused test projects;
- Client create, list, detail, and lifecycle behavior across the full CRM layer stack;
- SQL Server persistence and SQL-specific integration tests;
- event envelopes, outbox/inbox models, Service Bus publishing, and relay behavior;
- a timer-triggered CRM Azure Function that delegates into shared relay logic;
- Aspire resources for SQL, messaging, CRM workloads, gateway, and React;
- gateway correlation and shared OpenTelemetry configuration;
- the local design system source and initial application shell;
- a source-controlled Claude engineering toolkit and a requirement-linked delivery plan.

It does not yet contain five of the six service deployables, YARP routing, the Audit consumer/database, or complete frontend journeys.

That line between “designed” and “proved” is the most portfolio-worthy thing in the repository. AI can generate a large amount of plausible source. Architectural judgment shows up in what you refuse to claim until the evidence exists.

## The practical version

Stealing this approach for your own build:

**Write down the shortcuts, not just the destination.** “Use Service Bus” is weak. “Do not publish on the request path; persist an outbox fact in the owning transaction” changes code.

**Make instructions obey single responsibility.** Project memory, path rules, procedures, reviewers, and deterministic hooks are different tools. Do not bury all five in one prompt.

**Make the correct stopping point explicit.** Architecture, security, contracts, data ownership, and topology are decision gates. Reward the agent for surfacing them.

**Keep the unit of change smaller than the model's confidence.** One seam, one proof, one review. Large generated diffs are not leverage if nobody can explain the decisions inside them.

**Treat prompts like code.** Version them. Test their assumptions against the repository. Track provenance. Retire stale framework language. The fact that an instruction is written in Markdown does not make it harmless.

**Say where every pattern ends.** YARP is selected but not routed. Identity is owned but not built. CRM emits audit facts but Audit does not consume them yet. Honesty is an architecture feature.

The lesson from Project Chicago is not that Claude can build a distributed CRM. Of course it can produce the files.

The lesson is that a repository can carry enough architectural judgment that the next generated file has somewhere correct to belong—and enough evidence that a reviewer can tell when it does not.

The code is the artifact. The control system is the work.

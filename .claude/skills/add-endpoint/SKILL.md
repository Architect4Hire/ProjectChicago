---
name: add-endpoint
description: Add or extend a REST endpoint in one Project Chicago bounded service while preserving Controller → Facade → Business → Data → Repository, SQL Server persistence, transactional outbox behavior, YARP public routing, React contract compatibility, and tests. Use for any HTTP command/query feature.
---
# Add an HTTP endpoint

Build one vertical use case in the service that owns the data/decision. Do not use this skill to invent a new bounded service.

## 0. Establish ownership before editing

Answer from the existing architecture/code:

- Which bounded service owns the aggregate/data being read or mutated?
- Is this a command or query?
- Does the use case require immediate data from another service?
- Does success create a cross-service fact that should be an integration event?
- Is there already a gateway route/public contract to extend?

If implementing the feature would require another service's database/Core, do not continue by breaking the boundary. Surface the need for a synchronous contract/read model/event or service-boundary decision.

Read:

- `.claude/rules/backend.md`
- `.claude/rules/database.md`
- `.claude/rules/gateway.md`
- `.claude/rules/messaging.md` if the mutation emits an event
- `.claude/rules/frontend.md` if the React client changes

## 1. Define the public contract first

Before repositories/controllers, define the intended gateway-visible contract:

- method + stable public route;
- request body/query/path inputs;
- success status and response shape;
- validation/domain failure statuses and shared error shape;
- authorization expectation;
- idempotency expectation for client retries if relevant.

Do not return an EF entity or service-internal DataModel.

For a mutation, name the resulting business fact(s) in plain past tense before deciding whether an integration event is necessary.

## 2. Service/API model

Create/reuse:

- transport request/ViewModel in the API contract area;
- `.Core` ServiceModel/use-case input that is not tied to HTTP if the operation is also callable from a Function;
- mapper between transport and service model when non-trivial.

Transport model validation catches shape/format. Domain/state rules stay in Business.

## 3. Controller — transport only

Implement the smallest action that:

1. binds request;
2. obtains authenticated actor/tenant/correlation context through established abstractions;
3. maps to service input;
4. calls one Facade method;
5. maps typed result/error to public HTTP response.

Do not:

- inject Repository/DbContext;
- inject `ServiceBusClient`;
- open a transaction;
- implement lifecycle rules;
- query another service.

## 4. Facade — use-case boundary

Add a focused facade method.

Facade may:

- run validator(s);
- normalize safe input;
- check/cache service-owned read-through values;
- coordinate Business methods in the same service;
- invalidate service-owned cache after successful mutations.

Facade must not:

- issue EF queries directly;
- send Service Bus messages;
- become a second repository/data layer;
- call another service's Core.

Return a typed service result rather than using exceptions for expected domain rejection when the codebase has an established result/error pattern.

## 5. Business — rules and event decision

Business owns:

- allowed CRM lifecycle transition(s);
- current-state/business preconditions;
- calculations/decisions;
- translation between service state and persistence requests;
- creation of integration-event fact(s) when other bounded services need to know the committed outcome.

Business does not publish the event. It returns/attaches the event to the Data operation in the project's established style.

For an event, define the contract in `ProjectChicago.Contracts` and then follow `add-integration-event` for the full seam.

## 6. Data — transaction boundary

Data composes repositories and executes the service-owned transaction.

For a mutating operation:

```text
BEGIN SQL TRANSACTION
  read/update through repositories as required
  persist domain/data changes
  add OutboxMessages row(s) for integration facts
COMMIT
```

Rules:

- state and outbox succeed/fail together;
- do not publish to Service Bus inside the transaction;
- use the owning service DbContext only;
- if no event is needed, do not create one merely for ceremony;
- preserve cancellation tokens.

## 7. Repository / EF Core SQL Server

Add only the persistence operation required by the use case.

- Use SQL Server-compatible EF mappings/query APIs.
- Bound text lengths and decimal precision intentionally.
- Add indexes only for concrete query patterns/invariants.
- Add rowversion/concurrency only where concurrent edits need protection; handle/test conflict.
- Avoid N+1 query patterns and over-eager full-aggregate loading.
- No Npgsql/Postgres annotations or SQL.

If schema changes, generate/review the migration in the owning `.Core` project using the solution's chosen SQL Server migration startup convention.

## 8. Gateway route

If this is a new public route:

- expose it through YARP under the stable Project Chicago API path;
- route to the owning API project via configured resource discovery;
- do not expose an internal port/host to the browser;
- preserve correlation/auth edge behavior.

If the route already maps a broad service prefix, verify rather than add redundant routing.

## 9. React contract (only when feature reaches UI)

Update the typed gateway API module and public TypeScript model.

- Do not call internal service addresses.
- Do not duplicate an internal `.Core` ServiceModel blindly; mirror the public API contract.
- Render loading/empty/error/success states with PCDS patterns.
- Use `add-component` for new UI composition.

## 10. Tests

### Core/unit

- validator accepts/rejects expected input;
- business allows/rejects key state transitions;
- mapper preserves required data;
- cache behavior if introduced.

### SQL integration

For mutation/persistence-sensitive paths:

- repository query against SQL Server;
- transaction rollback;
- state + outbox atomicity;
- unique/concurrency behavior if applicable.

### API

- correct route/status/body;
- invalid request maps to correct error;
- domain rejection maps correctly;
- authorization behavior;
- correlation if part of edge contract.

### Messaging

If an event is produced, follow `add-integration-event` tests. Do not claim the feature complete after testing only the controller.

## 11. Review pass

Run/delegate:

- `api-contract-checker`
- `test-gap-analyzer`
- `code-reviewer`
- `function-boundary-checker` if an event seam changed

## Completion checklist

- [ ] Owning service is explicit.
- [ ] Controller is transport-only.
- [ ] Facade validates/orchestrates only.
- [ ] Business owns rules/event decision.
- [ ] Data owns transaction.
- [ ] Repository uses only owning SQL Server DbContext.
- [ ] State + outbox are atomic when event emitted.
- [ ] No direct Service Bus send from request path.
- [ ] Public route is through gateway.
- [ ] React public contract is synchronized when affected.
- [ ] SQL Server migration reviewed when schema changed.
- [ ] Focused tests cover failures, not only happy path.

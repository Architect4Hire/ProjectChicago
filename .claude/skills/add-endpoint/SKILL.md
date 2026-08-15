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

Create/reuse two named shapes per use case, both living in the owning `.Core` project (e.g.
`<Service>.Core/Contracts/<Area>`) rather than the HTTP host project, so Business can reference
them directly without creating a Core→API-host reference cycle:

- a **ViewModel** for the inbound shape (e.g. `Create<Aggregate>ViewModel`) — what the controller
  binds the request body/query/route to;
- a **ServiceModel** for the outbound shape (e.g. `<Aggregate>ServiceModel`) — what the controller
  returns as the HTTP response body.

Both are plain DTOs with the transport-level `[JsonPropertyName]`/`DataAnnotations` attributes
(api-contracts.md). There is no separate Facade-only or Business-only request/result type layered
on top of them — Controller, Facade, and Business all pass the same ViewModel in and the same
ServiceModel out. `IFacade`/`IBusiness` method signatures are written directly against these two
types (e.g. `Task<XServiceModel> CreateAsync(CreateXViewModel request, ...)`), not against an
internal DataModel or a wrapper record.

Transport model validation (`[Required]`, `[StringLength]`, `[EnumDataType]`, etc. on the
ViewModel) catches shape/format. Domain/state rules stay in Business.

## 3. Controller — transport only

Implement the smallest action that:

1. binds the request body/query/route to the ViewModel;
2. calls one Facade method, passing the bound ViewModel straight through;
3. wraps the returned ServiceModel in the right `ActionResult` (`Created`, `Ok`, etc.) or maps a
   thrown exception to the standard Problem Details shape.

**Documentation:**
Use standard XML summary comments that describe the endpoint's purpose, parameters, and response
codes — for example:

```csharp
/// <summary>
/// Create a new client. Requires Clients.Write authorization (SEC-010..013).
/// </summary>
/// <param name="request">Client creation data</param>
/// <param name="cancellationToken">Cancellation token</param>
/// <response code="201">Client created</response>
/// <response code="400">Validation error</response>
/// <response code="401">Not authenticated</response>
/// <response code="403">Not authorized (requires Clients.Write)</response>
```

Do not embed verbose inline comments above methods explaining authorization or architectural
layers — the architecture and attributes (below) already document that context.

**Authentication and authorization:**
- Apply `[RequireAuthentication]` at the class level to ensure all actions require an authenticated
  user. This filter returns 401 Unauthorized if `User.Identity.IsAuthenticated` is false.
- Apply `[Authorize(Policy = "...")]` (ASP.NET Core's built-in) on the class or individual actions
  for fine-grained policy authorization (e.g., role-based). Policy failures surface as 403 Forbidden
  through the registered `ApiExceptionHandler`.

The controller never constructs a service input from the ViewModel's fields, and never reads the
ServiceModel's fields to build a different response shape — it passes the ViewModel in and the
ServiceModel out completely unchanged. No field-by-field mapping code, and no mapping extension
method call, belongs in the controller.

Do not:

- inject Repository/DbContext;
- inject `ServiceBusClient`;
- open a transaction;
- implement lifecycle rules;
- query another service;
- map ViewModel fields into anything, or read ServiceModel fields to build anything — the
  controller only forwards and wraps;
- perform manual `if (User.Identity is not { IsAuthenticated: true })` checks — use
  `[RequireAuthentication]` attribute instead.

## 4. Facade — use-case boundary

Add a focused facade method with the exact same signature shape as the Business method it calls:
`Task<XServiceModel> CreateAsync(CreateXViewModel request, ...)`.

Facade may:

- run validator(s) against the ViewModel (`System.ComponentModel.DataAnnotations.Validator`, or an
  equivalent);
- resolve actor/correlation/clock context the ViewModel must never carry itself (security.md:
  never accept the actor from ordinary client input) and pass those as separate parameters
  alongside the untouched ViewModel;
- check/cache service-owned read-through values;
- coordinate Business methods in the same service;
- invalidate service-owned cache after successful mutations.

Facade must not:

- issue EF queries directly;
- send Service Bus messages;
- become a second repository/data layer;
- call another service's Core;
- map any ViewModel field into a different shape, or read any field off the Business result to
  build a different response — Facade passes the ViewModel into Business and returns whatever
  Business hands back, unchanged. **All ViewModel↔domain↔ServiceModel mapping lives in Business,
  not Facade** — Facade's only job around the DTOs is resolving the context Business needs and
  forwarding.

## 5. Business — rules, mapping, and event decision

Business is the only layer that maps. `IBusiness.CreateAsync(CreateXViewModel request, actor,
requestContext, createdAtUtc, cancellationToken)` returns `Task<XServiceModel>` and, inside that
one method:

- unwraps/normalizes the ViewModel's fields (trim, lowercase-email, etc.);
- maps any wire-level enum to the domain enum (throw on an undefined value rather than silently
  defaulting);
- owns allowed lifecycle transition(s), current-state/business preconditions, calculations/
  decisions, and persistence-request translation;
- builds the domain aggregate and persists it (plus outbox/audit facts) through Data;
- maps the persisted domain result back into the ServiceModel before returning.

Keep the ViewModel→domain and domain→ServiceModel translation as small private/internal helper
methods or extension methods colocated in the `<domain-project>/<Area>/Business` folder (e.g.
`<Area>ContractMappingExtensions`) so the translation is unit-testable and readable independently
of the rest of `CreateAsync`, but do not expose a separate public overload that accepts an internal
command/result type instead of the ViewModel/ServiceModel — the ViewModel in, ServiceModel out
signature is the one seam every caller (Facade today, a Function later) uses.

Business also owns creation of integration-event fact(s) when other bounded services need to know
the committed outcome, but does not publish the event — it returns/attaches the event to the Data
operation in the project's established style.

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
- [ ] Controller is transport-only and does no ViewModel/ServiceModel field mapping.
- [ ] Facade validates/resolves context/orchestrates only; it passes the ViewModel to Business unchanged and returns the ServiceModel unchanged.
- [ ] Business's `CreateAsync`-style method takes the ViewModel and returns the ServiceModel directly, owns rules/event decision, and its mapping helpers live in its own folder.
- [ ] Data owns transaction.
- [ ] Repository uses only owning SQL Server DbContext.
- [ ] State + outbox are atomic when event emitted.
- [ ] No direct Service Bus send from request path.
- [ ] Public route is through gateway.
- [ ] React public contract is synchronized when affected.
- [ ] SQL Server migration reviewed when schema changed.
- [ ] Focused tests cover failures, not only happy path.

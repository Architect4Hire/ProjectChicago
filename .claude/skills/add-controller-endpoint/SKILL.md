---
name: add-controller-endpoint
description: >
  Add one ASP.NET Core MVC controller action and its required Controller -> Facade -> Business -> Data
  onion path for the Lifecycle CRM. Enforces strict layer responsibilities, typed models at every seam,
  validation/cache behavior in Facade, translation/business rules in Business, EF Core/SQL Server work in Data,
  stable Problem Details/OpenAPI contracts, concurrency, audit/timeline atomicity, and focused tests.
---

# Add One Controller Endpoint

## Discovery gate

Before changing code, discover the actual solution/project paths, namespaces, target frameworks, package versions, AppHost resource names, SQLDB connection name, DbContext, migrations assembly, test conventions, and feature location. Never treat example names as repository facts. Stop without editing when a required value cannot be proven. Aspire is required and is the supported source of local SQLDB connection information.

## Purpose

Implement exactly one HTTP operation using the mandatory call path:

```text
Controller -> Facade -> Business -> Data
```

This skill is not permission to skip prerequisite microsteps. If a required lower-layer model or operation does
not exist and adding it would make the request more than one primary action, stop and identify the next
microstep instead of inventing a shortcut.

## Non-negotiable architecture

| Layer | Project/location | May call | Must not call |
|---|---|---|---|
| Controller | `<api-project>/Controllers` | Facade interface | Business, Data, EF, cache |
| Facade | `<domain-project>/<Area>/Facade` | Business interface, trusted context/cache abstractions | Data, EF, controllers |
| Business | `<domain-project>/<Area>/Business` | Data interface | Facade, controllers, cache provider, EF |
| Data | `<domain-project>/<Area>/Data` | EF Core/SQL Server | Any upper layer |

Calls may move only one row downward. Models do not cross more than one seam.

## Expected feature anatomy

```text
<api-project>/
├── Controllers/<Area>Controller.cs
└── Contracts/<Area>/<Operation>/
    ├── <Operation>Request.cs
    └── <Operation>Response.cs

<domain-project>/<Area>/
├── Facade/<Operation>/
│   ├── I<Operation>Facade.cs
│   ├── <Operation>Facade.cs
│   ├── <Operation>FacadeRequest.cs
│   ├── <Operation>FacadeResult.cs
│   └── <Operation>Validator.cs
├── Business/<Operation>/
│   ├── I<Operation>Business.cs
│   ├── <Operation>Business.cs
│   ├── <Operation>BusinessRequest.cs
│   └── <Operation>BusinessResult.cs
└── Data/<Operation>/
    ├── I<Operation>Data.cs
    ├── <Operation>Data.cs
    ├── <Operation>DataRequest.cs
    └── <Operation>DataResult.cs
```

Adapt naming to existing conventions, but preserve the four responsibilities and three explicit seams.

## 1. Inspect before editing

Read:

1. Root `CLAUDE.md` and onion/backend/data rules.
2. One comparable controller action.
3. Its Facade, Business, and Data path end-to-end.
4. Existing result/error, Problem Details, validation, cache, current-user, audit, timeline, and transaction patterns.
5. Existing tests for each layer and controller integration tests.
6. OpenAPI/client-generation conventions.

Report the observed pattern and exact files expected to change. Do not introduce minimal APIs, MediatR,
a mapping library, generic repositories, or a new result framework without a separate architecture decision.

## 2. Specify the one operation

State:

- CRM area and controller.
- User outcome.
- HTTP method/route/action name.
- Authentication and coarse policy.
- Record-level rule to be enforced by Facade.
- Request/response contracts.
- Success code and expected 400/401/403/404/409/422 behavior.
- Cache read/write/invalidation behavior.
- Business invariants and model translations.
- Data query/command and transaction boundary.
- Concurrency strategy.
- Audit/timeline requirements.
- Whether OpenAPI or EF model changes.

One invocation implements one action only.

## 3. Define the seam models

Create separate typed models for each boundary:

```text
API Request -> Facade Request -> Business Request -> Data Request
Data Result -> Business Result -> Facade Result -> API Response
```

Rules:

- Never pass API contracts into Domain Business/Data.
- Never pass Facade models directly to Data.
- Never return Data models directly from Facade or Controller.
- Never expose EF entities anywhere above Data.
- Prefer immutable records when consistent with the codebase.
- Model nullability, absence, dates, page limits, and identifiers explicitly.
- Use server-derived actor/tenant/ownership values instead of client-supplied trusted context.

## 4. Implement Data first when missing

Data owns persistence mechanics only.

For queries:

- Accept a Data request model.
- Apply filters in SQL.
- Project directly into a Data result model.
- Apply stable ordering before pagination.
- Use no-tracking for read-only queries.
- Define UTC/date-window semantics.
- Return empty collections, not null.

For commands:

- Load only state required for the write.
- Use an explicit transaction where multiple rows represent one business fact.
- Guard concurrency in the write using version/expected state/conditional update/unique constraint.
- Persist state, lifecycle history, audit, and timeline facts atomically when required.
- Translate known provider failures into typed Data outcomes; do not leak provider exceptions.

Data contains no business-rule decision beyond enforcing persistence invariants and translating database facts.

## 5. Implement Business

Business receives Business models and calls only Data interfaces.

- Translate Business request to Data request explicitly.
- Enforce CRM invariants and lifecycle rules before/after Data as appropriate.
- Interpret typed Data outcomes into domain/business outcomes.
- Translate Data results into Business results.
- Decide expected-state/version values supplied to Data.
- Define audit/timeline facts that Data persists atomically.
- Keep HTTP statuses, controller types, cache calls, claims, and EF Core out of Business.

A read-side existence check alone is not a concurrency strategy. Business must require Data to enforce the
expected write condition.

## 6. Implement Facade

Facade is the use-case boundary visible to controllers.

Order for a cacheable query:

1. Validate request shape and operation context.
2. Enforce record-level authorization using trusted user context.
3. Build a stable cache key from normalized authorized inputs.
4. Return a valid cached Facade result when present.
5. Call Business on cache miss.
6. Translate Business result to Facade result.
7. Cache only successful, safe, non-user-leaking results with approved TTL.

Order for a command:

1. Validate.
2. Enforce record-level authorization.
3. Coordinate idempotency when required.
4. Call Business exactly once unless retry policy explicitly permits otherwise.
5. Translate Business outcome into Facade outcome.
6. Invalidate/refresh affected cache entries only after successful persistence.

Facade must not inject Data, `<db-context>`, `DbSet`, or EF repositories. Facade validators may perform
contextual validation through Business query operations, never by reaching into Data.

## 7. Implement the Controller

Use MVC attributes and typed `ActionResult<T>`.

Representative shape:

```csharp
[ApiController]
[Route("api/accounts")]
public sealed class AccountsController : ControllerBase
{
    private readonly ICreateContactFacade _createContactFacade;

    public AccountsController(ICreateContactFacade createContactFacade)
        => _createContactFacade = createContactFacade;

    [HttpPost("{accountId:guid}/contacts", Name = "CreateContact")]
    [Authorize(Policy = Policies.ContactsWrite)]
    [ProducesResponseType<CreateContactResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CreateContactResponse>> CreateContact(
        Guid accountId,
        [FromBody] CreateContactRequest request,
        CancellationToken cancellationToken)
    {
        var facadeRequest = CreateContactHttpMapper.ToFacade(accountId, request);
        var result = await _createContactFacade.ExecuteAsync(facadeRequest, cancellationToken);

        return result.Match<ActionResult<CreateContactResponse>>(
            success => CreatedAtAction(
                nameof(GetContact),
                new { accountId, contactId = success.ContactId },
                CreateContactHttpMapper.ToResponse(success)),
            problem => problem.ToActionResult(this));
    }
}
```

Controller rules:

- One Facade dependency per operation or a cohesive area Facade consistent with repository style.
- No direct Business/Data injection.
- No cache, EF, transaction, domain-rule, or persistence translation code.
- HTTP mapping may be a small explicit mapper in API; it maps only API <-> Facade models.
- Cancellation token flows through all four layers.
- Stable action names support generated client method names and `CreatedAtAction`.

## 8. Register dependencies

- API registers controllers and HTTP-only concerns.
- Domain registration extension registers Facade, Business, and Data implementations.
- Lifetimes must be compatible with `DbContext` and cache abstractions.
- Never resolve dependencies through `IServiceProvider` inside feature code.
- Add an architecture test proving forbidden dependencies are absent.

## 9. Tests by layer

### Controller tests

- Route/action attributes and response mapping.
- Correct Facade request mapping.
- Cancellation propagation.
- No Business/Data dependencies in constructor.
- Success and each stable Problem Details outcome.

### Facade unit tests

- Validation rejection does not call Business.
- Unauthorized request does not call Business or cache unsafe data.
- Cache hit bypasses Business.
- Cache miss calls Business once and caches approved result.
- Successful command invalidates correct keys.
- Failed command does not invalidate success-dependent keys.
- Business outcomes map correctly.

### Business unit tests

- Every business invariant.
- Facade-to-Data model translation through Business models.
- Expected concurrency state/version passed to Data.
- Data outcomes map to Business outcomes.
- Required audit/timeline facts are supplied.

### Data integration tests

- SQL filtering/projection/order/pagination.
- Unique/concurrency conflict behavior.
- Atomic rollback of state/history/audit/timeline.
- UTC/date-boundary behavior.
- Provider errors become typed Data outcomes.

### HTTP integration tests

- Authentication/authorization.
- Model-binding and Problem Details shape.
- Success status/body/location.
- Missing/conflict/validation behavior.
- OpenAPI operation and schemas.
- Full Controller -> Facade -> Business -> Data path against SQL Server.

Use a real SQL Server test database/resource; prefer the repository's Aspire-compatible integration fixture for relational behavior, not mocked `DbSet` or EF InMemory.

## 10. Verify

Run the smallest layer-specific tests first, then:

```bash
dotnet test --filter "FullyQualifiedName~<Area>"
dotnet build --no-restore
```

When HTTP contract changes, report Angular client regeneration as a separate microstep. When EF model changes,
stop before migration generation unless that separate microstep was explicitly requested.

## Forbidden examples

- Controller -> Business.
- Controller -> Data or `<db-context>`.
- Facade -> Data/repository/EF.
- Business -> cache provider or `HttpContext`.
- Data -> Facade/Business implementation.
- Reusing one DTO from HTTP through persistence.
- Controller performing FluentValidation plus Facade performing a different validation set.
- Facade cache key omitting user/tenant scope for protected data.
- Business returning HTTP status codes.
- Data deciding whether a lifecycle transition is semantically allowed.

## Stop conditions

Stop and report when:

- The owning CRM area is unclear.
- A required lower-layer interface/model is absent and adding it is a separate primary action.
- Record-level authorization rules are undefined.
- Cache scope/TTL/invalidation cannot be determined safely.
- The operation requires a destructive contract change.
- The migration/model snapshot is inconsistent.
- The requested design requires skipping a layer.

## Completion checklist

- [ ] MVC controller action, not minimal API.
- [ ] Controller calls only Facade.
- [ ] Facade owns validation, record access, and cache behavior.
- [ ] Facade calls only Business.
- [ ] Business owns rules and Facade/Data model translation.
- [ ] Business calls only Data.
- [ ] Data owns EF Core/SQL Server and returns typed Data outcomes.
- [ ] Separate models exist at every seam.
- [ ] No EF entities, provider exceptions, or `IQueryable` escape Data.
- [ ] Concurrency is enforced in the write.
- [ ] Audit/timeline persistence is atomic where required.
- [ ] OpenAPI/Problem Details remain stable.
- [ ] Layer-specific and HTTP integration tests pass.
- [ ] Follow-up migration/client-generation steps are reported separately.

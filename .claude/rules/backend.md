---
paths:
  - "src/services/ProjectChicago.*/**"
  - "tests/ProjectChicago.*.Core.Tests/**"
  - "tests/ProjectChicago.*.Api.Tests/**"
---
# Backend service rules

Each CRM bounded service owns its behavior and persistence. Preserve the thin HTTP host + `.Core` implementation split from the source architecture, with a sibling `.Functions` project for async transport.

## HTTP host

`ProjectChicago.<Service>` contains:

- Controllers/API transport contracts
- Program/startup composition
- ASP.NET Core authentication/authorization middleware wiring
- exception/ProblemDetails wiring
- health/telemetry wiring
- no repositories, EF models, business rules or Service Bus consumers

Controller flow:

```text
Controller -> Facade -> Business -> Data -> Repository -> DbContext
```

Controller responsibilities:

1. Bind/validate transport shape enough to produce a coherent request.
2. Capture user/tenant/correlation context through shared abstractions.
3. Call one service-owned facade use case.
4. Map the service result to HTTP status/view model/error contract.
5. Do not call Business/Data/Repository directly.

## `.Core` layout

Use a predictable structure; adapt names only when existing code has an established equivalent:

```text
.Core/
├── Facades/
├── Business/
├── Data/
├── Repositories/
├── Persistence/
├── Models/
│   ├── ServiceModels/
│   └── DataModels/Entities/
├── Validation/
├── Mapping/
└── DependencyInjection/
```

### Facade

- Public application/use-case seam for controllers and Functions.
- Performs validation through validators, not hand-written repeated `if` blocks where a validator exists.
- May handle cache read-through/invalidation when the cache belongs to the use case.
- May coordinate multiple Business calls **inside the same bounded service**.
- Does not query EF directly.
- Does not publish Service Bus messages directly.

### Business

- Owns state-transition rules and CRM lifecycle decisions.
- Translates between service models and data-layer operations through mappers.
- Decides which integration event facts should be emitted as a result of a successful mutation.
- Does not open transactions, run EF queries, or know Service Bus connection/entity names.

### Data

- Composes repositories.
- Owns transactional persistence for a use case.
- Writes domain/data changes and outbox messages atomically.
- For consumed events, owns the persistent idempotency/inbox transition along with side effects where the design requires atomicity.
- Does not contain CRM policy beyond persistence-specific invariants.

### Repository

- Works only with its owning service DbContext.
- Expresses persistence operations/query specifications.
- Does not call another service, gateway or message broker.
- Does not return `IQueryable` beyond the repository/data boundary unless the project explicitly standardizes that pattern.

## Models and contracts

- HTTP ViewModels/requests are transport-facing and live with the API host or an explicit service API-contract area.
- ServiceModels are service-owned internal application types.
- EF entities/data models stay in `.Core` persistence/model areas and do not leak to the browser or integration events.
- Integration events live in `ProjectChicago.Contracts` and are stable cross-service facts.
- Avoid one giant shared `Customer` DTO used by every service. Each bounded context owns the shape it needs.


## ASP.NET Core Identity boundary

- ASP.NET Core Identity is the confirmed identity framework, but the bounded service/database that owns the Identity store is not defined yet. Do not create an `Identity` service/project unless the architecture explicitly assigns that responsibility.
- Once an owner is selected, Identity EF entities/tables belong only to that owner's database; other services consume trusted user identity/claims and keep only service-owned references they actually need.
- Use supported ASP.NET Core Identity APIs for password hashing, lockout, account tokens, roles/claims and user management. CRM Business/Data code must not recreate credential-security mechanics.
- Authorization decisions about service-owned resources remain in the owning service even when authentication is established at the edge.
- Functions do not authenticate end users through ASP.NET Core Identity; they process trusted broker messages and propagate actor/correlation metadata captured by the publishing transaction when applicable.

## SQL Server persistence

- Use EF Core SQL Server packages/provider, not Npgsql.
- DbContext is service-owned and derives from the Project Chicago shared base context only if that base contains cross-cutting persistence mechanism (outbox/inbox, conventions), not domain entities.
- Use `datetime2`/UTC for persistent timestamps.
- Use optimistic concurrency (`rowversion` or an explicit concurrency token) only when the domain operation benefits from it; test the conflict path.
- Migrations belong to the service `.Core` project and must be SQL Server compatible.
- Do not run migrations implicitly on every Function invocation.

## Cross-service reads/writes

A service may not:

- attach another service's DbContext;
- query another service's database for validation;
- join two service databases;
- reference another service `.Core`;
- copy another service repository into Shared.

If a use case needs data owned elsewhere, choose deliberately between:

- a synchronous gateway/internal API contract when immediate consistency is required and accepted;
- an integration event/read model when eventual consistency is appropriate;
- a bounded-context redesign, which requires explicit architecture approval.

## Error handling

- Use the shared error/ProblemDetails shape consistently.
- Business/domain failures should be typed/structured, not inferred by parsing exception strings.
- Do not leak SQL details, stack traces or Service Bus internals to public API responses.

## Tests

For each mutation, test at minimum:

- happy path;
- validation failure;
- important domain/state transition rejection;
- persistence failure/rollback when relevant;
- outbox row written in the same transaction if an event is emitted;
- no outbox row when the transaction fails;
- concurrency conflict if concurrency is part of the behavior.

## Red flags

Reject changes that add:

- `BackgroundService`/`IHostedService` to a service API host for message work;
- `ServiceBusClient` use in Controller/Facade/Business;
- direct repository calls from Controller;
- EF queries in Business;
- Npgsql/Postgres types;
- cross-service project references or DB access;
- a second copy of a service implementation inside `.Functions`.

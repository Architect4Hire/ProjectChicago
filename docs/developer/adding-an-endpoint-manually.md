# Adding an Endpoint Manually

Use this when implementing without the SCRUB `add-endpoint` skill. The invariant is the architecture arrow, not file ceremony.

## Before coding

1. Read the requirement IDs.
2. Confirm the owning bounded service.
3. Confirm the public contract/route.
4. Confirm auth policy and resource scope.
5. Decide whether the operation is read-only or a mutation.
6. For a mutation, identify audit fact, concurrency and outbox requirements.

## Read path

```text
Controller
 → Facade
 → Business
 → Data
 → Repository
 → DbContext
```

A list/query should:
- accept a typed public query contract,
- validate page/filter/sort,
- apply authorization scope before returning data,
- use deterministic ordering,
- enforce bounded pagination,
- project only needed fields,
- return a public response model, never EF entities.

## Mutation path

A mutation should:
- bind a typed request,
- use trusted actor/correlation context,
- authorize before state change,
- validate in Facade/business at the proper layer,
- enforce optimistic concurrency where relevant,
- build an audit/business fact,
- commit state + outbox in the same Data-layer transaction,
- never publish directly to Service Bus.

## Controller example shape

```csharp
[HttpPost]
[Authorize(Policy = Policies.ManageClients)]
public async Task<ActionResult<ClientResponse>> Create(
    CreateClientRequest request,
    CancellationToken cancellationToken)
{
    var result = await clientFacade.CreateAsync(request, cancellationToken);
    return result.Match(...);
}
```

Exact types/method names come from current code; do not paste this skeleton blindly.

## Error contract

Map failures consistently:
- validation → 400,
- unauthenticated → 401,
- forbidden → 403,
- missing → 404,
- stale concurrency → 409,
- unexpected → safe 5xx ProblemDetails with support/trace reference.

## Tests

Minimum endpoint tests:
- happy path,
- invalid request,
- 401,
- 403,
- not-found/conflict when applicable,
- public response/OpenAPI shape.

Mutations also require Core/SQL tests proving domain rule and atomic outbox behavior.

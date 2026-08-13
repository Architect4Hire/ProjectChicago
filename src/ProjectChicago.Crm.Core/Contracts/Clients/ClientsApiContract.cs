namespace ProjectChicago.Crm.Contracts.Clients;

// Public HTTP coordinates for the Client creation and list/search use cases, fixed at
// contract-definition time (add-endpoint.md step 1) so a later controller-implementation
// microstep does not silently invent route/operation-id/policy values.
//
// -- POST api/clients (create) --
//   Method + route:  POST api/clients                                        (API-002/API-003)
//   Success:         201 Created, ClientServiceModel body, Location: api/clients/{id}
//                     (the GET-by-id companion route is not implemented by this microstep; the
//                     Location value follows the API-002 canonical route shape ahead of it).
//   Validation:      400 ValidationProblemDetails (ApiProblemDetailsFactory.Validation) for
//                     malformed/out-of-bounds request fields (SEC-022).
//   Unauthenticated: 401 ProblemDetails (ApiProblemDetailsFactory.AuthenticationRequired).
//   Unauthorized:    403 ProblemDetails (ApiProblemDetailsFactory.Forbidden) when the caller lacks
//                     RequiredAuthorizationPolicy (SEC-012/SEC-013).
//   Unexpected:      500 ProblemDetails (ApiProblemDetailsFactory.InternalError).
//   Concurrency:     not applicable to creation - no prior version exists to conflict with; the
//                     201 response's ConcurrencyToken exists for a future PUT/PATCH contract.
//   Idempotency:     no Idempotency-Key/retry-safety mechanism is defined by this contract. A
//                     retried POST is a new create attempt; CLIENT-004's PossibleDuplicates warning
//                     is the only accidental-duplicate signal today. Formal retry-safe idempotency
//                     is an open enhancement, not silently assumed here (CLAUDE.md Usage #5).
//
// -- GET api/clients (list/search) --
//   Method + route:  GET api/clients                                         (API-002/API-003)
//   Request:         ListClientsRequest bound from the query string (CLIENT-020..024, API-005).
//   Success:         200 OK, PagedResponse<ClientServiceModel> body.
//   Validation:      400 ValidationProblemDetails (ApiProblemDetailsFactory.Validation) for an
//                     out-of-range Page/PageSize or an undefined LifecycleStatus/SortBy/
//                     SortDirection value (SEC-022).
//   Unauthenticated: 401 ProblemDetails (ApiProblemDetailsFactory.AuthenticationRequired).
//   Unauthorized:    403 ProblemDetails (ApiProblemDetailsFactory.Forbidden) when the caller lacks
//                     RequiredReadAuthorizationPolicy (SEC-012/SEC-013).
//   Unexpected:      500 ProblemDetails (ApiProblemDetailsFactory.InternalError).
//   Pagination:      server-side only (CLIENT-024); DefaultPage/DefaultPageSize apply when the
//                     corresponding query value is omitted, MaxPageSize bounds every request so an
//                     unbounded result set can never be requested.
public static class ClientsApiContract
{
    public const string Route = "api/clients";

    public const string CreateOperationId = "Clients_Create";

    public const string ListOperationId = "Clients_List";

    // Named per security.md's "<CRM capability>.<Verb>" convention (its own examples - Accounts.*
    // - are illustrative; this repository's entity is named Client, so the capability follows that
    // naming). Policy registration/enforcement is controller/composition-root work, out of scope
    // for this contract-only microstep.
    public const string RequiredAuthorizationPolicy = "Clients.Write";

    // Read policy for GET api/clients, distinct from the write policy above per security.md's
    // least-privilege capability list (its own example enumerates Accounts.Read separately from
    // Accounts.Write).
    public const string RequiredReadAuthorizationPolicy = "Clients.Read";

    // Narrow, reversible pagination defaults/bounds (CLAUDE.md Usage #5) - CLIENT-024/API-005
    // require server-side, bounded pagination but do not name specific numbers. 25/100 follow the
    // requirements doc's Performance section framing ("not intended to optimize prematurely for
    // massive enterprise CRM workloads, but common workflows must remain responsive"): small enough
    // to stay responsive, large enough to be usable for a typical list/search screen.
    public const int DefaultPage = 1;
    public const int DefaultPageSize = 25;
    public const int MaxPageSize = 100;
}

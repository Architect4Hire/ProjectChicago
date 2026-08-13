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
//
// -- GET api/clients/{clientId} (detail) --
//   Method + route:  GET api/clients/{clientId}                                (API-002/API-003)
//   Success:         200 OK, ClientDetailServiceModel body (CLIENT-030..032).
//   Not found:       404 ProblemDetails (ApiProblemDetailsFactory.NotFound) when no Client with the
//                     requested Id exists - IClientFacade.GetDetailAsync returns null for that case,
//                     and only this controller action decides that null maps to 404.
//   Unauthenticated: 401 ProblemDetails (ApiProblemDetailsFactory.AuthenticationRequired).
//   Unauthorized:    403 ProblemDetails (ApiProblemDetailsFactory.Forbidden) when the caller lacks
//                     RequiredReadAuthorizationPolicy (SEC-012/SEC-013).
//   Unexpected:      500 ProblemDetails (ApiProblemDetailsFactory.InternalError).
//
// -- PATCH api/clients/{clientId}/lifecycle-status (lifecycle transition) --
//   Method + route:  PATCH api/clients/{clientId}/lifecycle-status              (API-002/API-003;
//                     PATCH - a partial, state-only update of the Client resource, distinct from a
//                     future general PUT/PATCH api/clients/{clientId} that would replace broader
//                     Client fields - RESTRICTION: this contract adds no other Client update action).
//   Request:         ChangeClientLifecycleStatusViewModel body - NewStatus plus
//                     ExpectedConcurrencyToken, the caller's last-known
//                     ClientServiceModel.ConcurrencyToken (DATA-008).
//   Success:         200 OK, ClientServiceModel body reflecting the new LifecycleStatus and
//                     ConcurrencyToken (CLIENT-010..012).
//   Validation:      400 ValidationProblemDetails (ApiProblemDetailsFactory.Validation) for a
//                     malformed/undefined NewStatus or missing ExpectedConcurrencyToken (SEC-022),
//                     and for a well-formed but disallowed transition (CLIENT-010..015 -
//                     ClientLifecycleTransitionRules rejects it) surfaced as a NewStatus field
//                     error - a transition request is invalid the same way a malformed field is,
//                     not a race with another request.
//   Not found:       404 ProblemDetails (ApiProblemDetailsFactory.NotFound) when no Client with the
//                     requested Id exists.
//   Conflict:        409 ProblemDetails (ApiProblemDetailsFactory.ConcurrencyConflict) when
//                     ExpectedConcurrencyToken does not match the Client's currently persisted
//                     version (DATA-008) - the caller must reload and retry.
//   Unauthenticated: 401 ProblemDetails (ApiProblemDetailsFactory.AuthenticationRequired).
//   Unauthorized:    403 ProblemDetails (ApiProblemDetailsFactory.Forbidden) when the caller lacks
//                     RequiredAuthorizationPolicy (SEC-012/SEC-013 - a lifecycle transition is a
//                     mutation, so it is authorized against the same Clients.Write policy as
//                     creation, not the read policy).
//   Unexpected:      500 ProblemDetails (ApiProblemDetailsFactory.InternalError).
public static class ClientsApiContract
{
    public const string Route = "api/clients";

    public const string CreateOperationId = "Clients_Create";

    public const string ListOperationId = "Clients_List";

    public const string GetDetailOperationId = "Clients_GetDetail";

    public const string ChangeLifecycleStatusOperationId = "Clients_ChangeLifecycleStatus";

    // Relative to Route: "api/clients/{clientId}/lifecycle-status".
    public const string LifecycleStatusRouteSuffix = "{clientId:guid}/lifecycle-status";

    public const string ArchiveOperationId = "Clients_Archive";

    // Relative to Route: "api/clients/{clientId}/archive".
    public const string ArchiveRouteSuffix = "{clientId:guid}/archive";

    public const string RestoreOperationId = "Clients_Restore";

    // Relative to Route: "api/clients/{clientId}/restore".
    public const string RestoreRouteSuffix = "{clientId:guid}/restore";

    public const string UpdateOperationId = "Clients_Update";

    // Relative to Route: "api/clients/{clientId}" for a general profile update (CLIENT-002).
    // PATCH is appropriate for a partial, field-selective update where callers omit fields they
    // are not modifying (a true partial update, distinct from lifecycle/archive operations which
    // transition the Client's state).
    public const string UpdateRouteSuffix = "{clientId:guid}";

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

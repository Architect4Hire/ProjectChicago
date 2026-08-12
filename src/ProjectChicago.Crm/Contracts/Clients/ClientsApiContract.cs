namespace ProjectChicago.Crm.Contracts.Clients;

// Public HTTP coordinates for the Client creation use case, fixed at contract-definition time
// (add-endpoint.md step 1) so a later controller-implementation microstep does not silently invent
// route/operation-id/policy values.
//
//   Method + route:  POST api/clients                                        (API-002/API-003)
//   Success:         201 Created, ClientResponse body, Location: api/clients/{id}
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
public static class ClientsApiContract
{
    public const string Route = "api/clients";

    public const string CreateOperationId = "Clients_Create";

    // Named per security.md's "<CRM capability>.<Verb>" convention (its own examples - Accounts.*
    // - are illustrative; this repository's entity is named Client, so the capability follows that
    // naming). Policy registration/enforcement is controller/composition-root work, out of scope
    // for this contract-only microstep.
    public const string RequiredAuthorizationPolicy = "Clients.Write";
}

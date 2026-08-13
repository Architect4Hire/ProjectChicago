namespace ProjectChicago.Crm.Contracts.Projects;

// Public HTTP coordinates for the Project creation use case, fixed at contract-definition time
// (add-endpoint.md step 1) so a later controller-implementation microstep does not silently
// invent route/operation-id/policy values.
//
// -- POST api/clients/{clientId}/projects (create) --
//   Method + route:  POST api/clients/{clientId}/projects                       (API-002/API-003;
//                     PROJECT-001..002 - a Project belongs to exactly one Client, so the Client
//                     context is part of the stable route shape).
//   Request:         CreateProjectViewModel body bound from the request JSON.
//   Success:         201 Created, ProjectServiceModel body, Location: api/projects/{id}
//                     (the companion GET /api/projects/{projectId} detail route may be added
//                     later; the Location value follows the stable PROJECT-002 route shape).
//   Validation:      400 ValidationProblemDetails (ApiProblemDetailsFactory.Validation) for
//                     malformed/out-of-bounds request fields (SEC-022). Missing or invalid Client
//                     ID in the route parameter is detected by MVC binding and produces a 400 as well.
//   Not found:       400/422 when the Client referenced by clientId does not exist. IProjectData
//                     throws ProjectClientNotFoundException (DATA-002); the controller catches it
//                     and maps it as 400 BadRequest with a detail message - the Project cannot be
//                     created because its required parent Client does not exist, making the request
//                     as a whole invalid given the current state.
//   Unauthenticated: 401 ProblemDetails (ApiProblemDetailsFactory.AuthenticationRequired).
//   Unauthorized:    403 ProblemDetails (ApiProblemDetailsFactory.Forbidden) when the caller lacks
//                     RequiredAuthorizationPolicy scoped to the Client (SEC-012/SEC-013; PROJECT-001).
//   Unexpected:      500 ProblemDetails (ApiProblemDetailsFactory.InternalError).
//   Concurrency:     not applicable to creation - no prior version exists to conflict with; the
//                     201 response's ConcurrencyToken exists for a future PUT/PATCH contract.
//   Idempotency:     no Idempotency-Key/retry-safety mechanism is defined by this contract. A
//                     retried POST is a new create attempt. Formal retry-safe idempotency is an
//                     open enhancement, not silently assumed here (CLAUDE.md Usage #5).
public static class ProjectsApiContract
{
    public const string Route = "api/clients/{clientId}/projects";

    public const string CreateOperationId = "Projects_Create";

    // Named per security.md's "<CRM capability>.<Verb>" convention (PROJECT-001..002 - a user
    // authorized to create Projects is authorized per-Client, matching the fine-grained
    // authorization model established by the Client/project boundary structure). Policy
    // registration/enforcement is controller/composition-root work, out of scope for this
    // contract-only microstep.
    public const string RequiredAuthorizationPolicy = "Projects.Write";

    // List Projects operation (PROJECT-020..023, API-004/API-005).
    public const string ListRoute = "api/projects";

    public const string ListOperationId = "Projects_List";

    // Authorization for Projects.List uses Projects.Read policy (PROJECT-020: users view Projects).
    public const string ListRequiredAuthorizationPolicy = "Projects.Read";

    // Server-side pagination defaults (PROJECT-023, API-005). Mirrors ClientsApiContract for
    // consistency across list operations.
    public const int DefaultPage = 1;

    public const int DefaultPageSize = 25;

    public const int MaxPageSize = 200;

    // Detail Project operation (PROJECT-030..031, API-002/API-003).
    public const string DetailRoute = "api/projects/{projectId}";

    public const string DetailOperationId = "Projects_GetDetail";

    // Authorization for Projects.Detail uses Projects.Read policy (PROJECT-030: users view Project details).
    public const string DetailRequiredAuthorizationPolicy = "Projects.Read";

    // Transition Project status operation (PROJECT-010..014, API-001..007, SEC-012..013, DATA-008).
    // Method + route:  PATCH api/projects/{projectId}/status                      (API-003; status
    //                   transition is a specialized mutation that owns new state, completion timestamps
    //                   PROJECT-012, and acknowledgement requirements PROJECT-013, so a dedicated route
    //                   is cleaner than PUT with a partial request body).
    // Request:         ChangeProjectStatusViewModel body bound from the request JSON.
    // Success:         200 OK, ProjectServiceModel body (the updated Project state).
    // Validation:      400 ValidationProblemDetails for malformed/out-of-bounds request fields
    //                   (SEC-022, PROJECT-010..013 transition legality, PROJECT-013 acknowledgement
    //                   requirement). Invalid projectId in the route parameter is detected by MVC
    //                   binding and produces a 400 as well.
    // Not found:       404 when the Project referenced by projectId does not exist (PROJECT-030).
    // Unauthenticated: 401 ProblemDetails.
    // Unauthorized:    403 ProblemDetails when the caller lacks Projects.Write policy (SEC-012/013).
    // Concurrency:     409 Conflict when expectedConcurrencyToken (DATA-008) does not match the
    //                   Project's current RowVersion. The client must refresh and retry.
    // Unexpected:      500 ProblemDetails.
    public const string TransitionStatusRoute = "api/projects/{projectId}/status";

    public const string TransitionStatusOperationId = "Projects_TransitionStatus";

    // Authorization for Projects.TransitionStatus uses Projects.Write policy (PROJECT-010..014:
    // a user authorized to transition Project status and complete Projects with acknowledgement).
    public const string TransitionStatusRequiredAuthorizationPolicy = "Projects.Write";
}

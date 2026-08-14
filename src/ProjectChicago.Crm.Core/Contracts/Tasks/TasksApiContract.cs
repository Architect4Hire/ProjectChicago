namespace ProjectChicago.Crm.Contracts.Tasks;

// Public HTTP coordinates for Task operations, fixed at contract-definition time
// (add-endpoint.md step 1) so a later controller-implementation microstep does not silently
// invent route/operation-id/policy values.
//
// -- POST /api/projects/{projectId}/tasks (create) --
//   Method + route:  POST /api/projects/{projectId}/tasks                    (API-002/API-003;
//                     TASK-001..002 - a Task belongs to exactly one Project, so the Project
//                     context is part of the stable route shape).
//   Request:         CreateTaskViewModel body bound from the request JSON.
//   Success:         201 Created, TaskServiceModel body, Location: /api/tasks/{id}
//                     (the companion GET /api/tasks/{taskId} detail route may be added
//                     later; the Location value follows the stable TASK-002 route shape).
//   Validation:      400 ValidationProblemDetails (ApiProblemDetailsFactory.Validation) for
//                     malformed/out-of-bounds request fields (SEC-022). Missing or invalid Project
//                     ID in the route parameter is detected by MVC binding and produces a 400 as well.
//   Not found:       400/422 when the Project referenced by projectId does not exist. ITaskData
//                     throws TaskProjectNotFoundException (DATA-003); the controller catches it
//                     and maps it as 400 BadRequest with a detail message - the Task cannot be
//                     created because its required parent Project does not exist, making the request
//                     as a whole invalid given the current state.
//   Unauthenticated: 401 ProblemDetails (ApiProblemDetailsFactory.AuthenticationRequired).
//   Unauthorized:    403 ProblemDetails (ApiProblemDetailsFactory.Forbidden) when the caller lacks
//                     RequiredAuthorizationPolicy scoped to the Project (SEC-012/SEC-013; TASK-001).
//   Unexpected:      500 ProblemDetails (ApiProblemDetailsFactory.InternalError).
//   Concurrency:     not applicable to creation - no prior version exists to conflict with; the
//                     201 response's ConcurrencyToken exists for a future PUT/PATCH contract.
//   Idempotency:     no Idempotency-Key/retry-safety mechanism is defined by this contract. A
//                     retried POST is a new create attempt. Formal retry-safe idempotency is an
//                     open enhancement, not silently assumed here (CLAUDE.md Usage #5).
//
// -- PATCH /api/tasks/{taskId} (assign/reassign) --
//   Method + route:  PATCH /api/tasks/{taskId}                              (API-002/API-003;
//                     TASK-013..014 assignment/reassignment operation).
//   Request:         AssignTaskViewModel body (taskId, assignedUserId, concurrencyToken).
//   Success:         200 OK, TaskServiceModel body (with updated ConcurrencyToken).
//   Validation:      400 ValidationProblemDetails (ApiProblemDetailsFactory.Validation) for
//                     malformed/out-of-bounds request fields (SEC-022).
//   Not found:       400/404 when the Task referenced by taskId does not exist. ITaskBusiness
//                     throws ArgumentException; the controller catches it and maps as 400
//                     BadRequest (the request is invalid given the current state).
//   Conflict:        409 Conflict when RowVersion has changed since the client's fetch
//                     (optimistic locking). ITaskData detects via DbUpdateConcurrencyException
//                     from EF Core (DATA-008). The handler maps this as 409 ConflictProblemDetails.
//   Unauthenticated: 401 ProblemDetails (ApiProblemDetailsFactory.AuthenticationRequired).
//   Unauthorized:    403 ProblemDetails (ApiProblemDetailsFactory.Forbidden) when the caller lacks
//                     Tasks.Write authorization (SEC-012/SEC-013; TASK-013).
//   Unexpected:      500 ProblemDetails (ApiProblemDetailsFactory.InternalError).
//   Idempotency:     no Idempotency-Key/retry-safety mechanism is defined by this contract. A
//                     retried PATCH is a new assign attempt. Formal retry-safe idempotency is an
//                     open enhancement, not silently assumed here (CLAUDE.md Usage #5).
//
// -- PATCH /api/tasks/{taskId}/priority (change priority) --
//   Method + route:  PATCH /api/tasks/{taskId}/priority                     (API-002/API-003;
//                     TASK-015 priority change operation).
//   Request:         ChangeTaskPriorityViewModel body (taskId, priority, concurrencyToken).
//   Success:         200 OK, TaskServiceModel body (with updated ConcurrencyToken).
//   Validation:      400 ValidationProblemDetails (ApiProblemDetailsFactory.Validation) for
//                     malformed/out-of-bounds request fields (SEC-022).
//   Not found:       400/404 when the Task referenced by taskId does not exist. ITaskBusiness
//                     throws ArgumentException; the controller catches it and maps as 400
//                     BadRequest (the request is invalid given the current state).
//   Conflict:        409 Conflict when RowVersion has changed since the client's fetch
//                     (optimistic locking). ITaskData detects via DbUpdateConcurrencyException
//                     from EF Core (DATA-008). The handler maps this as 409 ConflictProblemDetails.
//   Unauthenticated: 401 ProblemDetails (ApiProblemDetailsFactory.AuthenticationRequired).
//   Unauthorized:    403 ProblemDetails (ApiProblemDetailsFactory.Forbidden) when the caller lacks
//                     Tasks.Write authorization (SEC-012/SEC-013; TASK-015).
//   Unexpected:      500 ProblemDetails (ApiProblemDetailsFactory.InternalError).
//   Idempotency:     no Idempotency-Key/retry-safety mechanism is defined by this contract. A
//                     retried PATCH is a new priority change attempt. Formal retry-safe idempotency
//                     is an open enhancement, not silently assumed here (CLAUDE.md Usage #5).
//
// -- PATCH /api/tasks/{taskId}/details (edit details) --
//   Method + route:  PATCH /api/tasks/{taskId}/details                       (API-002/API-003;
//                     TASK-002 edit operation for title, description, start/due dates, notes).
//   Request:         EditTaskViewModel body (taskId, title?, description?, startDateUtc?,
//                     dueDateUtc?, notes?, concurrencyToken). Optional fields allow partial updates.
//   Success:         200 OK, TaskServiceModel body (with updated ConcurrencyToken).
//   Validation:      400 ValidationProblemDetails (ApiProblemDetailsFactory.Validation) for
//                     malformed/out-of-bounds request fields (SEC-022). Invalid dates (non-UTC)
//                     or no fields changed result in 400 BadRequest.
//   Not found:       400/404 when the Task referenced by taskId does not exist. ITaskBusiness
//                     throws ArgumentException; the controller catches it and maps as 400
//                     BadRequest (the request is invalid given the current state).
//   Conflict:        409 Conflict when RowVersion has changed since the client's fetch
//                     (optimistic locking). ITaskData detects via DbUpdateConcurrencyException
//                     from EF Core (DATA-008). The handler maps this as 409 ConflictProblemDetails.
//   Unauthenticated: 401 ProblemDetails (ApiProblemDetailsFactory.AuthenticationRequired).
//   Unauthorized:    403 ProblemDetails (ApiProblemDetailsFactory.Forbidden) when the caller lacks
//                     Tasks.Write authorization (SEC-012/SEC-013; TASK-002).
//   Unexpected:      500 ProblemDetails (ApiProblemDetailsFactory.InternalError).
//   Idempotency:     no Idempotency-Key/retry-safety mechanism is defined by this contract. A
//                     retried PATCH is a new edit attempt. Formal retry-safe idempotency is an
//                     open enhancement, not silently assumed here (CLAUDE.md Usage #5).
//
// -- GET /api/tasks (list) --
//   Method + route:  GET /api/tasks                                         (API-002/API-003;
//                     TASK-020..022 collection endpoint - returns multiple Tasks matching the
//                     supplied filters/sort, paginated per API-005).
//   Request:         ListTasksRequest (query string binding; all parameters optional).
//   Success:         200 OK, PagedResponse<TaskServiceModel> body.
//   Validation:      400 ValidationProblemDetails (ApiProblemDetailsFactory.Validation) for
//                     malformed/out-of-bounds query fields (SEC-022). Page/PageSize bounds are
//                     validated before calling Facade (PERF-003).
//   Unauthenticated: 401 ProblemDetails (ApiProblemDetailsFactory.AuthenticationRequired).
//   Unauthorized:    403 ProblemDetails (ApiProblemDetailsFactory.Forbidden) when the caller lacks
//                     Tasks.Read authorization (SEC-012/SEC-013).
//   Unexpected:      500 ProblemDetails (ApiProblemDetailsFactory.InternalError).
public static class TasksApiContract
{
    public const string Route = "api/projects/{projectId}/tasks";

    public const string CreateOperationId = "Tasks_Create";

    public const string AssignOperationId = "Tasks_Assign";

    public const string ChangePriorityOperationId = "Tasks_ChangePriority";

    public const string ChangeStatusOperationId = "Tasks_ChangeStatus";

    public const string ReopenOperationId = "Tasks_Reopen";

    public const string EditOperationId = "Tasks_Edit";

    public const string ListOperationId = "Tasks_List";

    // Named per security.md's "<CRM capability>.<Verb>" convention (TASK-001..002 - a user
    // authorized to create Tasks is authorized per-Project, matching the fine-grained
    // authorization model established by the Project/task boundary structure). Policy
    // registration/enforcement is controller/composition-root work, out of scope for this
    // contract-only microstep.
    public const string RequiredAuthorizationPolicy = "Tasks.Write";

    // Pagination defaults (PERF-003/API-005). Mirror ClientsApiContract and ProjectsApiContract
    // conventions so "no page requested" and "out-of-range page size" behave identically across
    // all collection endpoints.
    public const int DefaultPage = 1;
    public const int DefaultPageSize = 25;
    public const int MaxPageSize = 100;
}

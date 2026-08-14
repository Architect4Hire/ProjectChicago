using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using ProjectChicago.Crm.Contracts.Common;
using ProjectChicago.Crm.Contracts.Tasks;
using ProjectChicago.Crm.Core.Data;
using ProjectChicago.Crm.Core.Facades;
using ProjectChicago.ServiceDefaults.Correlation;
using ProjectChicago.Shared.Errors;

namespace ProjectChicago.Crm.Controllers;

// Handlers for Task collection (TASK-020..022, API-001..007, SEC-010..013, ERROR-001..005).
// Transport-only: bind wire requests, apply the coarse "is there an authenticated actor at all"
// check, call ITaskFacade operations, and map typed results/exceptions to standard HTTP/ProblemDetails
// shape (onion-boundaries.md; add-endpoint skill step 3). No field-by-field request/response mapping
// lives here - ITaskFacade accepts and returns wire contract types directly, and TaskContractMappingExtensions
// (Business) owns translation to/from Business models. Fine-grained SEC-012/013 policy authorization
// and business rules live in Facade/Business - this controller injects no Business/Data/Repository/
// DbContext and never publishes directly (RESTRICTION).
[ApiController]
[Route("")]
public sealed class TasksController : ControllerBase
{
    private readonly ITaskFacade _taskFacade;

    public TasksController(ITaskFacade taskFacade)
    {
        _taskFacade = taskFacade ?? throw new ArgumentNullException(nameof(taskFacade));
    }

    // Unauthenticated (401) vs unauthorized (403) are deliberately distinct per TasksApiContract:
    // this coarse check (is there any authenticated actor at all) stays in the controller as plain
    // ASP.NET Core ClaimsPrincipal inspection - not a call into ITaskFacade/ITaskAuthorization -
    // so it never depends on the still-open ADR-0018 authentication-transport decision. The narrower
    // "does this actor hold Tasks.Write" policy check happens in Facade/ITaskAuthorization and
    // surfaces here only as an UnauthorizedAccessException the already-registered ApiExceptionHandler
    // classifies into 403. The TaskProjectNotFoundException from ITaskData (DATA-003: "A Task
    // shall not exist without a Project") is caught here and mapped as a 400 BadRequest - the Project
    // does not exist, making the request invalid given the current state.
    [Route("api/projects/{projectId}/tasks")]
    [HttpPost(Name = TasksApiContract.CreateOperationId)]
    [ProducesResponseType(typeof(TaskServiceModel), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<TaskServiceModel>> Create(
        Guid projectId,
        [FromBody] CreateTaskViewModel request,
        CancellationToken cancellationToken)
    {
        if (User.Identity is not { IsAuthenticated: true })
        {
            return Unauthorized();
        }

        try
        {
            var response = await _taskFacade.CreateAsync(request, cancellationToken).ConfigureAwait(false);

            return Created(new Uri($"api/tasks/{response.Id}", UriKind.Relative), response);
        }
        catch (TaskProjectNotFoundException ex)
        {
            var requestContext = HttpRequestContextFactory.Create(HttpContext);
            var problem = ApiProblemDetailsFactory.Validation(
                requestContext,
                fieldErrors: new Dictionary<string, string[]>
                {
                    [nameof(CreateTaskViewModel.ProjectId)] = [ex.Message],
                });

            return BadRequest(problem);
        }
    }

    // TASK-013/014: assign or reassign a Task to a user. Requires Tasks.Write authorization
    // (SEC-012/013). Returns 200 with the updated TaskServiceModel on success, 409 Conflict
    // when the ConcurrencyToken (RowVersion) has changed since fetch (optimistic locking,
    // DATA-008), 400 when the Task doesn't exist, 401/403 for authentication/authorization.
    [Route("api/tasks/{taskId}")]
    [HttpPatch(Name = TasksApiContract.AssignOperationId)]
    [ProducesResponseType(typeof(TaskServiceModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<TaskServiceModel>> Assign(
        Guid taskId,
        [FromBody] AssignTaskViewModel request,
        CancellationToken cancellationToken)
    {
        if (User.Identity is not { IsAuthenticated: true })
        {
            return Unauthorized();
        }

        try
        {
            var response = await _taskFacade.AssignAsync(request, cancellationToken).ConfigureAwait(false);

            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            var requestContext = HttpRequestContextFactory.Create(HttpContext);
            var problem = ApiProblemDetailsFactory.Validation(
                requestContext,
                fieldErrors: new Dictionary<string, string[]>
                {
                    [nameof(AssignTaskViewModel.TaskId)] = [ex.Message],
                });

            return BadRequest(problem);
        }
        catch (DbUpdateConcurrencyException)
        {
            var requestContext = HttpRequestContextFactory.Create(HttpContext);
            var problem = ApiProblemDetailsFactory.ConcurrencyConflict(
                requestContext,
                detail: "The Task's version has changed. Fetch the latest version and retry.");

            return Conflict(problem);
        }
    }

    // TASK-015: change a Task's priority. Requires Tasks.Write authorization (SEC-012/013).
    // Returns 200 with the updated TaskServiceModel on success, 409 Conflict when the
    // ConcurrencyToken (RowVersion) has changed since fetch (optimistic locking, DATA-008),
    // 400 when the Task doesn't exist, 401/403 for authentication/authorization.
    [Route("api/tasks/{taskId}/priority")]
    [HttpPatch(Name = TasksApiContract.ChangePriorityOperationId)]
    [ProducesResponseType(typeof(TaskServiceModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<TaskServiceModel>> ChangePriority(
        Guid taskId,
        [FromBody] ChangeTaskPriorityViewModel request,
        CancellationToken cancellationToken)
    {
        if (User.Identity is not { IsAuthenticated: true })
        {
            return Unauthorized();
        }

        try
        {
            var response = await _taskFacade.ChangePriorityAsync(request, cancellationToken).ConfigureAwait(false);

            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            var requestContext = HttpRequestContextFactory.Create(HttpContext);
            var problem = ApiProblemDetailsFactory.Validation(
                requestContext,
                fieldErrors: new Dictionary<string, string[]>
                {
                    [nameof(ChangeTaskPriorityViewModel.TaskId)] = [ex.Message],
                });

            return BadRequest(problem);
        }
        catch (DbUpdateConcurrencyException)
        {
            var requestContext = HttpRequestContextFactory.Create(HttpContext);
            var problem = ApiProblemDetailsFactory.ConcurrencyConflict(
                requestContext,
                detail: "The Task's version has changed. Fetch the latest version and retry.");

            return Conflict(problem);
        }
    }

    // TASK-010..012: change a Task's status. Requires Tasks.Write authorization (SEC-012/013).
    // Returns 200 with the updated TaskServiceModel on success, 409 Conflict when the
    // ConcurrencyToken (RowVersion) has changed since fetch (optimistic locking, DATA-008),
    // 400 when the Task doesn't exist or the status transition is invalid, 401/403 for
    // authentication/authorization.
    [Route("api/tasks/{taskId}/status")]
    [HttpPatch(Name = TasksApiContract.ChangeStatusOperationId)]
    [ProducesResponseType(typeof(TaskServiceModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<TaskServiceModel>> ChangeStatus(
        Guid taskId,
        [FromBody] ChangeTaskStatusViewModel request,
        CancellationToken cancellationToken)
    {
        if (User.Identity is not { IsAuthenticated: true })
        {
            return Unauthorized();
        }

        try
        {
            var response = await _taskFacade.ChangeStatusAsync(request, cancellationToken).ConfigureAwait(false);

            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            var requestContext = HttpRequestContextFactory.Create(HttpContext);
            var problem = ApiProblemDetailsFactory.Validation(
                requestContext,
                fieldErrors: new Dictionary<string, string[]>
                {
                    [nameof(ChangeTaskStatusViewModel.TaskId)] = [ex.Message],
                });

            return BadRequest(problem);
        }
        catch (InvalidOperationException ex)
        {
            var requestContext = HttpRequestContextFactory.Create(HttpContext);
            var problem = ApiProblemDetailsFactory.Validation(
                requestContext,
                detail: ex.Message);

            return BadRequest(problem);
        }
        catch (DbUpdateConcurrencyException)
        {
            var requestContext = HttpRequestContextFactory.Create(HttpContext);
            var problem = ApiProblemDetailsFactory.ConcurrencyConflict(
                requestContext,
                detail: "The Task's version has changed. Fetch the latest version and retry.");

            return Conflict(problem);
        }
    }

    // TASK-012: reopen a completed Task. Requires Tasks.Write authorization (SEC-012/013).
    // Returns 200 with the updated TaskServiceModel on success, 409 Conflict when the
    // ConcurrencyToken (RowVersion) has changed since fetch (optimistic locking, DATA-008),
    // 400 when the Task doesn't exist or is not Completed, 401/403 for authentication/authorization.
    [Route("api/tasks/{taskId}/reopen")]
    [HttpPatch(Name = TasksApiContract.ReopenOperationId)]
    [ProducesResponseType(typeof(TaskServiceModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<TaskServiceModel>> Reopen(
        Guid taskId,
        [FromBody] ReopenTaskViewModel request,
        CancellationToken cancellationToken)
    {
        if (User.Identity is not { IsAuthenticated: true })
        {
            return Unauthorized();
        }

        try
        {
            var response = await _taskFacade.ReopenAsync(request, cancellationToken).ConfigureAwait(false);

            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            var requestContext = HttpRequestContextFactory.Create(HttpContext);
            var problem = ApiProblemDetailsFactory.Validation(
                requestContext,
                fieldErrors: new Dictionary<string, string[]>
                {
                    [nameof(ReopenTaskViewModel.TaskId)] = [ex.Message],
                });

            return BadRequest(problem);
        }
        catch (InvalidOperationException ex)
        {
            var requestContext = HttpRequestContextFactory.Create(HttpContext);
            var problem = ApiProblemDetailsFactory.Validation(
                requestContext,
                detail: ex.Message);

            return BadRequest(problem);
        }
        catch (DbUpdateConcurrencyException)
        {
            var requestContext = HttpRequestContextFactory.Create(HttpContext);
            var problem = ApiProblemDetailsFactory.ConcurrencyConflict(
                requestContext,
                detail: "The Task's version has changed. Fetch the latest version and retry.");

            return Conflict(problem);
        }
    }

    // TASK-002: edit a Task's details (title, description, start/due dates, notes). Requires
    // Tasks.Write authorization (SEC-012/013). Returns 200 with the updated TaskServiceModel on
    // success, 409 Conflict when the ConcurrencyToken (RowVersion) has changed since fetch
    // (optimistic locking, DATA-008), 400 when the Task doesn't exist or no fields changed,
    // 401/403 for authentication/authorization.
    [Route("api/tasks/{taskId}/details")]
    [HttpPatch(Name = TasksApiContract.EditOperationId)]
    [ProducesResponseType(typeof(TaskServiceModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<TaskServiceModel>> Edit(
        Guid taskId,
        [FromBody] EditTaskViewModel request,
        CancellationToken cancellationToken)
    {
        if (User.Identity is not { IsAuthenticated: true })
        {
            return Unauthorized();
        }

        try
        {
            var response = await _taskFacade.EditAsync(request, cancellationToken).ConfigureAwait(false);

            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            var requestContext = HttpRequestContextFactory.Create(HttpContext);
            var problem = ApiProblemDetailsFactory.Validation(
                requestContext,
                fieldErrors: new Dictionary<string, string[]>
                {
                    [nameof(EditTaskViewModel.TaskId)] = [ex.Message],
                });

            return BadRequest(problem);
        }
        catch (InvalidOperationException ex)
        {
            var requestContext = HttpRequestContextFactory.Create(HttpContext);
            var problem = ApiProblemDetailsFactory.Validation(
                requestContext,
                detail: ex.Message);

            return BadRequest(problem);
        }
        catch (DbUpdateConcurrencyException)
        {
            var requestContext = HttpRequestContextFactory.Create(HttpContext);
            var problem = ApiProblemDetailsFactory.ConcurrencyConflict(
                requestContext,
                detail: "The Task's version has changed. Fetch the latest version and retry.");

            return Conflict(problem);
        }
    }

    [Route("api/tasks")]
    [HttpGet(Name = TasksApiContract.ListOperationId)]
    [ProducesResponseType(typeof(PagedResponse<TaskServiceModel>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResponse<TaskServiceModel>>> List(
        [FromQuery] ListTasksRequest request,
        CancellationToken cancellationToken)
    {
        if (User.Identity is not { IsAuthenticated: true })
        {
            return Unauthorized();
        }

        try
        {
            var response = await _taskFacade.ListAsync(request, cancellationToken).ConfigureAwait(false);

            return Ok(response);
        }
        catch (ValidationException ex)
        {
            var requestContext = HttpRequestContextFactory.Create(HttpContext);
            var problem = ApiProblemDetailsFactory.Validation(requestContext, detail: ex.Message);

            return BadRequest(problem);
        }
    }
}

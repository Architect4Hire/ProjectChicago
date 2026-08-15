using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using ProjectChicago.Crm.Contracts.Common;
using ProjectChicago.Crm.Contracts.Tasks;
using ProjectChicago.Crm.Core.Data;
using ProjectChicago.Crm.Core.Facades;
using ProjectChicago.ServiceDefaults.Correlation;
using ProjectChicago.ServiceDefaults.Filters;
using ProjectChicago.Shared.Errors;

namespace ProjectChicago.Crm.Controllers;

/// <summary>
/// CRM Tasks resource endpoints (TASK-020..022, API-006/007, SEC-010..013).
/// Transport-only: binds requests, delegates to ITaskFacade, maps results to HTTP/ProblemDetails.
/// </summary>
[ApiController]
[Route("")]
[RequireAuthentication]
public sealed class TasksController : ControllerBase
{
    private readonly ITaskFacade _taskFacade;

    public TasksController(ITaskFacade taskFacade)
    {
        _taskFacade = taskFacade ?? throw new ArgumentNullException(nameof(taskFacade));
    }

    /// <summary>
    /// Create a new task for a project. Requires Tasks.Write authorization (SEC-010..013).
    /// Returns 400 if project does not exist.
    /// </summary>
    /// <param name="projectId">Project ID</param>
    /// <param name="request">Task creation data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <response code="201">Task created</response>
    /// <response code="400">Validation error or project not found</response>
    /// <response code="401">Not authenticated</response>
    /// <response code="403">Not authorized (requires Tasks.Write)</response>
    [Route("api/projects/{projectId}/tasks")]
    [HttpPost(Name = TasksApiContract.CreateOperationId)]
    [Authorize(Policy = "Tasks.Write")]
    [ProducesResponseType(typeof(TaskServiceModel), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<TaskServiceModel>> Create(
        Guid projectId,
        [FromBody] CreateTaskViewModel request,
        CancellationToken cancellationToken)
    {

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

    /// <summary>
    /// Assign or reassign a task to a user. Requires Tasks.Write authorization (SEC-010..013).
    /// Returns 409 if concurrency conflict; 400 if task not found.
    /// </summary>
    /// <param name="taskId">Task ID</param>
    /// <param name="request">Assign request with assignee and concurrency token</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <response code="200">Task assigned</response>
    /// <response code="400">Validation error or task not found</response>
    /// <response code="401">Not authenticated</response>
    /// <response code="403">Not authorized (requires Tasks.Write)</response>
    /// <response code="409">Concurrency conflict (expected version mismatch)</response>
    [Route("api/tasks/{taskId}")]
    [HttpPatch(Name = TasksApiContract.AssignOperationId)]
    [Authorize(Policy = "Tasks.Write")]
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

    /// <summary>
    /// Change task priority. Requires Tasks.Write authorization (SEC-010..013).
    /// Returns 409 if concurrency conflict; 400 if task not found.
    /// </summary>
    /// <param name="taskId">Task ID</param>
    /// <param name="request">Priority change with concurrency token</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <response code="200">Priority changed</response>
    /// <response code="400">Validation error or task not found</response>
    /// <response code="401">Not authenticated</response>
    /// <response code="403">Not authorized (requires Tasks.Write)</response>
    /// <response code="409">Concurrency conflict (expected version mismatch)</response>
    [Route("api/tasks/{taskId}/priority")]
    [HttpPatch(Name = TasksApiContract.ChangePriorityOperationId)]
    [Authorize(Policy = "Tasks.Write")]
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

    /// <summary>
    /// Change task status. Requires Tasks.Write authorization (SEC-010..013).
    /// Returns 409 if concurrency conflict; 400 if task not found or transition invalid.
    /// </summary>
    /// <param name="taskId">Task ID</param>
    /// <param name="request">Status change with concurrency token</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <response code="200">Status changed</response>
    /// <response code="400">Validation error, task not found, or invalid state transition</response>
    /// <response code="401">Not authenticated</response>
    /// <response code="403">Not authorized (requires Tasks.Write)</response>
    /// <response code="409">Concurrency conflict (expected version mismatch)</response>
    [Route("api/tasks/{taskId}/status")]
    [HttpPatch(Name = TasksApiContract.ChangeStatusOperationId)]
    [Authorize(Policy = "Tasks.Write")]
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

    /// <summary>
    /// Reopen a completed task. Requires Tasks.Write authorization (SEC-010..013).
    /// Returns 409 if concurrency conflict; 400 if task not found or not completed.
    /// </summary>
    /// <param name="taskId">Task ID</param>
    /// <param name="request">Reopen request with concurrency token</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <response code="200">Task reopened</response>
    /// <response code="400">Validation error, task not found, or task not completed</response>
    /// <response code="401">Not authenticated</response>
    /// <response code="403">Not authorized (requires Tasks.Write)</response>
    /// <response code="409">Concurrency conflict (expected version mismatch)</response>
    [Route("api/tasks/{taskId}/reopen")]
    [HttpPatch(Name = TasksApiContract.ReopenOperationId)]
    [Authorize(Policy = "Tasks.Write")]
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
    [Authorize(Policy = "Tasks.Write")]
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
    [Authorize(Policy = "Tasks.Read")]
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

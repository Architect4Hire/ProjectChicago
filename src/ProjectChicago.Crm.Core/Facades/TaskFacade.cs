using System.ComponentModel.DataAnnotations;
using ProjectChicago.Crm.Contracts.Common;
using ProjectChicago.Crm.Contracts.Tasks;
using ProjectChicago.Crm.Core.Business;
using ProjectChicago.Shared.Correlation;

namespace ProjectChicago.Crm.Core.Facades;

// ITaskFacade implementation for Task operations (TASK-001..022, SEC-010..013;
// onion-boundaries.md, backend.md). Owns exactly: resolving the acting user/correlation context,
// the SEC-012/013 authorization checks, and contextual request validation, before delegating to
// ITaskBusiness for defaults, persistence, retrieval, and the wire<->domain mapping. No EF,
// cache, HttpContext, or Service Bus dependency - those belong to Data/Repository, the HTTP host,
// and the outbox relay respectively (RESTRICTION: Facade does not access Data/EF). This Facade
// never maps contract fields itself - methods only resolve the actor/context/timestamp a caller
// must never supply itself and hand them, plus the untouched contract, to ITaskBusiness - and
// return its Business result straight back to the Controller.
public sealed class TaskFacade : ITaskFacade
{
    private readonly ITaskBusiness _taskBusiness;
    private readonly ITaskAuthorization _authorization;
    private readonly ICurrentRequestContext _currentRequestContext;
    private readonly IClock _clock;

    public TaskFacade(
        ITaskBusiness taskBusiness,
        ITaskAuthorization authorization,
        ICurrentRequestContext currentRequestContext,
        IClock clock)
    {
        _taskBusiness = taskBusiness ?? throw new ArgumentNullException(nameof(taskBusiness));
        _authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
        _currentRequestContext = currentRequestContext ?? throw new ArgumentNullException(nameof(currentRequestContext));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<TaskServiceModel> CreateAsync(CreateTaskViewModel request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var requestContext = _currentRequestContext.Current;

        // SEC-013: authorization is verified before any validation/business work changes or even
        // evaluates request data. TASK-001: authorization is scoped to the Project - the actor
        // must be authorized for the specific Project to which the Task is being added.
        var authorized = await _authorization.CanCreateAsync(requestContext.Actor, request.ProjectId, cancellationToken)
            .ConfigureAwait(false);
        if (!authorized)
        {
            throw new UnauthorizedAccessException(
                "The current actor is not authorized to create a Task for the specified Project (Tasks.Write).");
        }

        Validate(request);

        // TASK-001..016: the Facade does not translate/map CreateTaskViewModel itself - it
        // only authorizes and validates before handing the untouched request to
        // ITaskBusiness.CreateAsync for status/priority defaults, persistence, and mapping into
        // TaskServiceModel.
        return await _taskBusiness.CreateAsync(
            request, requestContext.Actor, requestContext, _clock.UtcNow, cancellationToken).ConfigureAwait(false);
    }

    public async Task<TaskServiceModel> AssignAsync(AssignTaskViewModel request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var requestContext = _currentRequestContext.Current;

        // SEC-013: authorization is verified before any validation/business work changes or even
        // evaluates request data. TASK-013/014: authorization for assignment (Tasks.Write).
        var authorized = await _authorization.CanAssignAsync(requestContext.Actor, cancellationToken)
            .ConfigureAwait(false);
        if (!authorized)
        {
            throw new UnauthorizedAccessException(
                "The current actor is not authorized to assign Tasks (Tasks.Write).");
        }

        Validate(request);

        // TASK-013/014: the Facade does not translate/map AssignTaskViewModel itself - it only
        // authorizes and validates before handing the untouched request to ITaskBusiness.AssignAsync
        // for fetching, mutation, persistence, and mapping into TaskServiceModel.
        return await _taskBusiness.AssignAsync(
            request, requestContext.Actor, requestContext, _clock.UtcNow, cancellationToken).ConfigureAwait(false);
    }

    public async Task<TaskServiceModel> ChangePriorityAsync(ChangeTaskPriorityViewModel request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var requestContext = _currentRequestContext.Current;

        // SEC-013: authorization is verified before any validation/business work changes or even
        // evaluates request data. TASK-015: authorization for priority change (Tasks.Write).
        var authorized = await _authorization.CanAssignAsync(requestContext.Actor, cancellationToken)
            .ConfigureAwait(false);
        if (!authorized)
        {
            throw new UnauthorizedAccessException(
                "The current actor is not authorized to change Task priority (Tasks.Write).");
        }

        Validate(request);

        // TASK-015: the Facade does not translate/map ChangeTaskPriorityViewModel itself - it only
        // authorizes and validates before handing the untouched request to ITaskBusiness.ChangePriorityAsync
        // for fetching, mutation, persistence, and mapping into TaskServiceModel.
        return await _taskBusiness.ChangePriorityAsync(
            request, requestContext.Actor, requestContext, _clock.UtcNow, cancellationToken).ConfigureAwait(false);
    }

    public async Task<PagedResponse<TaskServiceModel>> ListAsync(
        ListTasksRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var requestContext = _currentRequestContext.Current;

        // SEC-012/013: authorization for the read policy (Tasks.Read) is verified before any
        // validation/business work touches the request, mirroring CreateAsync's authorize-first
        // ordering so an unauthorized caller never learns which page/sort/filter values would have
        // failed validation.
        var authorized = await _authorization.CanListAsync(requestContext.Actor, cancellationToken)
            .ConfigureAwait(false);
        if (!authorized)
        {
            throw new UnauthorizedAccessException(
                "The current actor is not authorized to list Tasks (Tasks.Read).");
        }

        Validate(request);

        // TASK-020..022: the Facade does not translate/map ListTasksRequest itself - it only
        // authorizes and validates before handing the untouched request to
        // ITaskBusiness.ListAsync for filter translation, retrieval, and mapping into
        // PagedResponse<TaskServiceModel>.
        return await _taskBusiness.ListAsync(request, cancellationToken).ConfigureAwait(false);
    }

    public async Task<TaskServiceModel> ChangeStatusAsync(ChangeTaskStatusViewModel request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var requestContext = _currentRequestContext.Current;

        // SEC-013: authorization is verified before any validation/business work changes or even
        // evaluates request data. TASK-010: authorization for status change (Tasks.Write).
        var authorized = await _authorization.CanAssignAsync(requestContext.Actor, cancellationToken)
            .ConfigureAwait(false);
        if (!authorized)
        {
            throw new UnauthorizedAccessException(
                "The current actor is not authorized to change Task status (Tasks.Write).");
        }

        Validate(request);

        // TASK-010..012: the Facade does not translate/map ChangeTaskStatusViewModel itself - it only
        // authorizes and validates before handing the untouched request to ITaskBusiness.ChangeStatusAsync
        // for fetching, validation, mutation, persistence, and mapping into TaskServiceModel.
        return await _taskBusiness.ChangeStatusAsync(
            request, requestContext.Actor, requestContext, _clock.UtcNow, cancellationToken).ConfigureAwait(false);
    }

    public async Task<TaskServiceModel> ReopenAsync(ReopenTaskViewModel request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var requestContext = _currentRequestContext.Current;

        // SEC-013: authorization is verified before any validation/business work changes or even
        // evaluates request data. TASK-012: authorization for reopening (Tasks.Write).
        var authorized = await _authorization.CanAssignAsync(requestContext.Actor, cancellationToken)
            .ConfigureAwait(false);
        if (!authorized)
        {
            throw new UnauthorizedAccessException(
                "The current actor is not authorized to reopen Tasks (Tasks.Write).");
        }

        Validate(request);

        // TASK-012: the Facade does not translate/map ReopenTaskViewModel itself - it only
        // authorizes and validates before handing the untouched request to ITaskBusiness.ReopenAsync
        // for fetching, validation, mutation, persistence, and mapping into TaskServiceModel.
        return await _taskBusiness.ReopenAsync(
            request, requestContext.Actor, requestContext, _clock.UtcNow, cancellationToken).ConfigureAwait(false);
    }

    public async Task<TaskServiceModel> EditAsync(EditTaskViewModel request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var requestContext = _currentRequestContext.Current;

        // SEC-013: authorization is verified before any validation/business work changes or even
        // evaluates request data. TASK-002: authorization for editing (Tasks.Write).
        var authorized = await _authorization.CanAssignAsync(requestContext.Actor, cancellationToken)
            .ConfigureAwait(false);
        if (!authorized)
        {
            throw new UnauthorizedAccessException(
                "The current actor is not authorized to edit Tasks (Tasks.Write).");
        }

        Validate(request);

        // TASK-002: the Facade does not translate/map EditTaskViewModel itself - it only
        // authorizes and validates before handing the untouched request to ITaskBusiness.EditAsync
        // for fetching, validation, mutation, persistence, and mapping into TaskServiceModel.
        return await _taskBusiness.EditAsync(
            request, requestContext.Actor, requestContext, _clock.UtcNow, cancellationToken).ConfigureAwait(false);
    }

    private static void Validate(CreateTaskViewModel request) => Validate((object)request);

    private static void Validate(AssignTaskViewModel request) => Validate((object)request);

    private static void Validate(ChangeTaskPriorityViewModel request) => Validate((object)request);

    private static void Validate(ChangeTaskStatusViewModel request) => Validate((object)request);

    private static void Validate(ReopenTaskViewModel request) => Validate((object)request);

    private static void Validate(EditTaskViewModel request) => Validate((object)request);

    private static void Validate(ListTasksRequest request) => Validate((object)request);

    private static void Validate(object request)
    {
        var validationContext = new ValidationContext(request);
        var results = new List<ValidationResult>();

        if (!Validator.TryValidateObject(request, validationContext, results, validateAllProperties: true))
        {
            var message = string.Join("; ", results.Select(result => result.ErrorMessage));
            throw new ValidationException(
                string.IsNullOrWhiteSpace(message) ? "Task request failed validation." : message);
        }
    }
}

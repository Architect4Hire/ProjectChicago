using ProjectChicago.Contracts.Audit;
using ProjectChicago.Crm.Contracts.Common;
using ProjectChicago.Crm.Contracts.Tasks;
using ProjectChicago.Crm.Core.Data;
using ProjectChicago.Crm.Core.Models.DataModels.Entities;
using ProjectChicago.Crm.Core.Repositories;
using ProjectChicago.Shared.Correlation;
using static ProjectChicago.Contracts.Audit.AuditActions;

using ActorType = ProjectChicago.Shared.Correlation.ActorType;

namespace ProjectChicago.Crm.Core.Business;

// ITaskBusiness implementation for Task creation (TASK-001..016, AUDIT-001..003;
// backend.md, onion-boundaries.md). Owns exactly: normalizing business values, deciding the
// initial status/priority defaults, verifying the Project exists (DATA-003), translating the wire
// CreateTaskViewModel into the Task aggregate and the one EntityMutationAudited fact for the
// mutation, persisting both through ITaskData, and mapping the result into the wire
// TaskServiceModel (TaskContractMappingExtensions). No EF, cache, HttpContext, or Service
// Bus dependency - those belong to Data, Facade, and the outbox relay respectively.
public sealed class TaskBusiness : ITaskBusiness
{
    private readonly ITaskData _taskData;

    public TaskBusiness(ITaskData taskData)
    {
        _taskData = taskData ?? throw new ArgumentNullException(nameof(taskData));
    }

    public async Task<TaskServiceModel> CreateAsync(
        CreateTaskViewModel request,
        ActorContext actor,
        RequestContext requestContext,
        DateTime createdAtUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var normalizedTitle = NormalizeRequired(request.Title, nameof(request.Title));

        // TASK-010: initial status defaults to Backlog when omitted (CreateTaskViewModel comment
        // documents this contract expectation).
        var status = request.Status is { } statusValue
            ? statusValue.ToCoreStatus()
            : TaskItemStatus.Backlog;

        // TASK-015: initial priority defaults to Normal when omitted (CreateTaskViewModel comment
        // documents this contract expectation).
        var priority = request.Priority is { } priorityValue
            ? priorityValue.ToCorePriority()
            : TaskItemPriority.Normal;

        // TASK-001: only an identified actor (User or Service) can be attributed as CreatedBy.
        var createdBy = ResolveCreatedBy(actor);

        var task = TaskItem.Create(
            id: Guid.NewGuid(),
            projectId: request.ProjectId,
            title: normalizedTitle,
            status: status,
            priority: priority,
            createdBy: createdBy,
            createdAtUtc: createdAtUtc,
            description: NormalizeOptional(request.Description),
            assignedUserId: NormalizeOptional(request.AssignedUserId),
            startDateUtc: request.StartDateUtc,
            dueDateUtc: request.DueDateUtc,
            notes: NormalizeOptional(request.Notes));

        var auditFact = BuildAuditFact(task, actor, requestContext);

        // ITaskData.CreateAsync verifies that the Project exists (DATA-003) and persists
        // the Task and audit fact atomically, or throws TaskProjectNotFoundException if the
        // Project does not exist.
        await _taskData.CreateAsync(task, auditFact, cancellationToken).ConfigureAwait(false);

        return task.ToServiceModel();
    }

    public async Task<TaskServiceModel> AssignAsync(
        AssignTaskViewModel request,
        ActorContext actor,
        RequestContext requestContext,
        DateTime modifiedAtUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var normalizedUserId = NormalizeRequired(request.AssignedUserId, nameof(request.AssignedUserId));

        // Fetch the task for assignment. ITaskData.GetByIdAsync returns a tracked entity
        // with its current RowVersion for concurrency checking (DATA-008).
        var task = await _taskData.GetByIdAsync(request.TaskId, cancellationToken)
            .ConfigureAwait(false);

        if (task is null)
        {
            throw new ArgumentException(
                $"Task with ID '{request.TaskId}' does not exist.",
                nameof(request.TaskId));
        }

        // Decode and apply the optimistic concurrency token (DATA-008). The client sends
        // RowVersion as a base64-encoded string; decode it and set it on the task so EF Core
        // can check it at SaveChangesAsync time. If the decoded bytes don't match the current
        // RowVersion in the database, DbUpdateConcurrencyException is thrown.
        task.RowVersion = Convert.FromBase64String(request.ConcurrencyToken);

        // Determine whether this is initial assignment or reassignment, and apply the mutation.
        // SetAssigned/SetReassigned validate domain rules (cannot assign Completed task, cannot
        // reassign to same user) and return (previousUserId, newUserId) for audit construction.
        var (previousUserId, newUserId) = task.AssignedUserId is null
            ? task.SetAssigned(normalizedUserId, ResolveModifiedBy(actor), modifiedAtUtc)
            : task.SetReassigned(normalizedUserId, ResolveModifiedBy(actor), modifiedAtUtc);

        // TASK-013/014: construct audit fact for assignment or reassignment.
        var action = previousUserId is null ? AuditActions.Assigned : AuditActions.Reassigned;
        var auditFact = BuildAuditFact(task, action, previousUserId, newUserId, actor, requestContext);

        // Persist the mutated task and audit fact atomically. EF Core checks RowVersion for
        // concurrency conflicts; DbUpdateConcurrencyException is thrown if the Task has been
        // updated since fetch (optimistic locking).
        await _taskData.AssignAsync(task, auditFact, cancellationToken).ConfigureAwait(false);

        return task.ToServiceModel();
    }

    public async Task<TaskServiceModel> ChangePriorityAsync(
        ChangeTaskPriorityViewModel request,
        ActorContext actor,
        RequestContext requestContext,
        DateTime modifiedAtUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var priority = request.Priority.ToCorePriority();

        // Fetch the task for priority change. ITaskData.GetByIdAsync returns a tracked entity
        // with its current RowVersion for concurrency checking (DATA-008).
        var task = await _taskData.GetByIdAsync(request.TaskId, cancellationToken)
            .ConfigureAwait(false);

        if (task is null)
        {
            throw new ArgumentException(
                $"Task with ID '{request.TaskId}' does not exist.",
                nameof(request.TaskId));
        }

        // Decode and apply the optimistic concurrency token (DATA-008). The client sends
        // RowVersion as a base64-encoded string; decode it and set it on the task so EF Core
        // can check it at SaveChangesAsync time. If the decoded bytes don't match the current
        // RowVersion in the database, DbUpdateConcurrencyException is thrown.
        task.RowVersion = Convert.FromBase64String(request.ConcurrencyToken);

        // Apply the priority mutation. SetPriority validates that the priority is defined and
        // differs from the current priority, and returns (previousPriority, newPriority) for
        // audit construction.
        var (previousPriority, newPriority) = task.SetPriority(
            priority,
            ResolveModifiedBy(actor),
            modifiedAtUtc);

        // TASK-015: construct audit fact for priority change.
        var auditFact = BuildAuditFact(
            task,
            previousPriority,
            newPriority,
            actor,
            requestContext);

        // Persist the mutated task and audit fact atomically. EF Core checks RowVersion for
        // concurrency conflicts; DbUpdateConcurrencyException is thrown if the Task has been
        // updated since fetch (optimistic locking).
        await _taskData.ChangePriorityAsync(task, auditFact, cancellationToken).ConfigureAwait(false);

        return task.ToServiceModel();
    }

    public async Task<PagedResponse<TaskServiceModel>> ListAsync(
        ListTasksRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var statuses = ParseStatusFilters(request.Statuses);
        var priorities = ParsePriorityFilters(request.Priorities);

        var filter = new TaskListFilter
        {
            Statuses = statuses,
            Priorities = priorities,
            AssignedUserId = NormalizeOptional(request.AssignedUserId),
            ProjectId = request.ProjectId,
            ClientId = request.ClientId,
            DueDateBefore = request.DueDateBefore,
            DueDateAfter = request.DueDateAfter,
            // TASK-022 default sort: DueDateUtc ascending - ensures Overdue tasks sort first (nulls
            // first), then by actual due date. Same default as Repository.ApplySort fallback, so
            // "no sort requested" and "an unmapped sort field" never disagree about the default ordering.
            SortBy = request.SortBy?.ToCoreListSortField() ?? TaskListSortField.DueDateUtc,
            SortDirection = request.SortDirection?.ToCoreListSortDirection() ?? TaskListSortDirection.Ascending,
            Page = request.Page,
            PageSize = request.PageSize,
        };

        var result = await _taskData.ListAsync(filter, cancellationToken).ConfigureAwait(false);

        return new PagedResponse<TaskServiceModel>
        {
            Items = result.Items.Select(task => task.ToServiceModel()).ToList(),
            Page = request.Page,
            PageSize = request.PageSize,
            TotalCount = result.TotalCount,
            TotalPages = request.PageSize > 0
                ? (int)Math.Ceiling(result.TotalCount / (double)request.PageSize)
                : 0,
        };
    }

    public async Task<TaskServiceModel> ChangeStatusAsync(
        ChangeTaskStatusViewModel request,
        ActorContext actor,
        RequestContext requestContext,
        DateTime modifiedAtUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var newStatus = request.Status.ToCoreStatus();

        // Fetch the task for status change. ITaskData.GetByIdAsync returns a tracked entity
        // with its current RowVersion for concurrency checking (DATA-008).
        var task = await _taskData.GetByIdAsync(request.TaskId, cancellationToken)
            .ConfigureAwait(false);

        if (task is null)
        {
            throw new ArgumentException(
                $"Task with ID '{request.TaskId}' does not exist.",
                nameof(request.TaskId));
        }

        // Decode and apply the optimistic concurrency token (DATA-008). The client sends
        // RowVersion as a base64-encoded string; decode it and set it on the task so EF Core
        // can check it at SaveChangesAsync time. If the decoded bytes don't match the current
        // RowVersion in the database, DbUpdateConcurrencyException is thrown.
        task.RowVersion = Convert.FromBase64String(request.ConcurrencyToken);

        // Apply the status mutation. SetStatus validates that the status is defined and is an
        // allowed transition, and returns (previousStatus, newStatus) for audit construction.
        var (previousStatus, statusAfterChange) = task.SetStatus(
            newStatus,
            ResolveModifiedBy(actor),
            modifiedAtUtc);

        // TASK-010..012: construct audit fact for status change. The action is "Completed"
        // when transitioning to Completed, otherwise "StatusChanged".
        var action = statusAfterChange == TaskItemStatus.Completed
            ? AuditActions.Completed
            : AuditActions.StatusChanged;
        var auditFact = BuildAuditFact(
            task,
            action,
            previousStatus,
            statusAfterChange,
            actor,
            requestContext);

        // Persist the mutated task and audit fact atomically. EF Core checks RowVersion for
        // concurrency conflicts; DbUpdateConcurrencyException is thrown if the Task has been
        // updated since fetch (optimistic locking).
        await _taskData.ChangeStatusAsync(task, auditFact, cancellationToken).ConfigureAwait(false);

        return task.ToServiceModel();
    }

    public async Task<TaskServiceModel> ReopenAsync(
        ReopenTaskViewModel request,
        ActorContext actor,
        RequestContext requestContext,
        DateTime modifiedAtUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var reopenToStatus = request.ReopenToStatus.ToCoreStatus();

        // Fetch the task for reopening. ITaskData.GetByIdAsync returns a tracked entity
        // with its current RowVersion for concurrency checking (DATA-008).
        var task = await _taskData.GetByIdAsync(request.TaskId, cancellationToken)
            .ConfigureAwait(false);

        if (task is null)
        {
            throw new ArgumentException(
                $"Task with ID '{request.TaskId}' does not exist.",
                nameof(request.TaskId));
        }

        // Decode and apply the optimistic concurrency token (DATA-008). The client sends
        // RowVersion as a base64-encoded string; decode it and set it on the task so EF Core
        // can check it at SaveChangesAsync time. If the decoded bytes don't match the current
        // RowVersion in the database, DbUpdateConcurrencyException is thrown.
        task.RowVersion = Convert.FromBase64String(request.ConcurrencyToken);

        // Apply the reopen mutation. SetReopen validates that the Task is Completed and the
        // target status is not Completed/Cancelled, and returns (previousStatus, newStatus)
        // for audit construction.
        var (previousStatus, statusAfterReopen) = task.SetReopen(
            reopenToStatus,
            ResolveModifiedBy(actor),
            modifiedAtUtc);

        // TASK-012: construct audit fact for reopening with Action="Reopened".
        var auditFact = BuildAuditFact(
            task,
            AuditActions.Reopened,
            previousStatus,
            statusAfterReopen,
            actor,
            requestContext);

        // Persist the mutated task and audit fact atomically. EF Core checks RowVersion for
        // concurrency conflicts; DbUpdateConcurrencyException is thrown if the Task has been
        // updated since fetch (optimistic locking).
        await _taskData.ReopenAsync(task, auditFact, cancellationToken).ConfigureAwait(false);

        return task.ToServiceModel();
    }

    private static IReadOnlySet<TaskItemStatus>? ParseStatusFilters(string? statusesStr)
    {
        if (string.IsNullOrWhiteSpace(statusesStr))
        {
            return null;
        }

        var statuses = new HashSet<TaskItemStatus>();
        var parts = statusesStr.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var part in parts)
        {
            if (Enum.TryParse<TaskItemStatusContract>(part, ignoreCase: true, out var statusContract))
            {
                statuses.Add(statusContract.ToCoreStatus());
            }
        }

        return statuses.Count > 0 ? statuses : null;
    }

    private static IReadOnlySet<TaskItemPriority>? ParsePriorityFilters(string? prioritiesStr)
    {
        if (string.IsNullOrWhiteSpace(prioritiesStr))
        {
            return null;
        }

        var priorities = new HashSet<TaskItemPriority>();
        var parts = prioritiesStr.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var part in parts)
        {
            if (Enum.TryParse<TaskItemPriorityContract>(part, ignoreCase: true, out var priorityContract))
            {
                priorities.Add(priorityContract.ToCorePriority());
            }
        }

        return priorities.Count > 0 ? priorities : null;
    }

    private static string NormalizeRequired(string? value, string paramName) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Value cannot be null or whitespace.", paramName)
            : value.Trim();

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string ResolveCreatedBy(ActorContext actor)
    {
        if (!string.IsNullOrWhiteSpace(actor.ActorId))
        {
            return actor.ActorId;
        }

        throw new InvalidOperationException(
            "ActorContext must contain an ActorId to record as CreatedBy.");
    }

    private static EntityMutationAudited BuildAuditFact(
        TaskItem task,
        ActorContext actor,
        RequestContext requestContext)
    {
        var changedFields = new List<string>
        {
            nameof(TaskItem.Title),
            nameof(TaskItem.Status),
            nameof(TaskItem.Priority),
        };

        if (!string.IsNullOrWhiteSpace(task.Description))
        {
            changedFields.Add(nameof(TaskItem.Description));
        }

        if (!string.IsNullOrWhiteSpace(task.AssignedUserId))
        {
            changedFields.Add(nameof(TaskItem.AssignedUserId));
        }

        if (task.StartDateUtc.HasValue)
        {
            changedFields.Add(nameof(TaskItem.StartDateUtc));
        }

        if (task.DueDateUtc.HasValue)
        {
            changedFields.Add(nameof(TaskItem.DueDateUtc));
        }

        if (!string.IsNullOrWhiteSpace(task.Notes))
        {
            changedFields.Add(nameof(TaskItem.Notes));
        }

        return new EntityMutationAudited
        {
            EventId = Guid.NewGuid().ToString(),
            OccurredAtUtc = new DateTimeOffset(task.CreatedAtUtc),
            SourceService = AuditSourceServices.Crm,
            EntityType = AuditEntityTypes.Task,
            EntityId = task.Id,
            Action = AuditActions.Created,
            ActorId = ResolveCreatedBy(actor),
            ActorType = actor.ActorType switch
            {
                ActorType.User => AuditActorTypes.User,
                ActorType.Service => AuditActorTypes.Service,
                _ => AuditActorTypes.Service,
            },
            TraceId = requestContext.TraceId,
            CorrelationId = requestContext.CorrelationId,
            CausationId = requestContext.CausationId,
            ChangedFields = changedFields,
        };
    }

    private static EntityMutationAudited BuildAuditFact(
        TaskItem task,
        string action,
        string? previousUserId,
        string newUserId,
        ActorContext actor,
        RequestContext requestContext)
    {
        var changedFields = new List<string> { nameof(TaskItem.AssignedUserId) };

        var previousValues = previousUserId is not null
            ? new Dictionary<string, string> { { nameof(TaskItem.AssignedUserId), previousUserId } }
            : null;

        var newValues = new Dictionary<string, string> { { nameof(TaskItem.AssignedUserId), newUserId } };

        return new EntityMutationAudited
        {
            EventId = Guid.NewGuid().ToString(),
            OccurredAtUtc = new DateTimeOffset(task.LastModifiedAtUtc),
            SourceService = AuditSourceServices.Crm,
            EntityType = AuditEntityTypes.Task,
            EntityId = task.Id,
            Action = action,
            ActorId = ResolveModifiedBy(actor),
            ActorType = actor.ActorType switch
            {
                ActorType.User => AuditActorTypes.User,
                ActorType.Service => AuditActorTypes.Service,
                _ => AuditActorTypes.Service,
            },
            TraceId = requestContext.TraceId,
            CorrelationId = requestContext.CorrelationId,
            CausationId = requestContext.CausationId,
            ChangedFields = changedFields,
            PreviousValues = previousValues,
            NewValues = newValues,
        };
    }

    private static EntityMutationAudited BuildAuditFact(
        TaskItem task,
        TaskItemPriority previousPriority,
        TaskItemPriority newPriority,
        ActorContext actor,
        RequestContext requestContext)
    {
        var changedFields = new List<string> { nameof(TaskItem.Priority) };
        var previousValues = new Dictionary<string, string> { { nameof(TaskItem.Priority), previousPriority.ToString() } };
        var newValues = new Dictionary<string, string> { { nameof(TaskItem.Priority), newPriority.ToString() } };

        return new EntityMutationAudited
        {
            EventId = Guid.NewGuid().ToString(),
            OccurredAtUtc = new DateTimeOffset(task.LastModifiedAtUtc),
            SourceService = AuditSourceServices.Crm,
            EntityType = AuditEntityTypes.Task,
            EntityId = task.Id,
            Action = PriorityChanged,
            ActorId = ResolveModifiedBy(actor),
            ActorType = actor.ActorType switch
            {
                ActorType.User => AuditActorTypes.User,
                ActorType.Service => AuditActorTypes.Service,
                _ => AuditActorTypes.Service,
            },
            TraceId = requestContext.TraceId,
            CorrelationId = requestContext.CorrelationId,
            CausationId = requestContext.CausationId,
            ChangedFields = changedFields,
            PreviousValues = previousValues,
            NewValues = newValues,
        };
    }

    private static EntityMutationAudited BuildAuditFact(
        TaskItem task,
        string action,
        TaskItemStatus previousStatus,
        TaskItemStatus newStatus,
        ActorContext actor,
        RequestContext requestContext)
    {
        var changedFields = new List<string> { nameof(TaskItem.Status) };

        var previousValues = new Dictionary<string, string> { { nameof(TaskItem.Status), previousStatus.ToString() } };
        var newValues = new Dictionary<string, string> { { nameof(TaskItem.Status), newStatus.ToString() } };

        // When transitioning to Completed, include the CompletedAtUtc timestamp in the audit record.
        if (newStatus == TaskItemStatus.Completed && task.CompletedAtUtc.HasValue)
        {
            newValues[nameof(TaskItem.CompletedAtUtc)] = task.CompletedAtUtc.Value.ToString("O");
        }

        // When reopening (transitioning from Completed), the CompletedAtUtc has been cleared.
        if (previousStatus == TaskItemStatus.Completed)
        {
            previousValues[nameof(TaskItem.CompletedAtUtc)] = "Cleared on reopen";
        }

        return new EntityMutationAudited
        {
            EventId = Guid.NewGuid().ToString(),
            OccurredAtUtc = new DateTimeOffset(task.LastModifiedAtUtc),
            SourceService = AuditSourceServices.Crm,
            EntityType = AuditEntityTypes.Task,
            EntityId = task.Id,
            Action = action,
            ActorId = ResolveModifiedBy(actor),
            ActorType = actor.ActorType switch
            {
                ActorType.User => AuditActorTypes.User,
                ActorType.Service => AuditActorTypes.Service,
                _ => AuditActorTypes.Service,
            },
            TraceId = requestContext.TraceId,
            CorrelationId = requestContext.CorrelationId,
            CausationId = requestContext.CausationId,
            ChangedFields = changedFields,
            PreviousValues = previousValues,
            NewValues = newValues,
        };
    }

    public async Task<TaskServiceModel> EditAsync(
        EditTaskViewModel request,
        ActorContext actor,
        RequestContext requestContext,
        DateTime modifiedAtUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Fetch the task for editing. ITaskData.GetByIdAsync returns a tracked entity
        // with its current RowVersion for concurrency checking (DATA-008).
        var task = await _taskData.GetByIdAsync(request.TaskId, cancellationToken)
            .ConfigureAwait(false);

        if (task is null)
        {
            throw new ArgumentException(
                $"Task with ID '{request.TaskId}' does not exist.",
                nameof(request.TaskId));
        }

        // Decode and apply the optimistic concurrency token (DATA-008). The client sends
        // RowVersion as a base64-encoded string; decode it and set it on the task so EF Core
        // can check it at SaveChangesAsync time. If the decoded bytes don't match the current
        // RowVersion in the database, DbUpdateConcurrencyException is thrown.
        task.RowVersion = Convert.FromBase64String(request.ConcurrencyToken);

        // Apply the edit mutation. Edit normalizes input, validates dates are UTC, and returns
        // before/after values for audit construction. Null request fields are treated as
        // "no change" to that task field.
        var changes = task.Edit(
            newTitle: NormalizeOptional(request.Title),
            newDescription: request.Description,
            newStartDateUtc: request.StartDateUtc,
            newDueDateUtc: request.DueDateUtc,
            newNotes: request.Notes,
            modifiedBy: ResolveModifiedBy(actor),
            modifiedAtUtc: modifiedAtUtc);

        // Idempotency check: if no fields actually changed, reject the operation.
        if (!changes.HasChanges)
        {
            throw new InvalidOperationException(
                "No fields were changed; the Task is already in the requested state.");
        }

        // Construct audit fact for the edit with Action="Updated".
        var auditFact = BuildAuditFact(task, changes, actor, requestContext);

        // Persist the mutated task and audit fact atomically. EF Core checks RowVersion for
        // concurrency conflicts; DbUpdateConcurrencyException is thrown if the Task has been
        // updated since fetch (optimistic locking).
        await _taskData.EditAsync(task, auditFact, cancellationToken).ConfigureAwait(false);

        return task.ToServiceModel();
    }

    private static EntityMutationAudited BuildAuditFact(
        TaskItem task,
        TaskEditChanges changes,
        ActorContext actor,
        RequestContext requestContext)
    {
        var changedFields = new List<string>();
        var previousValues = new Dictionary<string, string>();
        var newValues = new Dictionary<string, string>();

        if (changes.NewTitle is not null)
        {
            changedFields.Add(nameof(TaskItem.Title));
            previousValues[nameof(TaskItem.Title)] = changes.PreviousTitle ?? string.Empty;
            newValues[nameof(TaskItem.Title)] = changes.NewTitle;
        }

        if (changes.NewDescription is not null)
        {
            changedFields.Add(nameof(TaskItem.Description));
            previousValues[nameof(TaskItem.Description)] = changes.PreviousDescription ?? string.Empty;
            newValues[nameof(TaskItem.Description)] = changes.NewDescription ?? string.Empty;
        }

        if (changes.NewStartDateUtc.HasValue)
        {
            changedFields.Add(nameof(TaskItem.StartDateUtc));
            previousValues[nameof(TaskItem.StartDateUtc)] = changes.PreviousStartDateUtc?.ToString("O") ?? string.Empty;
            newValues[nameof(TaskItem.StartDateUtc)] = changes.NewStartDateUtc?.ToString("O") ?? string.Empty;
        }

        if (changes.NewDueDateUtc.HasValue)
        {
            changedFields.Add(nameof(TaskItem.DueDateUtc));
            previousValues[nameof(TaskItem.DueDateUtc)] = changes.PreviousDueDateUtc?.ToString("O") ?? string.Empty;
            newValues[nameof(TaskItem.DueDateUtc)] = changes.NewDueDateUtc?.ToString("O") ?? string.Empty;
        }

        if (changes.NewNotes is not null)
        {
            changedFields.Add(nameof(TaskItem.Notes));
            previousValues[nameof(TaskItem.Notes)] = changes.PreviousNotes ?? string.Empty;
            newValues[nameof(TaskItem.Notes)] = changes.NewNotes ?? string.Empty;
        }

        return new EntityMutationAudited
        {
            EventId = Guid.NewGuid().ToString(),
            OccurredAtUtc = new DateTimeOffset(task.LastModifiedAtUtc),
            SourceService = AuditSourceServices.Crm,
            EntityType = AuditEntityTypes.Task,
            EntityId = task.Id,
            Action = AuditActions.Updated,
            ActorId = ResolveModifiedBy(actor),
            ActorType = actor.ActorType switch
            {
                ActorType.User => AuditActorTypes.User,
                ActorType.Service => AuditActorTypes.Service,
                _ => AuditActorTypes.Service,
            },
            TraceId = requestContext.TraceId,
            CorrelationId = requestContext.CorrelationId,
            CausationId = requestContext.CausationId,
            ChangedFields = changedFields,
            PreviousValues = previousValues,
            NewValues = newValues,
        };
    }

    private static string ResolveModifiedBy(ActorContext actor)
    {
        if (!string.IsNullOrWhiteSpace(actor.ActorId))
        {
            return actor.ActorId;
        }

        throw new InvalidOperationException(
            "ActorContext must contain an ActorId to record as LastModifiedBy.");
    }
}

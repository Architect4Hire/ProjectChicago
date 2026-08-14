namespace ProjectChicago.Crm.Core.Models.DataModels.Entities;

// CRM's Task entity (TASK-001..016, DATA-003, DATA-006..008). Named TaskItem rather than Task to
// avoid colliding with System.Threading.Tasks.Task, which every async Facade/Business/Data/
// Controller/Function layer above this entity will also have in scope. Id is an
// application-assigned GUID rather than a database-generated sequential value, matching Client and
// Project (DATA-007). Construction is the only way to reach a valid TaskItem, so every invariant
// below holds for the lifetime of the instance; state-transition rules beyond construction belong
// to the CRM Business layer (backend.md), not this entity.
public sealed class TaskItem
{
    private TaskItem()
    {
    }

    public Guid Id { get; private set; }

    // DATA-003: every Task belongs to exactly one Project; a Task cannot exist without one. No
    // in-memory Project reference is kept - Data/Repository resolve the relationship
    // (onion-boundaries.md keeps entities free of cross-layer navigation concerns).
    public Guid ProjectId { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public TaskItemStatus Status { get; private set; }

    public TaskItemPriority Priority { get; private set; }

    // TASK-013: assignment (and reassignment) is a distinct, later action a Business-layer
    // operation performs, so unlike Client/Project's OwnerUserId this is optional at creation.
    public string? AssignedUserId { get; private set; }

    public DateTime? StartDateUtc { get; private set; }

    public DateTime? DueDateUtc { get; private set; }

    public DateTime? CompletedAtUtc { get; private set; }

    public string? Notes { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public string CreatedBy { get; private set; } = string.Empty;

    public DateTime LastModifiedAtUtc { get; private set; }

    public string LastModifiedBy { get; private set; } = string.Empty;

    // Optimistic concurrency token (DATA-008). Used by EF Core to detect concurrent updates.
    // The client sends this as a base64-encoded string in the ConcurrencyToken request field;
    // Business decodes it and sets it here before persisting so EF Core can check it.
    public byte[] RowVersion { get; set; } = [];

    public static TaskItem Create(
        Guid id,
        Guid projectId,
        string title,
        TaskItemStatus status,
        TaskItemPriority priority,
        string createdBy,
        DateTime createdAtUtc,
        string? description = null,
        string? assignedUserId = null,
        DateTime? startDateUtc = null,
        DateTime? dueDateUtc = null,
        DateTime? completedAtUtc = null,
        string? notes = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("TaskItem Id cannot be empty.", nameof(id));
        }

        if (projectId == Guid.Empty)
        {
            throw new ArgumentException("A Task cannot exist without a Project (DATA-003).", nameof(projectId));
        }

        if (!Enum.IsDefined(status))
        {
            throw new ArgumentException("Status must be a defined TaskItemStatus value.", nameof(status));
        }

        if (!Enum.IsDefined(priority))
        {
            throw new ArgumentException("Priority must be a defined TaskItemPriority value.", nameof(priority));
        }

        var validCreatedAtUtc = RequireUtc(createdAtUtc, nameof(createdAtUtc));
        var validStartDateUtc = RequireUtcIfProvided(startDateUtc, nameof(startDateUtc));
        var validDueDateUtc = RequireUtcIfProvided(dueDateUtc, nameof(dueDateUtc));
        var validCompletedAtUtc = RequireUtcIfProvided(completedAtUtc, nameof(completedAtUtc));

        // TASK-011: completing a Task records its completion date and time. Enforced here (not only
        // in Business) so a Task can never be constructed - by any entry point - as Completed
        // without one.
        if (status == TaskItemStatus.Completed && validCompletedAtUtc is null)
        {
            throw new ArgumentException(
                "A Completed Task must have a completed date/time (TASK-011).",
                nameof(completedAtUtc));
        }

        // Last-modified metadata starts identical to created metadata; it only diverges once a
        // later Business-layer mutation touches the record.
        return new TaskItem
        {
            Id = id,
            ProjectId = projectId,
            Title = RequireText(title, nameof(title)),
            Description = description,
            Status = status,
            Priority = priority,
            AssignedUserId = assignedUserId,
            StartDateUtc = validStartDateUtc,
            DueDateUtc = validDueDateUtc,
            CompletedAtUtc = validCompletedAtUtc,
            Notes = notes,
            CreatedBy = RequireText(createdBy, nameof(createdBy)),
            CreatedAtUtc = validCreatedAtUtc,
            LastModifiedBy = createdBy,
            LastModifiedAtUtc = validCreatedAtUtc,
        };
    }

    private static string RequireText(string? value, string paramName) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Value cannot be null or whitespace.", paramName)
            : value;

    private static DateTime RequireUtc(DateTime value, string paramName) =>
        value.Kind == DateTimeKind.Utc
            ? value
            : throw new ArgumentException("Value must be a UTC DateTime (DATA-006).", paramName);

    private static DateTime? RequireUtcIfProvided(DateTime? value, string paramName) =>
        value.HasValue ? RequireUtc(value.Value, paramName) : null;

    // TASK-013/014: assign a Task to a user (initial assignment). Validates that the Task
    // is not Completed and returns before/after values for audit fact construction.
    public (string? previousUserId, string newUserId) SetAssigned(
        string assignedUserId,
        string modifiedBy,
        DateTime modifiedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(assignedUserId))
        {
            throw new ArgumentException("AssignedUserId cannot be null or whitespace.", nameof(assignedUserId));
        }

        if (Status == TaskItemStatus.Completed)
        {
            throw new InvalidOperationException(
                "A Completed Task cannot be assigned (TASK-013).");
        }

        var previousUserId = AssignedUserId;
        AssignedUserId = assignedUserId;
        LastModifiedBy = RequireText(modifiedBy, nameof(modifiedBy));
        LastModifiedAtUtc = RequireUtc(modifiedAtUtc, nameof(modifiedAtUtc));

        return (previousUserId, assignedUserId);
    }

    // TASK-013/014: reassign a Task to a different user. Validates that the Task is not
    // Completed, not already assigned to the same user, and returns before/after values for
    // audit fact construction.
    public (string? previousUserId, string newUserId) SetReassigned(
        string assignedUserId,
        string modifiedBy,
        DateTime modifiedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(assignedUserId))
        {
            throw new ArgumentException("AssignedUserId cannot be null or whitespace.", nameof(assignedUserId));
        }

        if (Status == TaskItemStatus.Completed)
        {
            throw new InvalidOperationException(
                "A Completed Task cannot be reassigned (TASK-013).");
        }

        if (AssignedUserId == assignedUserId)
        {
            throw new InvalidOperationException(
                "Task is already assigned to the specified user (TASK-014).");
        }

        var previousUserId = AssignedUserId;
        AssignedUserId = assignedUserId;
        LastModifiedBy = RequireText(modifiedBy, nameof(modifiedBy));
        LastModifiedAtUtc = RequireUtc(modifiedAtUtc, nameof(modifiedAtUtc));

        return (previousUserId, assignedUserId);
    }

    // TASK-015: change a Task's priority. Validates that the priority is a defined value and
    // returns before/after values for audit fact construction (AUDIT-001..008).
    public (TaskItemPriority previousPriority, TaskItemPriority newPriority) SetPriority(
        TaskItemPriority priority,
        string modifiedBy,
        DateTime modifiedAtUtc)
    {
        if (!Enum.IsDefined(priority))
        {
            throw new ArgumentException("Priority must be a defined TaskItemPriority value.", nameof(priority));
        }

        if (Priority == priority)
        {
            throw new InvalidOperationException(
                "Task is already set to the specified priority (TASK-015).");
        }

        var previousPriority = Priority;
        Priority = priority;
        LastModifiedBy = RequireText(modifiedBy, nameof(modifiedBy));
        LastModifiedAtUtc = RequireUtc(modifiedAtUtc, nameof(modifiedAtUtc));

        return (previousPriority, priority);
    }

    // TASK-010..012: transition a Task to a new status. Validates that the status is defined,
    // differs from current, and is an allowed transition. Handles completion timestamp: when
    // transitioning TO Completed, sets CompletedAtUtc; when transitioning FROM Completed,
    // clears it. Returns (previousStatus, newStatus) for audit fact construction.
    public (TaskItemStatus previousStatus, TaskItemStatus newStatus) SetStatus(
        TaskItemStatus newStatus,
        string modifiedBy,
        DateTime modifiedAtUtc)
    {
        if (!Enum.IsDefined(newStatus))
        {
            throw new ArgumentException("Status must be a defined TaskItemStatus value.", nameof(newStatus));
        }

        if (Status == newStatus)
        {
            throw new InvalidOperationException(
                "Task is already set to the specified status (TASK-010).");
        }

        // Validate state transition rules (TASK-010..012). Completed and Cancelled are terminal
        // for SetStatus; use Reopen for Completed->other transitions.
        if (Status == TaskItemStatus.Completed)
        {
            throw new InvalidOperationException(
                "A Completed Task cannot transition to another status via SetStatus; use Reopen instead (TASK-012).");
        }

        if (Status == TaskItemStatus.Cancelled)
        {
            throw new InvalidOperationException(
                "A Cancelled Task cannot transition to another status (TASK-010).");
        }

        var previousStatus = Status;

        // TASK-011: when transitioning TO Completed, record the completion timestamp. When
        // transitioning away from Completed (which Reopen handles), clear it. Non-Completed
        // states must have CompletedAtUtc = null.
        if (newStatus == TaskItemStatus.Completed)
        {
            CompletedAtUtc = RequireUtc(modifiedAtUtc, nameof(modifiedAtUtc));
        }
        else
        {
            CompletedAtUtc = null;
        }

        Status = newStatus;
        LastModifiedBy = RequireText(modifiedBy, nameof(modifiedBy));
        LastModifiedAtUtc = RequireUtc(modifiedAtUtc, nameof(modifiedAtUtc));

        return (previousStatus, newStatus);
    }

    // TASK-012: reopen a completed Task, transitioning it back to a specified open status.
    // Validates that the Task is Completed and the target status is not Completed/Cancelled.
    // Clears the CompletedAtUtc timestamp and returns (previousStatus, newStatus) for audit
    // fact construction.
    public (TaskItemStatus previousStatus, TaskItemStatus newStatus) SetReopen(
        TaskItemStatus reopenToStatus,
        string modifiedBy,
        DateTime modifiedAtUtc)
    {
        if (!Enum.IsDefined(reopenToStatus))
        {
            throw new ArgumentException("Status must be a defined TaskItemStatus value.", nameof(reopenToStatus));
        }

        if (Status != TaskItemStatus.Completed)
        {
            throw new InvalidOperationException(
                "Only a Completed Task can be reopened (TASK-012).");
        }

        if (reopenToStatus == TaskItemStatus.Completed || reopenToStatus == TaskItemStatus.Cancelled)
        {
            throw new InvalidOperationException(
                "A reopened Task must transition to an open status, not Completed or Cancelled (TASK-012).");
        }

        var previousStatus = Status;

        // TASK-011: clearing CompletedAtUtc when reopening because the Task is no longer completed.
        CompletedAtUtc = null;
        Status = reopenToStatus;
        LastModifiedBy = RequireText(modifiedBy, nameof(modifiedBy));
        LastModifiedAtUtc = RequireUtc(modifiedAtUtc, nameof(modifiedAtUtc));

        return (previousStatus, reopenToStatus);
    }

    // Edit title, description, start/due dates, and notes. Returns before/after values for
    // audit fact construction. All input parameters are nullable except modifiedBy and
    // modifiedAtUtc - null values in the provided parameters are treated as "no change",
    // and will not overwrite the current value. This allows partial updates.
    public TaskEditChanges Edit(
        string? newTitle,
        string? newDescription,
        DateTime? newStartDateUtc,
        DateTime? newDueDateUtc,
        string? newNotes,
        string modifiedBy,
        DateTime modifiedAtUtc)
    {
        var changes = new TaskEditChanges();

        // Validate and update title if provided.
        if (newTitle is not null)
        {
            var normalizedTitle = RequireText(newTitle, nameof(newTitle));
            if (Title != normalizedTitle)
            {
                changes.PreviousTitle = Title;
                Title = normalizedTitle;
                changes.NewTitle = normalizedTitle;
            }
        }

        // Update description if provided (allow empty to clear).
        if (newDescription is not null)
        {
            var normalizedDescription = string.IsNullOrWhiteSpace(newDescription) ? null : newDescription.Trim();
            if (Description != normalizedDescription)
            {
                changes.PreviousDescription = Description;
                Description = normalizedDescription;
                changes.NewDescription = normalizedDescription;
            }
        }

        // Update start date if provided.
        if (newStartDateUtc.HasValue)
        {
            var validStartDateUtc = RequireUtc(newStartDateUtc.Value, nameof(newStartDateUtc));
            if (StartDateUtc != validStartDateUtc)
            {
                changes.PreviousStartDateUtc = StartDateUtc;
                StartDateUtc = validStartDateUtc;
                changes.NewStartDateUtc = validStartDateUtc;
            }
        }

        // Update due date if provided.
        if (newDueDateUtc.HasValue)
        {
            var validDueDateUtc = RequireUtc(newDueDateUtc.Value, nameof(newDueDateUtc));
            if (DueDateUtc != validDueDateUtc)
            {
                changes.PreviousDueDateUtc = DueDateUtc;
                DueDateUtc = validDueDateUtc;
                changes.NewDueDateUtc = validDueDateUtc;
            }
        }

        // Update notes if provided (allow empty to clear).
        if (newNotes is not null)
        {
            var normalizedNotes = string.IsNullOrWhiteSpace(newNotes) ? null : newNotes.Trim();
            if (Notes != normalizedNotes)
            {
                changes.PreviousNotes = Notes;
                Notes = normalizedNotes;
                changes.NewNotes = normalizedNotes;
            }
        }

        // Update last modified metadata only if at least one field changed.
        if (changes.HasChanges)
        {
            LastModifiedBy = RequireText(modifiedBy, nameof(modifiedBy));
            LastModifiedAtUtc = RequireUtc(modifiedAtUtc, nameof(modifiedAtUtc));
        }

        return changes;
    }
}

// Result of Edit operation, capturing what changed for audit trail construction.
public sealed class TaskEditChanges
{
    public string? PreviousTitle { get; set; }
    public string? NewTitle { get; set; }

    public string? PreviousDescription { get; set; }
    public string? NewDescription { get; set; }

    public DateTime? PreviousStartDateUtc { get; set; }
    public DateTime? NewStartDateUtc { get; set; }

    public DateTime? PreviousDueDateUtc { get; set; }
    public DateTime? NewDueDateUtc { get; set; }

    public string? PreviousNotes { get; set; }
    public string? NewNotes { get; set; }

    public bool HasChanges =>
        NewTitle is not null ||
        NewDescription is not null ||
        NewStartDateUtc.HasValue ||
        NewDueDateUtc.HasValue ||
        NewNotes is not null;
}

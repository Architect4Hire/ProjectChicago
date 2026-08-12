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

    // Optimistic concurrency token (DATA-008). Empty until the Data layer's EF mapping assigns it;
    // that mapping is out of scope for this microstep.
    public byte[] RowVersion { get; private set; } = [];

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
}

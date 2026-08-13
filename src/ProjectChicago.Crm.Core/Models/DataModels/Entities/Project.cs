namespace ProjectChicago.Crm.Core.Models.DataModels.Entities;

// CRM's Project entity (PROJECT-001..014, DATA-001..008). Id is an application-assigned GUID
// rather than a database-generated sequential value, matching Client (DATA-007). Construction is
// the only way to reach a valid Project, so every invariant below holds for the lifetime of the
// instance; state-transition rules beyond construction belong to the CRM Business layer
// (backend.md), not this entity.
public sealed class Project
{
    private Project()
    {
    }

    public Guid Id { get; private set; }

    // DATA-001/DATA-002: every Project belongs to exactly one Client; a Project cannot exist
    // without one. No in-memory Client reference is kept - Data/Repository resolve the
    // relationship (onion-boundaries.md keeps entities free of cross-layer navigation concerns).
    public Guid ClientId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public ProjectStatus Status { get; private set; }

    public ProjectPriority Priority { get; private set; }

    // Same actor-identifier convention as Client.OwnerUserId (ActorContext.ActorId shape) rather
    // than a type borrowed from an as-yet-unowned Identity store.
    public string OwnerUserId { get; private set; } = string.Empty;

    public DateTime? StartDateUtc { get; private set; }

    public DateTime? TargetCompletionDateUtc { get; private set; }

    public DateTime? ActualCompletionDateUtc { get; private set; }

    public string? Notes { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public string CreatedBy { get; private set; } = string.Empty;

    public DateTime LastModifiedAtUtc { get; private set; }

    public string LastModifiedBy { get; private set; } = string.Empty;

    // Optimistic concurrency token (DATA-008). Empty until the Data layer's EF mapping assigns it;
    // that mapping is out of scope for this microstep.
    public byte[] RowVersion { get; private set; } = [];

    public static Project Create(
        Guid id,
        Guid clientId,
        string name,
        ProjectStatus status,
        ProjectPriority priority,
        string ownerUserId,
        string createdBy,
        DateTime createdAtUtc,
        string? description = null,
        DateTime? startDateUtc = null,
        DateTime? targetCompletionDateUtc = null,
        DateTime? actualCompletionDateUtc = null,
        string? notes = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Project Id cannot be empty.", nameof(id));
        }

        if (clientId == Guid.Empty)
        {
            throw new ArgumentException("A Project cannot exist without a Client (DATA-002).", nameof(clientId));
        }

        if (!Enum.IsDefined(status))
        {
            throw new ArgumentException("Status must be a defined ProjectStatus value.", nameof(status));
        }

        if (!Enum.IsDefined(priority))
        {
            throw new ArgumentException("Priority must be a defined ProjectPriority value.", nameof(priority));
        }

        var validCreatedAtUtc = RequireUtc(createdAtUtc, nameof(createdAtUtc));
        var validStartDateUtc = RequireUtcIfProvided(startDateUtc, nameof(startDateUtc));
        var validTargetCompletionDateUtc = RequireUtcIfProvided(targetCompletionDateUtc, nameof(targetCompletionDateUtc));
        var validActualCompletionDateUtc = RequireUtcIfProvided(actualCompletionDateUtc, nameof(actualCompletionDateUtc));

        // PROJECT-012: completing a Project captures an actual completion timestamp. Enforced here
        // (not only in Business) so a Project can never be constructed - by any entry point - as
        // Completed without one.
        if (status == ProjectStatus.Completed && validActualCompletionDateUtc is null)
        {
            throw new ArgumentException(
                "A Completed Project must have an actual completion timestamp (PROJECT-012).",
                nameof(actualCompletionDateUtc));
        }

        // Last-modified metadata starts identical to created metadata; it only diverges once a
        // later Business-layer mutation touches the record.
        return new Project
        {
            Id = id,
            ClientId = clientId,
            Name = RequireText(name, nameof(name)),
            Description = description,
            Status = status,
            Priority = priority,
            OwnerUserId = RequireText(ownerUserId, nameof(ownerUserId)),
            StartDateUtc = validStartDateUtc,
            TargetCompletionDateUtc = validTargetCompletionDateUtc,
            ActualCompletionDateUtc = validActualCompletionDateUtc,
            Notes = notes,
            CreatedBy = RequireText(createdBy, nameof(createdBy)),
            CreatedAtUtc = validCreatedAtUtc,
            LastModifiedBy = createdBy,
            LastModifiedAtUtc = validCreatedAtUtc,
        };
    }

    // PROJECT-010..014: transitions Project status and records completion timestamp when moving to
    // Completed status. Allowed transitions: Planned→Active, Planned→Cancelled, Active→OnHold,
    // Active→Completed (requires acknowledgement of open Tasks), OnHold→Active, OnHold→Cancelled,
    // Completed/Cancelled→Archived. All other transitions are rejected.
    // Business layer validates open Tasks before calling this when target is Completed.
    public void TransitionStatus(
        ProjectStatus targetStatus,
        string modifiedBy,
        DateTime modifiedAtUtc,
        DateTime? completionTimestampUtc = null)
    {
        var validModifiedAtUtc = RequireUtc(modifiedAtUtc, nameof(modifiedAtUtc));

        if (!IsValidTransition(Status, targetStatus))
        {
            throw new InvalidOperationException(
                $"Cannot transition Project status from {Status} to {targetStatus}.");
        }

        if (targetStatus == ProjectStatus.Completed)
        {
            var validCompletionTimestampUtc = completionTimestampUtc.HasValue
                ? RequireUtc(completionTimestampUtc.Value, nameof(completionTimestampUtc))
                : throw new ArgumentException(
                    "Completing a Project requires an actual completion timestamp (PROJECT-012).",
                    nameof(completionTimestampUtc));

            ActualCompletionDateUtc = validCompletionTimestampUtc;
        }
        else if (targetStatus != ProjectStatus.Archived && Status == ProjectStatus.Completed)
        {
            // Only Archived can transition away from Completed, and it preserves the completion timestamp.
            throw new InvalidOperationException(
                $"A Completed Project can only transition to Archived, not {targetStatus}.");
        }

        Status = targetStatus;
        LastModifiedBy = RequireText(modifiedBy, nameof(modifiedBy));
        LastModifiedAtUtc = validModifiedAtUtc;
    }

    // PROJECT-014: archive is non-destructive; archived Projects retain all history and can transition
    // from either Completed or Cancelled status.
    public void Archive(string modifiedBy, DateTime modifiedAtUtc)
    {
        if (Status != ProjectStatus.Completed && Status != ProjectStatus.Cancelled)
        {
            throw new InvalidOperationException(
                $"Only Completed or Cancelled Projects can be archived. Current status: {Status}.");
        }

        var validModifiedAtUtc = RequireUtc(modifiedAtUtc, nameof(modifiedAtUtc));
        Status = ProjectStatus.Archived;
        LastModifiedBy = RequireText(modifiedBy, nameof(modifiedBy));
        LastModifiedAtUtc = validModifiedAtUtc;
    }

    private static bool IsValidTransition(ProjectStatus current, ProjectStatus target)
    {
        // PROJECT-010: allowed status transitions. All others are rejected.
        return (current, target) switch
        {
            (ProjectStatus.Planned, ProjectStatus.Active) => true,
            (ProjectStatus.Planned, ProjectStatus.Cancelled) => true,
            (ProjectStatus.Active, ProjectStatus.OnHold) => true,
            (ProjectStatus.Active, ProjectStatus.Completed) => true,
            (ProjectStatus.OnHold, ProjectStatus.Active) => true,
            (ProjectStatus.OnHold, ProjectStatus.Cancelled) => true,
            (ProjectStatus.Completed, ProjectStatus.Archived) => true,
            (ProjectStatus.Cancelled, ProjectStatus.Archived) => true,
            _ => false,
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

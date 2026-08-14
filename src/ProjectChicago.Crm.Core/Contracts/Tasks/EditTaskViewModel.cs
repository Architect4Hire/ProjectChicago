using System.ComponentModel.DataAnnotations;

namespace ProjectChicago.Crm.Contracts.Tasks;

// Request contract for editing a Task's details (title, description, start/due dates, notes).
// TASK-002: Task details may be edited after creation. TaskId identifies the Task, and the optional
// fields (Title, Description, StartDateUtc, DueDateUtc, Notes) specify what to update. Omitting a
// field or passing null means no change to that field, allowing partial updates. ConcurrencyToken
// is the base64-encoded RowVersion for optimistic locking (DATA-008). All dates must be UTC if
// provided (DATA-006).
public sealed record EditTaskViewModel
{
    [Required(ErrorMessage = "TaskId is required.")]
    public required Guid TaskId { get; init; }

    // Optional: new title for the task. Must not be null or whitespace if provided.
    // Omitting or sending null leaves the current title unchanged.
    [StringLength(255, MinimumLength = 1, ErrorMessage = "Title must be 1-255 characters.")]
    public string? Title { get; init; }

    // Optional: new description for the task. Whitespace-only string is treated as clearing
    // the description. Omitting or sending null leaves the current description unchanged.
    [StringLength(2000, ErrorMessage = "Description must be at most 2000 characters.")]
    public string? Description { get; init; }

    // Optional: new start date for the task (UTC). Omitting or sending null leaves the current
    // start date unchanged.
    public DateTime? StartDateUtc { get; init; }

    // Optional: new due date for the task (UTC). Omitting or sending null leaves the current
    // due date unchanged.
    public DateTime? DueDateUtc { get; init; }

    // Optional: new notes for the task. Whitespace-only string is treated as clearing the notes.
    // Omitting or sending null leaves the current notes unchanged.
    [StringLength(2000, ErrorMessage = "Notes must be at most 2000 characters.")]
    public string? Notes { get; init; }

    [Required(ErrorMessage = "ConcurrencyToken is required for optimistic locking.")]
    public required string ConcurrencyToken { get; init; }
}

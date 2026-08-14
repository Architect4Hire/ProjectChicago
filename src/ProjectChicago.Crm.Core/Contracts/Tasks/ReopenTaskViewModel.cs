using System.ComponentModel.DataAnnotations;

namespace ProjectChicago.Crm.Contracts.Tasks;

// Request contract for reopening a completed Task (TASK-012, API-001..007; api-contracts.md).
// TaskId identifies the Task to reopen, ReopenToStatus specifies the open status it should
// return to (must be Backlog, ToDo, InProgress, or Blocked; not Completed or Cancelled),
// and ConcurrencyToken is the base64-encoded RowVersion for optimistic locking (DATA-008).
// A request must supply all three; the Facade/Business layer never derives any of them from
// context or defaults.
public sealed record ReopenTaskViewModel
{
    [Required(ErrorMessage = "TaskId is required.")]
    public required Guid TaskId { get; init; }

    [Required(ErrorMessage = "ReopenToStatus is required.")]
    public required TaskItemStatusContract ReopenToStatus { get; init; }

    [Required(ErrorMessage = "ConcurrencyToken is required for optimistic locking.")]
    public required string ConcurrencyToken { get; init; }
}

using System.ComponentModel.DataAnnotations;

namespace ProjectChicago.Crm.Contracts.Tasks;

// Request contract for changing a Task's priority (TASK-015, API-001..007; api-contracts.md).
// TaskId identifies the Task whose priority should change, Priority is the target priority value,
// and ConcurrencyToken is the base64-encoded RowVersion for optimistic locking (DATA-008). A
// request must supply all three; the Facade/Business layer never derives any of them from
// context or defaults.
public sealed record ChangeTaskPriorityViewModel
{
    [Required(ErrorMessage = "TaskId is required.")]
    public required Guid TaskId { get; init; }

    [Required(ErrorMessage = "Priority is required.")]
    public required TaskItemPriorityContract Priority { get; init; }

    [Required(ErrorMessage = "ConcurrencyToken is required for optimistic locking.")]
    public required string ConcurrencyToken { get; init; }
}

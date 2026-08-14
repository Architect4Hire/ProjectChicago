using System.ComponentModel.DataAnnotations;

namespace ProjectChicago.Crm.Contracts.Tasks;

// Request contract for initial Task assignment (TASK-013, API-001..007; api-contracts.md).
// TaskId identifies the Task to assign, AssignedUserId is the target user, and ConcurrencyToken
// is the base64-encoded RowVersion for optimistic locking (DATA-008). A request must supply all
// three; the Facade/Business layer never derives any of them from context or defaults.
public sealed record AssignTaskViewModel
{
    [Required(ErrorMessage = "TaskId is required.")]
    public required Guid TaskId { get; init; }

    [Required(ErrorMessage = "AssignedUserId is required.")]
    [StringLength(450, MinimumLength = 1, ErrorMessage = "AssignedUserId must be between 1 and 450 characters.")]
    public required string AssignedUserId { get; init; }

    [Required(ErrorMessage = "ConcurrencyToken is required for optimistic locking.")]
    public required string ConcurrencyToken { get; init; }
}

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using ProjectChicago.Crm.Contracts.Clients;

namespace ProjectChicago.Crm.Contracts.Tasks;

// Public POST /api/projects/{projectId}/tasks request contract (TASK-001..002, API-001..007, SEC-012..013).
//
// Field bounds mirror TaskConfiguration's SQL Server column lengths so a request that will fit
// the transport contract also fits persistence, and vice versa. DataAnnotations here catch only
// shape/format problems at the transport boundary (add-endpoint.md step 2/3; backend.md Controller
// responsibility #1: "Bind/validate transport shape enough to produce a coherent request").
// Domain/state rules (e.g. status legality beyond "is it a defined contract value", completion
// date validation) stay in Business and are out of scope for this contract-only microstep.
//
// Deliberately excluded from this request: an actor/"created by" field. The acting user is
// resolved server-side from ICurrentUser/RequestContext, never accepted from an ordinary client
// request body (security.md: "Resolve the actor through ICurrentUser; never accept actor IDs from
// ordinary client requests."). AssignedUserId is a distinct business field - who the task is
// assigned to (TASK-013) - not the identity of whoever is submitting the request, so it is safe to
// accept here if present.
//
// Status is optional: TASK-010 requires the field to exist on every Task but does not mandate a
// caller-chosen initial value. When omitted, the initial status this contract expects the Business
// layer to assign is Backlog (a Task relationship normally starts as future work) - a narrow,
// reversible default recorded here rather than invented silently downstream (CLAUDE.md Usage #5).
//
// Priority is optional: When omitted, the initial priority this contract expects the Business
// layer to assign is Normal (a reasonable middle ground between Low and Critical) - the narrowest
// reversible assumption.
//
// CompletedAtUtc is not present on the creation request - it is set only when a Task transitions
// to Completed status (TASK-011, handled by Business layer).
public sealed record CreateTaskViewModel
{
    [JsonPropertyName("projectId")]
    [Required]
    public required Guid ProjectId { get; init; }

    [JsonPropertyName("title")]
    [Required(AllowEmptyStrings = false)]
    [StringLength(200, MinimumLength = 1)]
    public required string Title { get; init; }

    [JsonPropertyName("description")]
    [StringLength(2000)]
    public string? Description { get; init; }

    [JsonPropertyName("status")]
    [EnumDataType(typeof(TaskItemStatusContract))]
    public TaskItemStatusContract? Status { get; init; }

    [JsonPropertyName("priority")]
    [EnumDataType(typeof(TaskItemPriorityContract))]
    public TaskItemPriorityContract? Priority { get; init; }

    [JsonPropertyName("assignedUserId")]
    [StringLength(128)]
    public string? AssignedUserId { get; init; }

    [JsonPropertyName("startDateUtc")]
    public DateTime? StartDateUtc { get; init; }

    [JsonPropertyName("dueDateUtc")]
    public DateTime? DueDateUtc { get; init; }

    [JsonPropertyName("notes")]
    [StringLength(2000)]
    public string? Notes { get; init; }
}

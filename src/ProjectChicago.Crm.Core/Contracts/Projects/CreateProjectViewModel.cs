using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ProjectChicago.Crm.Contracts.Projects;

// Public POST /api/clients/{clientId}/projects request contract (PROJECT-001..002, API-001..007, SEC-012..013).
//
// Field bounds mirror ProjectConfiguration's SQL Server column lengths so a request that will fit
// the transport contract also fits persistence, and vice versa. DataAnnotations here catch only
// shape/format problems at the transport boundary (add-endpoint.md step 2/3; backend.md Controller
// responsibility #1: "Bind/validate transport shape enough to produce a coherent request").
// Domain/state rules (e.g. status legality beyond "is it a defined contract value", completion
// date validation, target date constraints) stay in Business and are out of scope for this
// contract-only microstep.
//
// Deliberately excluded from this request: an actor/"created by" field. The acting user is
// resolved server-side from ICurrentUser/RequestContext, never accepted from an ordinary client
// request body (security.md: "Resolve the actor through ICurrentUser; never accept actor IDs from
// ordinary client requests."). OwnerUserId is a distinct business field - the assigned project owner
// (PROJECT-002) - not the identity of whoever is submitting the request, so it is safe to accept
// here.
//
// Status is optional: PROJECT-010 requires the field to exist on every Project but does not
// mandate a caller-chosen initial value. When omitted, the initial status this contract expects
// the Business layer to assign is Planned (a Project relationship normally starts as planned work) -
// a narrow, reversible default recorded here rather than invented silently downstream (CLAUDE.md Usage #5).
//
// Priority is optional: When omitted, the initial priority this contract expects the Business
// layer to assign is Normal (a reasonable middle ground between Low and Critical) - the narrowest
// reversible assumption.
//
// ActualCompletionDateUtc is not present on the creation request - it is set only when a Project
// transitions to Completed status (PROJECT-012, handled by Business layer).
public sealed record CreateProjectViewModel
{
    [JsonPropertyName("clientId")]
    [Required]
    public required Guid ClientId { get; init; }

    [JsonPropertyName("name")]
    [Required(AllowEmptyStrings = false)]
    [StringLength(200, MinimumLength = 1)]
    public required string Name { get; init; }

    [JsonPropertyName("description")]
    [StringLength(2000)]
    public string? Description { get; init; }

    [JsonPropertyName("status")]
    [EnumDataType(typeof(ProjectStatusContract))]
    public ProjectStatusContract? Status { get; init; }

    [JsonPropertyName("priority")]
    [EnumDataType(typeof(ProjectPriorityContract))]
    public ProjectPriorityContract? Priority { get; init; }

    [JsonPropertyName("ownerUserId")]
    [Required(AllowEmptyStrings = false)]
    [StringLength(128, MinimumLength = 1)]
    public required string OwnerUserId { get; init; }

    [JsonPropertyName("startDateUtc")]
    public DateTime? StartDateUtc { get; init; }

    [JsonPropertyName("targetCompletionDateUtc")]
    public DateTime? TargetCompletionDateUtc { get; init; }

    [JsonPropertyName("notes")]
    [StringLength(2000)]
    public string? Notes { get; init; }
}

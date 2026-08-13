using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ProjectChicago.Crm.Contracts.Projects;

// Public PATCH api/projects/{projectId}/status request contract (PROJECT-010..014, API-001..007,
// SEC-012..013, DATA-008). Mirrors ChangeClientLifecycleStatusViewModel's split: DataAnnotations
// here catch only transport shape/format problems (a defined enum value, a non-empty token); the
// PROJECT-010..014 transition-graph legality check (Project.TransitionStatus) and the DATA-008
// stale-token comparison both stay in Business/Data.
//
// ExpectedConcurrencyToken is required, not optional: DATA-008 requires every mutation of a
// mutable business record to carry an optimistic-concurrency check, and this contract does not
// offer an "I didn't check" escape hatch for a status transition. Callers supply the
// ProjectServiceModel.ConcurrencyToken value from their last read of the Project.
//
// OpenTaskAcknowledgement is required when transitioning to Completed (PROJECT-013): if the
// Project contains open Tasks when completion is attempted, the caller must explicitly acknowledge
// this. The Facade/Controller is responsible for fetching the open-task count and presenting it
// to the user; this Business layer enforces the acknowledgement policy.
//
// Deliberately excluded from this request: an actor/"changed by" field, for the same reason
// CreateProjectViewModel excludes one - the acting user is resolved server-side from
// ICurrentUser/RequestContext (security.md), never accepted from an ordinary client request body.
public sealed record ChangeProjectStatusViewModel
{
    [JsonPropertyName("newStatus")]
    [Required]
    [EnumDataType(typeof(ProjectStatusContract))]
    public required ProjectStatusContract NewStatus { get; init; }

    [JsonPropertyName("expectedConcurrencyToken")]
    [Required(AllowEmptyStrings = false)]
    public required string ExpectedConcurrencyToken { get; init; }

    [JsonPropertyName("acknowledgeOpenTasks")]
    public required bool AcknowledgeOpenTasks { get; init; }
}

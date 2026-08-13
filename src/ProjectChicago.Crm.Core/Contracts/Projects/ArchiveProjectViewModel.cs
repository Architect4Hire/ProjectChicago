using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ProjectChicago.Crm.Contracts.Projects;

// Public DELETE api/projects/{projectId}/archive request contract (PROJECT-014, API-001..007,
// SEC-012..013, DATA-008). DataAnnotations here catch only transport shape/format problems (a
// non-empty token); the DATA-008 stale-token comparison stays in Business/Data.
//
// ExpectedConcurrencyToken is required, not optional: DATA-008 requires every mutation to carry an
// optimistic-concurrency check. Callers supply the ProjectServiceModel.ConcurrencyToken value from
// their last read of the Project.
//
// Deliberately excluded from this request: an actor/"archived by" field, for the same reason
// CreateProjectViewModel excludes one - the acting user is resolved server-side from
// ICurrentUser/RequestContext (security.md), never accepted from an ordinary client request body.
public sealed record ArchiveProjectViewModel
{
    [JsonPropertyName("expectedConcurrencyToken")]
    [Required(AllowEmptyStrings = false)]
    public required string ExpectedConcurrencyToken { get; init; }
}

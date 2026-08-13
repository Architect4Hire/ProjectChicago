using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ProjectChicago.Crm.Contracts.Clients;

// Public POST api/clients/{clientId}/archive request contract (CLIENT-013..015, API-001..007,
// SEC-012..013, DATA-008). DataAnnotations here catch only transport shape/format problems (a
// non-empty token); the CLIENT-015 active-Projects check (IClientBusiness.ArchiveAsync) and the
// DATA-008 stale-token comparison both stay in Business/Data.
//
// ExpectedConcurrencyToken is required, not optional: DATA-008 requires every mutation to carry an
// optimistic-concurrency check. Callers supply the ClientServiceModel.ConcurrencyToken value from
// their last read of the Client.
//
// Deliberately excluded from this request: an actor/"archived by" field, for the same reason
// CreateClientViewModel excludes one - the acting user is resolved server-side from
// ICurrentUser/RequestContext (security.md), never accepted from an ordinary client request body.
public sealed record ArchiveClientViewModel
{
    [JsonPropertyName("expectedConcurrencyToken")]
    [Required(AllowEmptyStrings = false)]
    public required string ExpectedConcurrencyToken { get; init; }
}

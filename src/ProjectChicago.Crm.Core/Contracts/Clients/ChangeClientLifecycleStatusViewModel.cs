using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ProjectChicago.Crm.Contracts.Clients;

// Public PATCH api/clients/{clientId}/lifecycle-status request contract (CLIENT-010..015,
// API-001..007, SEC-012..013, DATA-008). Mirrors CreateClientViewModel's split: DataAnnotations
// here catch only transport shape/format problems (a defined enum value, a non-empty token); the
// CLIENT-010..015 transition-graph legality check (ClientLifecycleTransitionRules) and the
// DATA-008 stale-token comparison both stay in Business/Data.
//
// ExpectedConcurrencyToken is required, not optional: DATA-008 requires every mutation of a
// mutable business record to carry an optimistic-concurrency check, and this contract does not
// offer an "I didn't check" escape hatch for a status transition. Callers supply the
// ClientServiceModel.ConcurrencyToken value from their last read of the Client.
//
// Deliberately excluded from this request: an actor/"changed by" field, for the same reason
// CreateClientViewModel excludes one - the acting user is resolved server-side from
// ICurrentUser/RequestContext (security.md), never accepted from an ordinary client request body.
public sealed record ChangeClientLifecycleStatusViewModel
{
    [JsonPropertyName("newStatus")]
    [Required]
    [EnumDataType(typeof(ClientLifecycleStatusContract))]
    public required ClientLifecycleStatusContract NewStatus { get; init; }

    [JsonPropertyName("expectedConcurrencyToken")]
    [Required(AllowEmptyStrings = false)]
    public required string ExpectedConcurrencyToken { get; init; }
}

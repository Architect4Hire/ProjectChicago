using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ProjectChicago.Crm.Contracts.Clients;

// Public POST api/clients/{clientId}/restore request contract (CLIENT-013..014, API-001..007,
// SEC-012..013, DATA-008). DataAnnotations here catch only transport shape/format problems (a
// defined enum value for RestoredStatus, a non-empty token); the archive-status check
// (IClientBusiness.RestoreAsync) and the DATA-008 stale-token comparison both stay in Business/Data.
//
// RestoredStatus is required - when restoring an archived Client, the caller must explicitly choose
// which lifecycle status to restore to (e.g. Active, Lead, Prospect) rather than defaulting. This
// forces the caller to make an explicit intent about the Client's future status after recovery.
//
// ExpectedConcurrencyToken is required, not optional: DATA-008 requires every mutation to carry an
// optimistic-concurrency check. Callers supply the ClientServiceModel.ConcurrencyToken value from
// their last read of the Client.
//
// Deliberately excluded from this request: an actor/"restored by" field, for the same reason
// CreateClientViewModel excludes one - the acting user is resolved server-side from
// ICurrentUser/RequestContext (security.md), never accepted from an ordinary client request body.
public sealed record RestoreClientViewModel
{
    [JsonPropertyName("restoredStatus")]
    [Required]
    [EnumDataType(typeof(ClientLifecycleStatusContract))]
    public required ClientLifecycleStatusContract RestoredStatus { get; init; }

    [JsonPropertyName("expectedConcurrencyToken")]
    [Required(AllowEmptyStrings = false)]
    public required string ExpectedConcurrencyToken { get; init; }
}

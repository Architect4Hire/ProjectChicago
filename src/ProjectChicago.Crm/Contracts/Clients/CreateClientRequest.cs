using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ProjectChicago.Crm.Contracts.Clients;

// Public POST /api/clients request contract (CLIENT-001..004, API-001..007, SEC-012..013).
//
// Field bounds mirror ClientConfiguration's SQL Server column lengths so a request that will fit
// the transport contract also fits persistence, and vice versa. DataAnnotations here catch only
// shape/format problems at the transport boundary (add-endpoint.md step 2/3; backend.md Controller
// responsibility #1: "Bind/validate transport shape enough to produce a coherent request").
// Domain/state rules (e.g. lifecycle-status legality beyond "is it a defined contract value",
// duplicate handling, default-owner assignment) stay in Business and are out of scope for this
// contract-only microstep.
//
// Deliberately excluded from this request: an actor/"created by" field. The acting user is
// resolved server-side from ICurrentUser/RequestContext, never accepted from an ordinary client
// request body (security.md: "Resolve the actor through ICurrentUser; never accept actor IDs from
// ordinary client requests."). OwnerUserId is a distinct business field - the assigned owner
// (CLIENT-002) - not the identity of whoever is submitting the request, so it is safe to accept
// here.
//
// LifecycleStatus is optional: CLIENT-002 requires the field to exist on every Client but does not
// mandate a caller-chosen initial value, and CLIENT-010 does not name a default. When omitted, the
// initial status this contract expects the Business layer to assign is Lead (a Client relationship
// normally starts as a lead) - a narrow, reversible default recorded here rather than invented
// silently downstream (CLAUDE.md Usage #5, requirements doc Sec. 48.8).
public sealed record CreateClientRequest
{
    [JsonPropertyName("name")]
    [Required(AllowEmptyStrings = false)]
    [StringLength(200, MinimumLength = 1)]
    public required string Name { get; init; }

    [JsonPropertyName("primaryContactName")]
    [StringLength(200)]
    public string? PrimaryContactName { get; init; }

    [JsonPropertyName("primaryEmail")]
    [EmailAddress]
    [StringLength(320)]
    public string? PrimaryEmail { get; init; }

    [JsonPropertyName("primaryPhone")]
    [StringLength(32)]
    public string? PrimaryPhone { get; init; }

    [JsonPropertyName("website")]
    [Url]
    [StringLength(2048)]
    public string? Website { get; init; }

    [JsonPropertyName("addressLine")]
    [StringLength(300)]
    public string? AddressLine { get; init; }

    [JsonPropertyName("city")]
    [StringLength(150)]
    public string? City { get; init; }

    [JsonPropertyName("stateOrProvince")]
    [StringLength(150)]
    public string? StateOrProvince { get; init; }

    [JsonPropertyName("postalCode")]
    [StringLength(20)]
    public string? PostalCode { get; init; }

    [JsonPropertyName("country")]
    [StringLength(100)]
    public string? Country { get; init; }

    [JsonPropertyName("lifecycleStatus")]
    public ClientLifecycleStatusContract? LifecycleStatus { get; init; }

    [JsonPropertyName("description")]
    [StringLength(2000)]
    public string? Description { get; init; }

    // Assigned owner (CLIENT-002) - a business field, not the requesting actor. See type-level
    // remarks above.
    [JsonPropertyName("ownerUserId")]
    [Required(AllowEmptyStrings = false)]
    [StringLength(128, MinimumLength = 1)]
    public required string OwnerUserId { get; init; }
}

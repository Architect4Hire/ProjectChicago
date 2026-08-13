using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ProjectChicago.Crm.Contracts.Clients;

// Public PATCH /api/clients/{clientId} request contract (CLIENT-002, API-001..007, SEC-012..013, DATA-008).
// Mirrors CreateClientViewModel's split: DataAnnotations here catch only transport shape/format problems
// (valid string lengths, valid email/URL); the domain business rules (field normalization) and the
// DATA-008 stale-token comparison both stay in Business/Data.
//
// ExpectedConcurrencyToken is required: DATA-008 requires every mutation of a mutable business record
// to carry an optimistic-concurrency check. Callers supply the ClientServiceModel.ConcurrencyToken value
// from their last read of the Client.
//
// Deliberately excluded from this request: an actor/"modified by" field. The acting user is resolved
// server-side from ICurrentUser/RequestContext (security.md), never accepted from an ordinary client
// request body.
//
// All fields are optional except ExpectedConcurrencyToken: CLIENT-002 allows omitted contact/address
// fields on creation, so update should also allow callers to leave fields untouched by omitting them
// entirely. A caller wishing to clear a field (e.g. remove Website) must pass explicit null.
public sealed record UpdateClientViewModel
{
    [JsonPropertyName("name")]
    [StringLength(200, MinimumLength = 1)]
    public string? Name { get; init; }

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

    [JsonPropertyName("description")]
    [StringLength(2000)]
    public string? Description { get; init; }

    [JsonPropertyName("ownerUserId")]
    [StringLength(128, MinimumLength = 1)]
    public string? OwnerUserId { get; init; }

    [JsonPropertyName("expectedConcurrencyToken")]
    [Required(AllowEmptyStrings = false)]
    public required string ExpectedConcurrencyToken { get; init; }
}

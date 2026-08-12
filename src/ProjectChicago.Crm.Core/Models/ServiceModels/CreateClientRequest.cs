using System.ComponentModel.DataAnnotations;
using ProjectChicago.Crm.Core.Models.DataModels.Entities;

namespace ProjectChicago.Crm.Core.Models.ServiceModels;

// Facade-layer input for Client creation (CLIENT-001..004, SEC-010..013; onion-boundaries.md
// "Facades own contextual validation"). Deliberately not the API host's
// ProjectChicago.Crm.Contracts.Clients.CreateClientRequest - that transport contract lives with the
// HTTP host, which .Core cannot reference without creating a reference cycle (CLAUDE.md reference
// direction; mirrors the same separation already established for ClientDuplicateMatchField). Field
// bounds mirror the wire contract 1:1 so a request that is valid at the transport boundary is also
// valid here, and vice versa.
//
// Deliberately excludes Actor, RequestContext, and CreatedAtUtc - ClientFacade resolves those
// itself (ICurrentRequestContext/IClock) and supplies them when it builds the CreateClientCommand
// it passes to Business, so a caller can never supply its own actor/timestamp (security.md:
// "Resolve the actor through ICurrentUser; never accept actor IDs from ordinary client requests.").
public sealed record CreateClientRequest
{
    [Required(AllowEmptyStrings = false)]
    [StringLength(200, MinimumLength = 1)]
    public required string Name { get; init; }

    [StringLength(200)]
    public string? PrimaryContactName { get; init; }

    [EmailAddress]
    [StringLength(320)]
    public string? PrimaryEmail { get; init; }

    [StringLength(32)]
    public string? PrimaryPhone { get; init; }

    [Url]
    [StringLength(2048)]
    public string? Website { get; init; }

    [StringLength(300)]
    public string? AddressLine { get; init; }

    [StringLength(150)]
    public string? City { get; init; }

    [StringLength(150)]
    public string? StateOrProvince { get; init; }

    [StringLength(20)]
    public string? PostalCode { get; init; }

    [StringLength(100)]
    public string? Country { get; init; }

    // Validated as a defined ClientLifecycleStatus member here (Facade validation) rather than
    // leaving an out-of-range value to surface as Business's unmapped ArgumentException.
    [EnumDataType(typeof(ClientLifecycleStatus))]
    public ClientLifecycleStatus? LifecycleStatus { get; init; }

    [StringLength(2000)]
    public string? Description { get; init; }

    [Required(AllowEmptyStrings = false)]
    [StringLength(128, MinimumLength = 1)]
    public required string OwnerUserId { get; init; }
}

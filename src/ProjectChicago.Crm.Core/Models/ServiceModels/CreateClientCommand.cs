using ProjectChicago.Crm.Core.Models.DataModels.Entities;
using ProjectChicago.Shared.Correlation;

namespace ProjectChicago.Crm.Core.Models.ServiceModels;

// Business-layer input for Client creation (CLIENT-001..004; backend.md Business responsibility:
// "translates between service models and data-layer operations"). Deliberately not the API host's
// CreateClientRequest - that transport contract lives in ProjectChicago.Crm.Contracts, which .Core
// cannot reference without creating a reference cycle (CLAUDE.md reference direction). Actor,
// RequestContext, and CreatedAtUtc are supplied already-resolved by the caller (the future Facade,
// via ICurrentRequestContext/a clock) rather than pulled by Business itself, so Business stays a
// pure translation/decision layer with no HttpContext/clock/DI dependency of its own.
//
// LifecycleStatus is optional: CLIENT-010 does not name a default, so Business assigns Lead when
// omitted (CLAUDE.md Usage #5 - narrow, reversible default; mirrors the same choice already
// recorded on CreateClientRequest).
public sealed record CreateClientCommand
{
    public required string Name { get; init; }

    public string? PrimaryContactName { get; init; }

    public string? PrimaryEmail { get; init; }

    public string? PrimaryPhone { get; init; }

    public string? Website { get; init; }

    public string? AddressLine { get; init; }

    public string? City { get; init; }

    public string? StateOrProvince { get; init; }

    public string? PostalCode { get; init; }

    public string? Country { get; init; }

    public ClientLifecycleStatus? LifecycleStatus { get; init; }

    public string? Description { get; init; }

    public required string OwnerUserId { get; init; }

    public required ActorContext Actor { get; init; }

    public required RequestContext RequestContext { get; init; }

    public required DateTime CreatedAtUtc { get; init; }
}

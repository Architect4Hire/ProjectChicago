using ProjectChicago.Crm.Core.Models.DataModels.Entities;

namespace ProjectChicago.Crm.Core.Repositories;

// Repository-owned input for IClientRepository.ListAsync (CLIENT-020..024, PERF-001..004). This is
// not the public ListClientsRequest wire contract - it carries already-resolved, already-bounded
// values (Page/PageSize defaults applied, SortBy/SortDirection defaulted, LifecycleStatus already
// translated from ClientLifecycleStatusContract) because deciding those defaults/translations is a
// Business-layer concern (onion-boundaries.md), not this repository's.
public sealed record ClientListFilter
{
    // CLIENT-021 free-text search, matched against Name, PrimaryContactName, PrimaryEmail, and
    // PrimaryPhone. Null/whitespace means "no search filter applied."
    public string? Search { get; init; }

    // CLIENT-022 lifecycle-status filter. Null means "no lifecycle-status filter applied" - it does
    // not by itself exclude Archived Clients; the repository's own CLIENT-013 default handles that.
    public ClientLifecycleStatus? LifecycleStatus { get; init; }

    // CLIENT-022 assigned-owner filter, matched against Client.OwnerUserId. Null/whitespace means
    // "no owner filter applied."
    public string? OwnerUserId { get; init; }

    // CLIENT-022 active/inactive-state filter. A Client is "active" while its LifecycleStatus is
    // anything other than Archived, and "inactive" once Archived - this mirrors CLIENT-013's framing
    // of Archived as the one status normal Client lists exclude by default, so IsActive/LifecycleStatus
    // never disagree about what "archived" means. True requests only non-Archived Clients, false
    // requests only Archived Clients, and null applies no additional state filter.
    public bool? IsActive { get; init; }

    public required ClientListSortField SortBy { get; init; }

    public required ClientListSortDirection SortDirection { get; init; }

    // 1-based page number. Bounds enforcement (CLIENT-024 "unbounded result sets shall not be
    // permitted") happens above this repository - ListClientsRequest's [Range] plus
    // ClientsApiContract.MaxPageSize - so this seam trusts the value it is given.
    public required int Page { get; init; }

    public required int PageSize { get; init; }
}

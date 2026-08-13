using ProjectChicago.Crm.Core.Models.DataModels.Entities;

namespace ProjectChicago.Crm.Core.Repositories;

// Repository-owned input for IProjectRepository.ListAsync (PROJECT-020..023, PERF-001..004). This
// is not the public ListProjectsRequest wire contract - it carries already-resolved, already-bounded
// values (Page/PageSize defaults applied, SortBy/SortDirection defaulted, Status already translated
// from ProjectStatusContract) because deciding those defaults/translations is a Business-layer
// concern (onion-boundaries.md), not this repository's.
public sealed record ProjectListFilter
{
    // PROJECT-022 free-text search, matched against Project Name, Client Name, and Description.
    // Null/whitespace means "no search filter applied."
    public string? Search { get; init; }

    // PROJECT-021 Client filter, matched against Project.ClientId. Guid.Empty means "no client
    // filter applied" (search across all authorized Clients).
    public Guid ClientId { get; init; }

    // PROJECT-021 status filter. Null means "no status filter applied."
    public ProjectStatus? Status { get; init; }

    // PROJECT-021 assigned-owner filter, matched against Project.OwnerUserId. Null/whitespace means
    // "no owner filter applied."
    public string? OwnerUserId { get; init; }

    // PROJECT-021 priority filter. Null means "no priority filter applied."
    public ProjectPriority? Priority { get; init; }

    // PROJECT-021 start date filter (start date range or exact date). Null means "no start date
    // filter applied."
    public DateTime? StartDateUtc { get; init; }

    // PROJECT-021 target completion date filter (target date range or exact date). Null means "no
    // target completion date filter applied."
    public DateTime? TargetCompletionDateUtc { get; init; }

    // PROJECT-014/DATA-020: Include archived Projects in list results. Default false excludes
    // archived Projects (normal active lists). Nil/false is the default - archived Projects only
    // appear in normal queries when explicitly requested by administrative/historical views.
    public bool IncludeArchived { get; init; } = false;

    public required ProjectListSortField SortBy { get; init; }

    public required ProjectListSortDirection SortDirection { get; init; }

    // 1-based page number. Bounds enforcement (PROJECT-023 "unbounded result sets shall not be
    // permitted") happens above this repository - so this seam trusts the value it is given.
    public required int Page { get; init; }

    public required int PageSize { get; init; }
}

using System.ComponentModel.DataAnnotations;

namespace ProjectChicago.Crm.Contracts.Projects;

// Public GET api/projects query contract (PROJECT-020..023, API-005). Bound from the query string
// ([FromQuery] on the future controller action - add-endpoint.md step 3, out of scope for this
// contract-only microstep), so property names are matched case-insensitively against query keys
// rather than driven by JsonPropertyName/System.Text.Json.
//
// PROJECT-022 search fields (name/description/client name) are exposed as a single free-text Search
// term rather than one parameter per field - the requirement describes what a search matches
// against, not three independently combinable filters, and a single term keeps the common case
// (type a name, get matches) simple.
//
// Page/PageSize default and bound against ProjectsApiContract's constants so "no page requested"
// and "an out-of-range page size requested" behave identically for every future caller
// (PROJECT-023 - "unbounded result sets shall not be permitted"; API-005).
public sealed record ListProjectsRequest
{
    [StringLength(200)]
    public string? Search { get; init; }

    // PROJECT-020/021 Client filter. Guid.Empty (or omitted) means search across all Clients.
    public Guid? ClientId { get; init; }

    // PROJECT-021 status filter. A single value - narrowing to exactly this status is the common
    // case.
    [EnumDataType(typeof(ProjectStatusContract))]
    public ProjectStatusContract? Status { get; init; }

    // PROJECT-021 assigned-owner filter.
    [StringLength(128)]
    public string? OwnerUserId { get; init; }

    // PROJECT-021 priority filter.
    [EnumDataType(typeof(ProjectPriorityContract))]
    public ProjectPriorityContract? Priority { get; init; }

    // PROJECT-021 start date filter (if provided, return Projects with StartDateUtc >= this date).
    public DateTime? StartDateUtc { get; init; }

    // PROJECT-021 target completion date filter (if provided, return Projects with TargetCompletionDateUtc <= this date).
    public DateTime? TargetCompletionDateUtc { get; init; }

    // PROJECT-023 sort attribute/direction. Both optional - the default sort applied when omitted is
    // a Business-layer decision.
    [EnumDataType(typeof(ProjectSortField))]
    public ProjectSortField? SortBy { get; init; }

    [EnumDataType(typeof(ProjectSortDirection))]
    public ProjectSortDirection? SortDirection { get; init; }

    // PROJECT-023/API-005 bounded server-side pagination. 1-based page number; omitted query value
    // resolves to ProjectsApiContract.DefaultPage via this property's initializer.
    [Range(1, int.MaxValue)]
    public int Page { get; init; } = ProjectsApiContract.DefaultPage;

    // Bounded by ProjectsApiContract.MaxPageSize so a caller cannot request an effectively unbounded
    // result set (PROJECT-023). Omitted query value resolves to ProjectsApiContract.DefaultPageSize.
    [Range(1, ProjectsApiContract.MaxPageSize)]
    public int PageSize { get; init; } = ProjectsApiContract.DefaultPageSize;
}

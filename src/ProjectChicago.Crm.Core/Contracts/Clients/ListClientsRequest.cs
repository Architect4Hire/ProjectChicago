using System.ComponentModel.DataAnnotations;

namespace ProjectChicago.Crm.Contracts.Clients;

// Public GET api/clients query contract (CLIENT-020..024, API-005). Bound from the query string
// ([FromQuery] on the future controller action - add-endpoint.md step 3, out of scope for this
// contract-only microstep), so property names are matched case-insensitively against query keys
// rather than driven by JsonPropertyName/System.Text.Json.
//
// DataAnnotations here catch only shape/format problems at the transport boundary (add-endpoint.md
// step 2/3), mirroring CreateClientViewModel's split: whether a supplied LifecycleStatus/OwnerUserId
// actually exists or is visible to the caller is a Business/Facade concern, not this contract's.
//
// CLIENT-021 search fields (name/contact/email/phone) are exposed as a single free-text Search
// term rather than one parameter per field - the requirement describes what a search matches
// against, not four independently combinable filters, and a single term keeps the common case
// (type a name or phone number, get matches) simple. Splitting into per-field search parameters is
// a reversible future addition if a requirement calls for it (CLAUDE.md Usage #5).
//
// Page/PageSize default and bound against ClientsApiContract's constants so "no page requested"
// and "an out-of-range page size requested" behave identically for every future caller
// (CLIENT-024 - "unbounded result sets shall not be permitted"; API-005).
public sealed record ListClientsRequest
{
    [StringLength(200)]
    public string? Search { get; init; }

    // CLIENT-022 lifecycle-status filter. A single value, not a set - CLIENT-022 does not ask for
    // multi-select filtering; narrowing to "clients currently in exactly this stage" is the
    // common case and a set-valued filter is a reversible future addition if required.
    [EnumDataType(typeof(ClientLifecycleStatusContract))]
    public ClientLifecycleStatusContract? LifecycleStatus { get; init; }

    // CLIENT-022 assigned-owner filter.
    [StringLength(128)]
    public string? OwnerUserId { get; init; }

    // CLIENT-022 active/inactive-state filter. Kept distinct from LifecycleStatus: CLIENT-022 lists
    // "Lifecycle status" and "Active/inactive state" as two separate filterable attributes, so this
    // contract does not assume active/inactive is merely a derived view over specific lifecycle
    // values. How IsActive maps onto stored Client state is a Business/Data concern.
    public bool? IsActive { get; init; }

    // CLIENT-023 sort attribute/direction. Both optional - the default sort applied when omitted is
    // a Business-layer decision (mirrors CreateClientViewModel.LifecycleStatus's optional-with-
    // downstream-default pattern), not baked into this transport contract.
    [EnumDataType(typeof(ClientSortField))]
    public ClientSortField? SortBy { get; init; }

    [EnumDataType(typeof(ClientSortDirection))]
    public ClientSortDirection? SortDirection { get; init; }

    // CLIENT-024/API-005 bounded server-side pagination. 1-based page number; omitted query value
    // resolves to ClientsApiContract.DefaultPage via this property's initializer.
    [Range(1, int.MaxValue)]
    public int Page { get; init; } = ClientsApiContract.DefaultPage;

    // Bounded by ClientsApiContract.MaxPageSize so a caller cannot request an effectively unbounded
    // result set (CLIENT-024). Omitted query value resolves to ClientsApiContract.DefaultPageSize.
    [Range(1, ClientsApiContract.MaxPageSize)]
    public int PageSize { get; init; } = ClientsApiContract.DefaultPageSize;
}

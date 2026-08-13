using ProjectChicago.Crm.Contracts.Clients;
using ProjectChicago.Crm.Contracts.Common;
using ProjectChicago.Shared.Correlation;

namespace ProjectChicago.Crm.Core.Business;

// Business-layer Client creation seam (CLIENT-001..004, AUDIT-001..003; backend.md, onion-
// boundaries.md). Accepts the wire CreateClientViewModel directly and returns the wire
// ClientServiceModel directly - Business owns the entire ViewModel<->domain<->ServiceModel
// translation (ClientContractMappingExtensions), so Facade only resolves/supplies the
// actor/context/timestamp a caller must never set itself and ClientsController does no mapping at
// all.
public interface IClientBusiness
{
    // Normalizes business values, assigns identity and the initial lifecycle status, decides
    // CLIENT-004 duplicate warnings, builds the AUDIT-001..003 audit fact, persists both the Client
    // and the audit fact through the single IClientData seam, and maps the result into a
    // ClientServiceModel. Duplicate warnings never block creation (CLIENT-004: "warn ... rather than
    // silently merge").
    Task<ClientServiceModel> CreateAsync(
        CreateClientViewModel request,
        ActorContext actor,
        RequestContext requestContext,
        DateTime createdAtUtc,
        CancellationToken cancellationToken);

    // Translates the wire ListClientsRequest into repository-facing filter values (resolving
    // SortBy/SortDirection defaults and the LifecycleStatus wire<->domain translation - CLIENT-
    // 020..024), retrieves one page through IClientData, and maps the result into the wire
    // PagedResponse<ClientServiceModel>. Page/PageSize on the returned envelope always mirror what
    // the caller requested, not what IClientData happened to return.
    Task<PagedResponse<ClientServiceModel>> ListAsync(
        ListClientsRequest request,
        CancellationToken cancellationToken);

    // Loads the current Client, verifies DATA-008 optimistic concurrency against
    // expectedConcurrencyToken, applies the CLIENT-010..015 transition rules
    // (ClientLifecycleTransitionRules), mutates the Client, builds the AUDIT-001..003
    // StatusChanged audit fact, and persists both atomically through IClientData. Returns null
    // when no Client with the requested Id exists. Throws InvalidOperationException when the
    // requested transition is not allowed, and ClientConcurrencyConflictException when
    // expectedConcurrencyToken does not match the Client's current state.
    Task<ClientServiceModel?> ChangeLifecycleStatusAsync(
        Guid clientId,
        ClientLifecycleStatusContract newStatus,
        string expectedConcurrencyToken,
        ActorContext actor,
        RequestContext requestContext,
        DateTime changedAtUtc,
        CancellationToken cancellationToken);

    // Retrieves the consolidated Client detail view (CLIENT-030..032) through IClientData, then
    // maps the result into a ClientDetailServiceModel (ClientContractMappingExtensions). Returns
    // null when no Client with the requested Id exists - Business does not decide 404 semantics,
    // that is a future Controller's concern.
    Task<ClientDetailServiceModel?> GetDetailAsync(Guid clientId, CancellationToken cancellationToken);

    // Archives a Client by transitioning its lifecycle status to Archived (CLIENT-013/014).
    // Enforces CLIENT-015: a Client with active Projects cannot be archived. Loads the current
    // Client, verifies DATA-008 optimistic concurrency against expectedConcurrencyToken, checks
    // that no active Projects exist, mutates the Client to Archived status, builds the AUDIT-001..003
    // Archived audit fact, and persists both atomically through IClientData. Returns null when no
    // Client with the requested Id exists. Throws InvalidOperationException when the Client has
    // active Projects, and ClientConcurrencyConflictException when expectedConcurrencyToken does
    // not match the Client's current state.
    Task<ClientServiceModel?> ArchiveAsync(
        Guid clientId,
        string expectedConcurrencyToken,
        ActorContext actor,
        RequestContext requestContext,
        DateTime archivedAtUtc,
        CancellationToken cancellationToken);

    // Restores an archived Client to a specified lifecycle status (CLIENT-013/014). Loads the
    // current Client, verifies DATA-008 optimistic concurrency against expectedConcurrencyToken,
    // verifies the Client is currently Archived, mutates the Client to the new status, builds the
    // AUDIT-001..003 Restored audit fact, and persists both atomically through IClientData. Returns
    // null when no Client with the requested Id exists. Throws InvalidOperationException when the
    // Client is not currently Archived or when the target status is not a valid lifecycle status for
    // restoration, and ClientConcurrencyConflictException when expectedConcurrencyToken does not
    // match the Client's current state.
    Task<ClientServiceModel?> RestoreAsync(
        Guid clientId,
        ClientLifecycleStatusContract restoredStatus,
        string expectedConcurrencyToken,
        ActorContext actor,
        RequestContext requestContext,
        DateTime restoredAtUtc,
        CancellationToken cancellationToken);

    // Updates a Client's editable profile fields (CLIENT-002): name, contact info, address,
    // website, description, and owner. Does not change lifecycle status (ChangeLifecycleStatusAsync is
    // separate) or archive state. Normalizes all string inputs (trim), loads the current Client,
    // verifies DATA-008 optimistic concurrency against expectedConcurrencyToken, mutates only the
    // provided (non-null) fields, builds the AUDIT-001..003 Updated audit fact with before/after
    // change-set, and persists both atomically through IClientData. Returns null when no Client with
    // the requested Id exists. Throws ClientConcurrencyConflictException when expectedConcurrencyToken
    // does not match the Client's current state.
    Task<ClientServiceModel?> UpdateAsync(
        Guid clientId,
        UpdateClientViewModel request,
        string expectedConcurrencyToken,
        ActorContext actor,
        RequestContext requestContext,
        DateTime modifiedAtUtc,
        CancellationToken cancellationToken);
}

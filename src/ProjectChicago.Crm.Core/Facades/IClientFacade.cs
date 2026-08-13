using ProjectChicago.Crm.Contracts.Clients;
using ProjectChicago.Crm.Contracts.Common;

namespace ProjectChicago.Crm.Core.Facades;

// Public application/use-case seam for Client creation (CLIENT-001..004, SEC-010..013;
// onion-boundaries.md: "Facades are the only application entry point callable by controllers").
// Accepts CreateClientViewModel and returns ClientServiceModel directly (rather than a separate
// Facade-only request/result shape) so ClientsController stays transport-only: it binds the
// request, calls this one method, and returns the ClientServiceModel straight into a 201 - no
// field-by-field mapping of its own. This Facade does not map either type itself - it resolves
// actor/context/timestamp and delegates the ViewModel<->domain<->ServiceModel translation entirely
// to IClientBusiness.CreateAsync (ClientContractMappingExtensions, in Business).
public interface IClientFacade
{
    // Verifies SEC-012/013 authorization, runs CLIENT-002 contextual validation on request, and
    // delegates to Business for CLIENT-004 duplicate-warning evaluation, model translation, and
    // persistence. Throws UnauthorizedAccessException when the resolved actor lacks the
    // Clients.Write policy, or System.ComponentModel.DataAnnotations.ValidationException when
    // request fails validation - both already classified by ApiExceptionHandler into the 403/400
    // ProblemDetails shape (ERROR-003).
    Task<ClientServiceModel> CreateAsync(CreateClientViewModel request, CancellationToken cancellationToken);

    // Verifies SEC-012/013 authorization (Clients.Read) for the resolved actor, runs CLIENT-
    // 020..024 contextual validation on request (bounded page/page size, only-defined
    // sort/filter/lifecycle-status values), and delegates to Business for filter translation,
    // retrieval, and mapping. Throws UnauthorizedAccessException when the resolved actor lacks the
    // Clients.Read policy, or System.ComponentModel.DataAnnotations.ValidationException when
    // request fails validation - both already classified by ApiExceptionHandler into the 403/400
    // ProblemDetails shape (ERROR-003).
    Task<PagedResponse<ClientServiceModel>> ListAsync(ListClientsRequest request, CancellationToken cancellationToken);

    // Verifies SEC-012/013 authorization (Clients.Read) for the resolved actor, validates that
    // clientId is not Guid.Empty, and delegates to Business for retrieval and mapping
    // (CLIENT-030..032). Returns null when no Client with the requested Id exists - this Facade
    // does not decide 404 semantics; that mapping belongs to a future Controller. Throws
    // UnauthorizedAccessException when the resolved actor lacks the Clients.Read policy, or
    // System.ComponentModel.DataAnnotations.ValidationException when clientId is empty - both
    // already classified by ApiExceptionHandler into the 403/400 ProblemDetails shape (ERROR-003).
    Task<ClientDetailServiceModel?> GetDetailAsync(Guid clientId, CancellationToken cancellationToken);

    // Verifies SEC-012/013 authorization (Clients.Write - a lifecycle transition is a mutation) for
    // the resolved actor, runs transport-shape validation on request, and delegates to Business for
    // the CLIENT-010..015 transition-rule check, the DATA-008 concurrency check, persistence, and
    // mapping. Returns null when no Client with the requested Id exists - this Facade does not
    // decide 404 semantics; that mapping belongs to the Controller, mirroring GetDetailAsync. Throws
    // UnauthorizedAccessException when the resolved actor lacks the Clients.Write policy,
    // System.ComponentModel.DataAnnotations.ValidationException when clientId is empty or request
    // fails transport validation, InvalidOperationException when Business rejects the requested
    // transition (CLIENT-010..015), or ClientConcurrencyConflictException when
    // request.ExpectedConcurrencyToken does not match the Client's current state (DATA-008) - all
    // classified by the Controller into the 400/403/404/409 ProblemDetails shape (ERROR-003).
    Task<ClientServiceModel?> ChangeLifecycleStatusAsync(
        Guid clientId, ChangeClientLifecycleStatusViewModel request, CancellationToken cancellationToken);

    // Verifies SEC-012/013 authorization (Clients.Write) for the resolved actor, validates that
    // clientId is not Guid.Empty and that request has valid ExpectedConcurrencyToken, and delegates
    // to Business for the CLIENT-015 check (blocks if active Projects exist), the DATA-008 concurrency
    // check, persistence, and mapping. Returns null when no Client with the requested Id exists - this
    // Facade does not decide 404 semantics; that mapping belongs to the Controller. Throws
    // UnauthorizedAccessException when the resolved actor lacks the Clients.Write policy,
    // System.ComponentModel.DataAnnotations.ValidationException when clientId is empty or request
    // fails transport validation, InvalidOperationException when Business detects active Projects
    // (CLIENT-015), or ClientConcurrencyConflictException when request.ExpectedConcurrencyToken does
    // not match the Client's current state (DATA-008) - all classified by the Controller into the
    // 400/403/404/409 ProblemDetails shape (ERROR-003).
    Task<ClientServiceModel?> ArchiveAsync(
        Guid clientId, ArchiveClientViewModel request, CancellationToken cancellationToken);

    // Verifies SEC-012/013 authorization (Clients.Write) for the resolved actor, validates that
    // clientId is not Guid.Empty and that request has valid fields, and delegates to Business for
    // the archive-status check, the DATA-008 concurrency check, persistence, and mapping. Returns
    // null when no Client with the requested Id exists - this Facade does not decide 404 semantics;
    // that mapping belongs to the Controller. Throws UnauthorizedAccessException when the resolved
    // actor lacks the Clients.Write policy, System.ComponentModel.DataAnnotations.ValidationException
    // when clientId is empty, RestoredStatus is undefined, or request fails transport validation,
    // InvalidOperationException when Business detects the Client is not currently Archived (CLIENT-014),
    // or ClientConcurrencyConflictException when request.ExpectedConcurrencyToken does not match the
    // Client's current state (DATA-008) - all classified by the Controller into the 400/403/404/409
    // ProblemDetails shape (ERROR-003).
    Task<ClientServiceModel?> RestoreAsync(
        Guid clientId, RestoreClientViewModel request, CancellationToken cancellationToken);

    // Verifies SEC-012/013 authorization (Clients.Write) for the resolved actor, validates that
    // clientId is not Guid.Empty and that request has valid ExpectedConcurrencyToken, and delegates
    // to Business for normalization, the DATA-008 concurrency check, persistence, and mapping.
    // Returns null when no Client with the requested Id exists - this Facade does not decide 404
    // semantics; that mapping belongs to the Controller. Throws UnauthorizedAccessException when the
    // resolved actor lacks the Clients.Write policy, System.ComponentModel.DataAnnotations.
    // ValidationException when clientId is empty or request fails transport validation, or
    // ClientConcurrencyConflictException when request.ExpectedConcurrencyToken does not match the
    // Client's current state (DATA-008) - all classified by the Controller into the 400/403/404/409
    // ProblemDetails shape (ERROR-003).
    Task<ClientServiceModel?> UpdateAsync(
        Guid clientId, UpdateClientViewModel request, CancellationToken cancellationToken);
}

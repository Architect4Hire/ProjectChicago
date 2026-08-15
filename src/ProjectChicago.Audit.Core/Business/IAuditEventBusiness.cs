using ProjectChicago.Contracts.Audit;
using ProjectChicago.Shared.Inbox;

namespace ProjectChicago.Audit.Core.Business;

/// <summary>
/// Business layer for translating, validating, and processing incoming audit events (EntityMutationAudited)
/// consumed from Service Bus (ADR-0016, AUDIT-001..008, PRIV-001..005, ASYNC-005..008).
///
/// Responsibilities:
/// - Validate event contract version and structure
/// - Redact sensitive fields (passwords, tokens, secrets) per PRIV-002, AUDIT-008
/// - Map actor type/ID, determine ActionCategory, translate before/after values
/// - Delegate to Data layer for atomic persistence with inbox idempotency
/// - Return typed results for Function trigger to interpret (retry/dead-letter policy)
///
/// Does NOT:
/// - Read CrmDb, IdentityDb, or other cross-service data (PRIV-002: minimize sensitive duplication)
/// - Expose query/retrieval endpoints
/// - Implement retry policy (Function trigger handles that)
/// </summary>
public interface IAuditEventBusiness
{
    /// <summary>
    /// Process a validated EntityMutationAudited event for persistence.
    ///
    /// Flow:
    /// 1. Validate event version (currently only Version=1 supported).
    /// 2. Validate structure: all required fields present and non-empty.
    /// 3. Check idempotency: if inboxMessage indicates already-completed, return DuplicateAlreadyProcessed.
    /// 4. Redact sensitive fields from PreviousValues/NewValues (PRIV-002, AUDIT-008).
    /// 5. Map actor type, determine ActionCategory, build summary.
    /// 6. Create AuditEntry model with all metadata.
    /// 7. Delegate to IAuditData.AppendAuditEntryIdempotentlyAsync for atomic persistence.
    /// 8. Return Success or TransientFailure (Data layer throws will be caught here).
    ///
    /// Parameters:
    /// - auditEvent: The deserialized, envelope-validated audit event contract.
    /// - inboxMessage: The Service Bus inbox tracking row for idempotency (already loaded by caller).
    /// - cancellationToken: Cancellation token.
    ///
    /// Returns:
    /// - Success: Event was persisted (or duplicate already processed successfully).
    /// - ValidationFailure: Unsupported version, malformed, or structurally invalid (dead-letter).
    /// - TransientFailure: Database or infrastructure error (allow retry via Function trigger).
    ///
    /// (AUDIT-001..008, PRIV-001..005, ASYNC-005..008, messaging.md failure semantics)
    /// </summary>
    Task<AuditEventProcessingResult> ProcessAuditEventAsync(
        EntityMutationAudited auditEvent,
        InboxMessage inboxMessage,
        CancellationToken cancellationToken);
}

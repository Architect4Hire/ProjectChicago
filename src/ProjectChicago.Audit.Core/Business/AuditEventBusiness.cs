using ProjectChicago.Audit.Core.Data;
using ProjectChicago.Audit.Core.Persistence;
using ProjectChicago.Contracts.Audit;
using ProjectChicago.Shared.Inbox;
using System.Text.Json;

namespace ProjectChicago.Audit.Core.Business;

/// <summary>
/// Business layer implementation for translating, validating, and processing incoming audit events
/// (ADR-0016, AUDIT-001..008, PRIV-001..005, ASYNC-005..008).
/// </summary>
public class AuditEventBusiness : IAuditEventBusiness
{
    private readonly IAuditData _auditData;

    public AuditEventBusiness(IAuditData auditData)
    {
        _auditData = auditData ?? throw new ArgumentNullException(nameof(auditData));
    }

    /// <summary>
    /// Process a validated EntityMutationAudited event for persistence.
    /// See IAuditEventBusiness.ProcessAuditEventAsync for detailed contract.
    /// </summary>
    public async Task<AuditEventProcessingResult> ProcessAuditEventAsync(
        EntityMutationAudited auditEvent,
        InboxMessage inboxMessage,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);
        ArgumentNullException.ThrowIfNull(inboxMessage);

        try
        {
            // Step 1: Validate contract version (currently only Version=1 supported).
            if (auditEvent.Version != EntityMutationAudited.CurrentVersion)
            {
                return new AuditEventProcessingResult.ValidationFailure
                {
                    Reason = $"Unsupported event version: {auditEvent.Version}. Expected: {EntityMutationAudited.CurrentVersion}",
                    Payload = SerializeForLogging(auditEvent),
                };
            }

            // Step 2: Validate required structure.
            var validationError = ValidateEventStructure(auditEvent);
            if (validationError != null)
            {
                return new AuditEventProcessingResult.ValidationFailure
                {
                    Reason = validationError,
                    Payload = SerializeForLogging(auditEvent),
                };
            }

            // Step 3: Check idempotency.
            // If the inbox is already completed, this is a duplicate delivery that already succeeded.
            if (inboxMessage.Status == InboxMessageStatus.Completed)
            {
                return new AuditEventProcessingResult.DuplicateAlreadyProcessed
                {
                    EventId = auditEvent.EventId,
                };
            }

            // Step 4: Redact sensitive fields from PreviousValues and NewValues (PRIV-002, AUDIT-008).
            var previousValues = RedactSensitiveValues(auditEvent.PreviousValues);
            var newValues = RedactSensitiveValues(auditEvent.NewValues);

            // Step 5: Map actor type and resolve display name.
            var (actorUserId, actorDisplayName) = ExtractActorInfo(auditEvent);

            // Step 6: Determine ActionCategory from Action (for efficient filtering).
            var actionCategory = DetermineActionCategory(auditEvent.Action);

            // Step 7: Build human-readable summary (for UI display, PRIV-003: minimize PII).
            var summaryDescription = BuildSummaryDescription(auditEvent, actionCategory);

            // Step 8: Serialize the raw event payload for forensics (redacted of secrets).
            var rawEventPayload = SerializeForStorageAsync(auditEvent);

            // Step 9: Create AuditEntry model with all metadata.
            var auditEntry = new AuditEntry
            {
                AuditEntryId = Guid.NewGuid(),
                EventId = auditEvent.EventId,
                EntityType = auditEvent.EntityType,
                EntityId = auditEvent.EntityId,
                Action = auditEvent.Action,
                ActionCategory = actionCategory,
                ActorUserId = actorUserId,
                ActorType = auditEvent.ActorType,
                ActorDisplayName = actorDisplayName,
                SourceService = auditEvent.SourceService,
                SourceEventType = $"{auditEvent.SourceService}.EntityMutationAudited",
                OccurredAtUtc = auditEvent.OccurredAtUtc.UtcDateTime,
                AuditedAtUtc = DateTime.UtcNow,
                TraceId = auditEvent.TraceId,
                CorrelationId = auditEvent.CorrelationId,
                CausationId = auditEvent.CausationId,
                ChangedFields = SerializeFieldNames(auditEvent.ChangedFields),
                PreviousValues = previousValues != null ? JsonSerializer.Serialize(previousValues) : null,
                NewValues = newValues != null ? JsonSerializer.Serialize(newValues) : null,
                SummaryDescription = summaryDescription,
                RawEventPayload = rawEventPayload,
            };

            // Step 10: Delegate to Data layer for atomic persistence with inbox idempotency.
            await _auditData.AppendAuditEntryIdempotentlyAsync(auditEntry, inboxMessage, cancellationToken);

            // Step 11: Return success.
            return new AuditEventProcessingResult.Success
            {
                EventId = auditEvent.EventId,
            };
        }
        catch (OperationCanceledException)
        {
            // Cancellation is not a failure; allow it to propagate.
            throw;
        }
        catch (Exception ex)
        {
            // Any other exception (database timeout, SQL constraint, etc.) is transient.
            // The Function trigger will fail and allow Service Bus to retry or dead-letter.
            return new AuditEventProcessingResult.TransientFailure
            {
                ErrorMessage = $"{ex.GetType().Name}: {ex.Message}",
                EventId = auditEvent.EventId,
            };
        }
    }

    /// <summary>
    /// Validate that the event has all required fields and they are non-empty.
    /// Returns null if valid, or an error message if invalid.
    /// </summary>
    private static string? ValidateEventStructure(EntityMutationAudited auditEvent)
    {
        if (string.IsNullOrWhiteSpace(auditEvent.EventId))
            return "EventId is required and non-empty.";

        if (string.IsNullOrWhiteSpace(auditEvent.SourceService))
            return "SourceService is required and non-empty.";

        if (string.IsNullOrWhiteSpace(auditEvent.EntityType))
            return "EntityType is required and non-empty.";

        if (auditEvent.EntityId == Guid.Empty)
            return "EntityId must be a non-empty GUID.";

        if (string.IsNullOrWhiteSpace(auditEvent.Action))
            return "Action is required and non-empty.";

        if (string.IsNullOrWhiteSpace(auditEvent.ActorType))
            return "ActorType is required and non-empty.";

        if (string.IsNullOrWhiteSpace(auditEvent.TraceId))
            return "TraceId is required and non-empty.";

        if (string.IsNullOrWhiteSpace(auditEvent.CorrelationId))
            return "CorrelationId is required and non-empty.";

        return null;
    }

    /// <summary>
    /// Redact sensitive fields from a values dictionary per AuditSensitiveFieldNames
    /// (PRIV-002: minimize sensitive data duplication, AUDIT-008: no secrets).
    /// Returns a new dictionary with forbidden fields removed, or null if input is null or all fields forbidden.
    /// </summary>
    private static IReadOnlyDictionary<string, string>? RedactSensitiveValues(
        IReadOnlyDictionary<string, string>? values)
    {
        if (values == null || values.Count == 0)
            return null;

        var redacted = new Dictionary<string, string>();
        foreach (var pair in values)
        {
            if (!AuditSensitiveFieldNames.IsForbidden(pair.Key))
            {
                redacted[pair.Key] = pair.Value;
            }
        }

        return redacted.Count > 0 ? redacted : null;
    }

    /// <summary>
    /// Extract actor information from the event.
    /// Returns (ActorUserId, ActorDisplayName) tuple.
    /// ActorUserId is null for System/Service-initiated actions.
    /// </summary>
    private static (Guid?, string?) ExtractActorInfo(EntityMutationAudited auditEvent)
    {
        // ActorId from the event is a string identifier; try to parse as GUID for User actor type.
        // For System/Service/Anonymous, ActorId may be absent; keep as null for ActorUserId.
        var actorUserId = auditEvent.ActorType == AuditActorTypes.User && !string.IsNullOrWhiteSpace(auditEvent.ActorId)
            ? (Guid.TryParse(auditEvent.ActorId, out var parsed) ? parsed : (Guid?)null)
            : null;

        // Display name is constructed from actor type and ID.
        // For User actors, use ActorId; for others, use ActorType.
        var actorDisplayName = auditEvent.ActorType switch
        {
            AuditActorTypes.User => auditEvent.ActorId ?? "Unknown User",
            AuditActorTypes.Service => $"Service: {auditEvent.ActorId ?? auditEvent.SourceService}",
            AuditActorTypes.System => "System",
            AuditActorTypes.Anonymous => "Anonymous",
            _ => auditEvent.ActorType,
        };

        return (actorUserId, actorDisplayName);
    }

    /// <summary>
    /// Determine ActionCategory from Action string.
    /// Categories help with efficient audit trail filtering and reporting (AUDIT-001..008).
    /// </summary>
    private static string DetermineActionCategory(string action)
    {
        return action switch
        {
            // Lifecycle/state changes
            AuditActions.Created or AuditActions.Updated => "WRITE",
            AuditActions.StatusChanged or AuditActions.PriorityChanged => "TRANSITION",
            AuditActions.Assigned or AuditActions.Reassigned => "ASSIGN",
            AuditActions.Completed or AuditActions.Reopened => "LIFECYCLE",
            AuditActions.Archived or AuditActions.Restored => "ARCHIVE",

            // Authentication
            AuditActions.LoggedIn or AuditActions.LoggedOut => "AUTH",
            AuditActions.FailedLogin or AuditActions.AccountLocked => "AUTH_FAILURE",

            // User management
            AuditActions.UserCreated or AuditActions.UserDeactivated or AuditActions.UserActivated => "USER_MGMT",
            AuditActions.PasswordChanged or AuditActions.PasswordReset or AuditActions.PasswordResetInitiated => "PASSWORD",
            AuditActions.RoleAdded or AuditActions.RoleRemoved => "ROLE",

            // Default for unknown actions
            _ => "OTHER",
        };
    }

    /// <summary>
    /// Build a human-readable summary for UI display (PRIV-003: minimize PII).
    /// Avoid including full names, email addresses, or other PII; focus on action and entity type.
    /// </summary>
    private static string BuildSummaryDescription(EntityMutationAudited auditEvent, string actionCategory)
    {
        var action = auditEvent.Action;
        var entityType = auditEvent.EntityType;
        var entityId = auditEvent.EntityId;

        return actionCategory switch
        {
            "WRITE" => $"{action} {entityType} {entityId:D}",
            "TRANSITION" => $"{action} on {entityType} {entityId:D}",
            "ASSIGN" => $"{action} {entityType} {entityId:D}",
            "LIFECYCLE" => $"{action} {entityType} {entityId:D}",
            "ARCHIVE" => $"{action} {entityType} {entityId:D}",
            "AUTH" or "AUTH_FAILURE" => $"{action} event",
            "USER_MGMT" or "PASSWORD" or "ROLE" => $"{action} on user account",
            _ => $"{action} on {entityType}",
        };
    }

    /// <summary>
    /// Serialize field names to JSON array for storage (AUDIT-002: record changed fields).
    /// </summary>
    private static string SerializeFieldNames(IReadOnlyList<string> fieldNames)
    {
        if (fieldNames == null || fieldNames.Count == 0)
            return "[]";

        return JsonSerializer.Serialize(fieldNames);
    }

    /// <summary>
    /// Serialize the audit event for storage in RawEventPayload (for forensics).
    /// This is the complete event payload as received, with all PreviousValues/NewValues
    /// already redacted by the publisher and further redacted here.
    /// </summary>
    private static string SerializeForStorageAsync(EntityMutationAudited auditEvent)
    {
        try
        {
            return JsonSerializer.Serialize(auditEvent, new JsonSerializerOptions { WriteIndented = false });
        }
        catch
        {
            // If serialization fails, return empty JSON object as fallback.
            return "{}";
        }
    }

    /// <summary>
    /// Serialize the audit event for logging (redacted, minimal PII).
    /// Used only in error cases to avoid logging full sensitive payloads.
    /// </summary>
    private static string? SerializeForLogging(EntityMutationAudited auditEvent)
    {
        try
        {
            var minimal = new
            {
                auditEvent.EventId,
                auditEvent.SourceService,
                auditEvent.EntityType,
                auditEvent.EntityId,
                auditEvent.Action,
                auditEvent.Version,
            };
            return JsonSerializer.Serialize(minimal);
        }
        catch
        {
            return null;
        }
    }
}

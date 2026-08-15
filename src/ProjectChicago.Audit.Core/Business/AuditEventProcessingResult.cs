namespace ProjectChicago.Audit.Core.Business;

/// <summary>
/// Result of attempting to process an audit event (EntityMutationAudited) through the Audit service
/// (AUDIT-001..008, PRIV-001..005, ASYNC-005..008).
/// </summary>
public abstract record AuditEventProcessingResult
{
    /// <summary>
    /// Event was successfully validated, translated, and queued for persistence (ASYNC-005 first delivery).
    /// </summary>
    public sealed record Success : AuditEventProcessingResult
    {
        /// <summary>The EventId from the processed audit event (idempotency key).</summary>
        public required string EventId { get; init; }
    }

    /// <summary>
    /// Event processing was skipped because it was a duplicate (same MessageId already Completed).
    /// This is expected for Service Bus redelivery (ASYNC-005: duplicate tolerance).
    /// </summary>
    public sealed record DuplicateAlreadyProcessed : AuditEventProcessingResult
    {
        /// <summary>The EventId from the duplicate event.</summary>
        public required string EventId { get; init; }
    }

    /// <summary>
    /// Event validation failed: unsupported contract version, malformed payload, or missing required fields.
    /// The event should not be retried; it must be dead-lettered.
    /// </summary>
    public sealed record ValidationFailure : AuditEventProcessingResult
    {
        /// <summary>The specific validation error (e.g., "Unsupported version: 2").</summary>
        public required string Reason { get; init; }

        /// <summary>The raw event payload for forensics (redacted of secrets).</summary>
        public string? Payload { get; init; }
    }

    /// <summary>
    /// A transient error occurred (e.g., database timeout, Service Bus temporary failure).
    /// The invocation should fail so the Function trigger can retry or dead-letter per policy.
    /// </summary>
    public sealed record TransientFailure : AuditEventProcessingResult
    {
        /// <summary>The exception details.</summary>
        public required string ErrorMessage { get; init; }

        /// <summary>The EventId if available (may be null if error occurred during deserialization).</summary>
        public string? EventId { get; init; }
    }
}

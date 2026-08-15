using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using ProjectChicago.Audit.Core.Business;
using ProjectChicago.Audit.Core.Persistence;
using ProjectChicago.Contracts.Audit;
using ProjectChicago.Shared.Inbox;
using ProjectChicago.Shared.Messaging;
using System.Diagnostics;

namespace ProjectChicago.Audit.Functions;

/// <summary>
/// Azure Function trigger for consuming EntityMutationAudited events from Service Bus
/// (ADR-0016, ADR-0017, AUDIT-001..008, ASYNC-001..008, TRACE-003..007).
///
/// Responsibility: Envelope deserialization and trace correlation only.
/// Audit mapping/persistence logic lives in AuditEventBusiness (per ADR-0016 boundaries).
///
/// Behavior:
/// - Binds ServiceBusReceivedMessage from subscription (Configuration in ServiceBusProcessor)
/// - Deserializes EventEnvelope<EntityMutationAudited>
/// - Establishes W3C trace context (TraceId, SpanId)
/// - Establishes correlation/causation context
/// - Registers inbox message for idempotency (ASYNC-005)
/// - Calls Facade to process event (validation, redaction, persistence)
/// - Returns immediately on success
/// - Throws on error (allow Service Bus retry/dead-letter per policy, ASYNC-007)
/// - Does NOT catch-and-return-success for failures
/// - Does NOT contain business logic
/// </summary>
public class ProcessAuditEventTrigger
{
    private readonly IAuditEventBusiness _auditBusiness;
    private readonly AuditDbContext _auditDbContext;
    private readonly ILogger _logger;

    public ProcessAuditEventTrigger(
        IAuditEventBusiness auditBusiness,
        AuditDbContext auditDbContext,
        ILoggerFactory loggerFactory)
    {
        _auditBusiness = auditBusiness ?? throw new ArgumentNullException(nameof(auditBusiness));
        _auditDbContext = auditDbContext ?? throw new ArgumentNullException(nameof(auditDbContext));
        _logger = loggerFactory.CreateLogger<ProcessAuditEventTrigger>();
    }

    /// <summary>
    /// Process an audit event from Service Bus subscription.
    /// Binding configuration (topic, subscription, auth) comes from environment and ServiceBusProcessor.
    /// </summary>
    [Function("ProcessAuditEvent")]
    public async Task RunAsync(
        [ServiceBusTrigger("ProjectChicago.Events", "Audit", Connection = "messaging")] ServiceBusReceivedMessage message,
        FunctionContext context)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(context);

        var invocationId = context.InvocationId;
        var messageId = message.MessageId;

        try
        {
            // Step 1: Deserialize the event envelope.
            var body = message.Body.ToString();
            EntityMutationAudited auditEvent;
            try
            {
                var envelope = EventEnvelopeSerializer.Deserialize<EntityMutationAudited>(
                    body,
                    [EntityMutationAudited.CurrentVersion]);

                auditEvent = envelope.Payload;

                // Step 2: Establish W3C trace context (TRACE-003..007).
                // Extract trace context from message headers if present (Service Bus preserves W3C TraceParent).
                var traceIdFromEvent = envelope.TraceId;
                var correlationIdFromEvent = envelope.CorrelationId;
                var causationIdFromEvent = envelope.CausationId;

                // Create a new Activity for this Function invocation (OpenTelemetry instrumentation).
                // The Activity captures the span context and establishes parent-child relationships.
                using var activity = new Activity("ProcessAuditEvent").Start();
                if (activity != null)
                {
                    activity.SetTag("messaging.message_id", messageId);
                    activity.SetTag("messaging.payload_type", "Audit.EntityMutationAudited");
                    activity.SetTag("audit.entity_type", auditEvent.EntityType);
                    activity.SetTag("audit.entity_id", auditEvent.EntityId);
                    activity.SetTag("audit.action", auditEvent.Action);
                    activity.SetTag("trace.correlation_id", correlationIdFromEvent);
                    if (!string.IsNullOrEmpty(causationIdFromEvent))
                    {
                        activity.SetTag("trace.causation_id", causationIdFromEvent);
                    }
                }

                _logger.LogInformation(
                    "Processing audit event: EventId={EventId}, Entity={EntityType}/{EntityId}, " +
                    "Action={Action}, CorrelationId={CorrelationId}, TraceId={TraceId}, MessageId={MessageId}",
                    auditEvent.EventId, auditEvent.EntityType, auditEvent.EntityId,
                    auditEvent.Action, correlationIdFromEvent, traceIdFromEvent, messageId);

                // Step 3: Register inbox message for idempotency (ASYNC-005, ASYNC-006).
                var inboxMessage = new InboxMessage
                {
                    MessageId = messageId,
                    ContractType = "Audit.EntityMutationAudited",
                    ContractVersion = EntityMutationAudited.CurrentVersion,
                    CorrelationId = correlationIdFromEvent,
                    CausationId = causationIdFromEvent,
                    TraceId = traceIdFromEvent,
                    ReceivedAtUtc = DateTime.UtcNow,
                    Status = InboxMessageStatus.Received,
                    AttemptCount = 0,
                };

                // Step 4: Call Facade to process the event.
                // The Facade handles validation, redaction, mapping, and delegation to Data layer.
                var result = await _auditBusiness.ProcessAuditEventAsync(
                    auditEvent, inboxMessage, context.CancellationToken);

                // Step 5: Handle result.
                switch (result)
                {
                    case AuditEventProcessingResult.Success success:
                        _logger.LogInformation(
                            "Audit event processed successfully: EventId={EventId}, " +
                            "Entity={EntityType}/{EntityId}, MessageId={MessageId}",
                            success.EventId, auditEvent.EntityType, auditEvent.EntityId, messageId);
                        return; // Success: Function completes successfully, Service Bus marks message consumed.

                    case AuditEventProcessingResult.DuplicateAlreadyProcessed duplicate:
                        // Duplicate delivery with inbox already completed: safe no-op (ASYNC-005).
                        // Return success; Service Bus will mark the message consumed.
                        _logger.LogInformation(
                            "Audit event already processed (duplicate): EventId={EventId}, " +
                            "MessageId={MessageId}",
                            duplicate.EventId, messageId);
                        return;

                    case AuditEventProcessingResult.ValidationFailure validation:
                        // Validation failure (unsupported version, malformed): dead-letter (ASYNC-007).
                        // Throw with the validation error; Function fails, message dead-letters after retries exhausted.
                        _logger.LogError(
                            "Audit event validation failed: Reason={Reason}, " +
                            "MessageId={MessageId}, Payload={Payload}",
                            validation.Reason, messageId, validation.Payload);
                        throw new InvalidOperationException(
                            $"Audit event validation failed: {validation.Reason}");

                    case AuditEventProcessingResult.TransientFailure transient:
                        // Transient failure (database timeout, etc.): allow retry (ASYNC-007).
                        // Throw with the error; Function fails, Service Bus retries per policy.
                        _logger.LogError(
                            "Audit event processing failed (transient): Error={Error}, " +
                            "EventId={EventId}, MessageId={MessageId}",
                            transient.ErrorMessage, transient.EventId, messageId);
                        throw new InvalidOperationException(
                            $"Audit event processing failed: {transient.ErrorMessage}");

                    default:
                        // Unexpected result type.
                        _logger.LogError(
                            "Unexpected audit event result type: Type={ResultType}, MessageId={MessageId}",
                            result.GetType().Name, messageId);
                        throw new InvalidOperationException(
                            $"Unexpected audit event processing result: {result.GetType().Name}");
                }
            }
            catch (UnsupportedContractVersionException ex)
            {
                // Envelope deserialization failed due to unsupported version: dead-letter (ASYNC-007).
                _logger.LogError(
                    ex,
                    "Unsupported contract version: ContractType={ContractType}, Version={Version}, MessageId={MessageId}",
                    ex.ContractType, ex.ContractVersion, messageId);
                throw; // Propagate to fail the Function invocation; Service Bus dead-letters after retries.
            }
            catch (EnvelopeDeserializationException ex)
            {
                // Envelope deserialization failed: malformed payload (dead-letter).
                _logger.LogError(
                    ex,
                    "Envelope deserialization failed: MessageId={MessageId}",
                    messageId);
                throw; // Propagate to fail the Function invocation; Service Bus dead-letters after retries.
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("ProcessAuditEvent was cancelled: MessageId={MessageId}", messageId);
            throw; // Propagate cancellation.
        }
        catch (Exception ex)
        {
            // Unexpected exception: log and propagate (allow Service Bus retry).
            _logger.LogError(
                ex,
                "Unexpected error in ProcessAuditEvent: MessageId={MessageId}, InvocationId={InvocationId}",
                messageId, invocationId);
            throw; // Propagate to fail the Function invocation; Service Bus retries per policy.
        }
    }
}

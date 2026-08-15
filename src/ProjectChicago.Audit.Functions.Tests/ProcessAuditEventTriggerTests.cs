using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Moq;
using ProjectChicago.Audit.Core.Business;
using ProjectChicago.Audit.Core.Persistence;
using ProjectChicago.Contracts.Audit;
using ProjectChicago.Shared.Inbox;
using ProjectChicago.Shared.Messaging;
using Xunit;

namespace ProjectChicago.Audit.Functions.Tests;

/// <summary>
/// Unit tests for ProcessAuditEventTrigger Service Bus trigger function
/// (ADR-0016, ADR-0017, AUDIT-001..008, ASYNC-001..008, TRACE-003..007).
/// Tests validate: envelope deserialization, trace correlation, business delegation, error handling.
/// Does NOT execute against real Service Bus; mocks Azure Functions runtime and Audit.Business.
/// </summary>
public class ProcessAuditEventTriggerTests
{
    private static readonly DateTime OccurredAtUtc = new(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);

    private static ServiceBusReceivedMessage CreateServiceBusMessage(string body, string? messageId = null)
    {
        // Create a ServiceBusReceivedMessage with the given body.
        // Note: ServiceBusReceivedMessage is a sealed class; we use a helper to create a mock-friendly version.
        var msg = ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: BinaryData.FromString(body),
            messageId: messageId ?? Guid.NewGuid().ToString());
        return msg;
    }

    private static EntityMutationAudited CreateValidAuditEvent(string eventId = "event-1") =>
        new()
        {
            EventId = eventId,
            Version = EntityMutationAudited.CurrentVersion,
            OccurredAtUtc = new DateTimeOffset(OccurredAtUtc),
            SourceService = AuditSourceServices.Crm,
            EntityType = AuditEntityTypes.Client,
            EntityId = Guid.NewGuid(),
            Action = AuditActions.Created,
            ActorId = Guid.NewGuid().ToString(),
            ActorType = AuditActorTypes.User,
            TraceId = Guid.NewGuid().ToString("N"),
            CorrelationId = Guid.NewGuid().ToString(),
            CausationId = Guid.NewGuid().ToString(),
            ChangedFields = new[] { "Name", "Email" },
            PreviousValues = null,
            NewValues = new Dictionary<string, string> { { "Name", "Acme Corp" } },
        };

    private static EventEnvelope<EntityMutationAudited> CreateValidEventEnvelope(
        EntityMutationAudited? auditEvent = null,
        string? eventId = null) =>
        new()
        {
            EventId = eventId ?? Guid.NewGuid().ToString(),
            ContractType = "Audit.EntityMutationAudited",
            ContractVersion = EntityMutationAudited.CurrentVersion,
            OccurredAtUtc = new DateTimeOffset(OccurredAtUtc),
            CorrelationId = Guid.NewGuid().ToString(),
            CausationId = Guid.NewGuid().ToString(),
            TraceId = Guid.NewGuid().ToString("N"),
            Payload = auditEvent ?? CreateValidAuditEvent(eventId ?? Guid.NewGuid().ToString()),
        };

    private static FunctionContext CreateMockFunctionContext()
    {
        var mockContext = new Mock<FunctionContext>();
        mockContext
            .Setup(c => c.InvocationId)
            .Returns(Guid.NewGuid().ToString());
        mockContext
            .Setup(c => c.CancellationToken)
            .Returns(CancellationToken.None);

        return mockContext.Object;
    }

    private static ILoggerFactory CreateMockLoggerFactory()
    {
        var mockLoggerFactory = new Mock<ILoggerFactory>();
        var mockLogger = new Mock<ILogger>();

        mockLoggerFactory
            .Setup(f => f.CreateLogger(It.IsAny<string>()))
            .Returns(mockLogger.Object);

        return mockLoggerFactory.Object;
    }

    // Scenario: Valid event envelope is deserialized, delegated to Facade, and processed successfully

    [Fact]
    public async Task RunAsync_ValidEvent_DelegatesBusinessAndReturnsSuccess()
    {
        var mockAuditBusiness = new Mock<IAuditEventBusiness>();
        mockAuditBusiness
            .Setup(x => x.ProcessAuditEventAsync(
                It.IsAny<EntityMutationAudited>(),
                It.IsAny<InboxMessage>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuditEventProcessingResult.Success { EventId = "event-1" });

        var mockDbContext = new Mock<AuditDbContext>(new Microsoft.EntityFrameworkCore.DbContextOptions<AuditDbContext>());

        var trigger = new ProcessAuditEventTrigger(
            mockAuditBusiness.Object,
            mockDbContext.Object,
            CreateMockLoggerFactory());

        var auditEvent = CreateValidAuditEvent("event-1");
        var envelope = CreateValidEventEnvelope(auditEvent, "event-1");
        var envelopeJson = EventEnvelopeSerializer.Serialize(envelope);

        var serviceMessage = CreateServiceBusMessage(envelopeJson, "msg-1");
        var functionContext = CreateMockFunctionContext();

        // Act: Should complete successfully without throwing.
        await trigger.RunAsync(serviceMessage, functionContext);

        // Verify Facade was called exactly once.
        mockAuditBusiness.Verify(
            x => x.ProcessAuditEventAsync(
                It.IsAny<EntityMutationAudited>(),
                It.IsAny<InboxMessage>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RunAsync_ValidEvent_PassesInboxMessageWithCorrelationMetadata()
    {
        var capturedInboxMessage = default(InboxMessage);
        var mockAuditBusiness = new Mock<IAuditEventBusiness>();
        mockAuditBusiness
            .Setup(x => x.ProcessAuditEventAsync(
                It.IsAny<EntityMutationAudited>(),
                It.IsAny<InboxMessage>(),
                It.IsAny<CancellationToken>()))
            .Callback<EntityMutationAudited, InboxMessage, CancellationToken>((_, inbox, _) => capturedInboxMessage = inbox)
            .ReturnsAsync(new AuditEventProcessingResult.Success { EventId = "event-2" });

        var mockDbContext = new Mock<AuditDbContext>(new Microsoft.EntityFrameworkCore.DbContextOptions<AuditDbContext>());

        var trigger = new ProcessAuditEventTrigger(
            mockAuditBusiness.Object,
            mockDbContext.Object,
            CreateMockLoggerFactory());

        var traceId = Guid.NewGuid().ToString("N");
        var correlationId = Guid.NewGuid().ToString();
        var causationId = Guid.NewGuid().ToString();

        var auditEvent = CreateValidAuditEvent("event-2");
        var envelope = new EventEnvelope<EntityMutationAudited>
        {
            EventId = "event-2",
            ContractType = "Audit.EntityMutationAudited",
            ContractVersion = EntityMutationAudited.CurrentVersion,
            OccurredAtUtc = new DateTimeOffset(OccurredAtUtc),
            CorrelationId = correlationId,
            CausationId = causationId,
            TraceId = traceId,
            Payload = auditEvent,
        };

        var envelopeJson = EventEnvelopeSerializer.Serialize(envelope);
        var messageId = "msg-2";
        var serviceMessage = CreateServiceBusMessage(envelopeJson, messageId);
        var functionContext = CreateMockFunctionContext();

        // Act
        await trigger.RunAsync(serviceMessage, functionContext);

        // Assert: InboxMessage should have correct correlation metadata.
        Assert.NotNull(capturedInboxMessage);
        Assert.Equal(messageId, capturedInboxMessage.MessageId);
        Assert.Equal(traceId, capturedInboxMessage.TraceId);
        Assert.Equal(correlationId, capturedInboxMessage.CorrelationId);
        Assert.Equal(causationId, capturedInboxMessage.CausationId);
    }

    // Scenario: Duplicate already processed returns success (ASYNC-005)

    [Fact]
    public async Task RunAsync_DuplicateAlreadyProcessed_ReturnsSuccessWithoutError()
    {
        var mockAuditBusiness = new Mock<IAuditEventBusiness>();
        mockAuditBusiness
            .Setup(x => x.ProcessAuditEventAsync(
                It.IsAny<EntityMutationAudited>(),
                It.IsAny<InboxMessage>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuditEventProcessingResult.DuplicateAlreadyProcessed { EventId = "event-dup" });

        var mockDbContext = new Mock<AuditDbContext>(new Microsoft.EntityFrameworkCore.DbContextOptions<AuditDbContext>());

        var trigger = new ProcessAuditEventTrigger(
            mockAuditBusiness.Object,
            mockDbContext.Object,
            CreateMockLoggerFactory());

        var auditEvent = CreateValidAuditEvent("event-dup");
        var envelope = CreateValidEventEnvelope(auditEvent, "event-dup");
        var envelopeJson = EventEnvelopeSerializer.Serialize(envelope);

        var serviceMessage = CreateServiceBusMessage(envelopeJson, "msg-dup");
        var functionContext = CreateMockFunctionContext();

        // Act: Should complete successfully (duplicate is a safe no-op).
        await trigger.RunAsync(serviceMessage, functionContext);

        // No exception should be thrown.
    }

    // Scenario: Unsupported contract version is rejected with dead-letter policy (ASYNC-007)

    [Fact]
    public async Task RunAsync_UnsupportedContractVersion_ThrowsUnsupportedContractVersionException()
    {
        var mockAuditBusiness = new Mock<IAuditEventBusiness>();
        var mockDbContext = new Mock<AuditDbContext>(new Microsoft.EntityFrameworkCore.DbContextOptions<AuditDbContext>());

        var trigger = new ProcessAuditEventTrigger(
            mockAuditBusiness.Object,
            mockDbContext.Object,
            CreateMockLoggerFactory());

        var auditEvent = CreateValidAuditEvent("event-unsupported");
        var envelope = new EventEnvelope<EntityMutationAudited>
        {
            EventId = "event-unsupported",
            ContractType = "Audit.EntityMutationAudited",
            ContractVersion = 99, // Unsupported version
            OccurredAtUtc = new DateTimeOffset(OccurredAtUtc),
            CorrelationId = Guid.NewGuid().ToString(),
            TraceId = Guid.NewGuid().ToString("N"),
            Payload = auditEvent,
        };

        var envelopeJson = EventEnvelopeSerializer.Serialize(envelope);
        var serviceMessage = CreateServiceBusMessage(envelopeJson);
        var functionContext = CreateMockFunctionContext();

        // Act & Assert: Should throw UnsupportedContractVersionException.
        await Assert.ThrowsAsync<UnsupportedContractVersionException>(
            () => trigger.RunAsync(serviceMessage, functionContext));

        // Verify Facade was not called (version check failed before delegation).
        mockAuditBusiness.Verify(
            x => x.ProcessAuditEventAsync(
                It.IsAny<EntityMutationAudited>(),
                It.IsAny<InboxMessage>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // Scenario: Malformed envelope JSON is rejected (EnvelopeDeserializationException)

    [Fact]
    public async Task RunAsync_MalformedEnvelope_ThrowsEnvelopeDeserializationException()
    {
        var mockAuditBusiness = new Mock<IAuditEventBusiness>();
        var mockDbContext = new Mock<AuditDbContext>(new Microsoft.EntityFrameworkCore.DbContextOptions<AuditDbContext>());

        var trigger = new ProcessAuditEventTrigger(
            mockAuditBusiness.Object,
            mockDbContext.Object,
            CreateMockLoggerFactory());

        var malformedJson = "{invalid json";
        var serviceMessage = CreateServiceBusMessage(malformedJson);
        var functionContext = CreateMockFunctionContext();

        // Act & Assert: Should throw EnvelopeDeserializationException.
        await Assert.ThrowsAsync<EnvelopeDeserializationException>(
            () => trigger.RunAsync(serviceMessage, functionContext));

        // Verify Facade was not called.
        mockAuditBusiness.Verify(
            x => x.ProcessAuditEventAsync(
                It.IsAny<EntityMutationAudited>(),
                It.IsAny<InboxMessage>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // Scenario: Business validation failure throws InvalidOperationException (dead-letter)

    [Fact]
    public async Task RunAsync_ValidationFailure_ThrowsInvalidOperationException()
    {
        var mockAuditBusiness = new Mock<IAuditEventBusiness>();
        mockAuditBusiness
            .Setup(x => x.ProcessAuditEventAsync(
                It.IsAny<EntityMutationAudited>(),
                It.IsAny<InboxMessage>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuditEventProcessingResult.ValidationFailure
            {
                Reason = "Unsupported entity type",
                Payload = "{}",
            });

        var mockDbContext = new Mock<AuditDbContext>(new Microsoft.EntityFrameworkCore.DbContextOptions<AuditDbContext>());

        var trigger = new ProcessAuditEventTrigger(
            mockAuditBusiness.Object,
            mockDbContext.Object,
            CreateMockLoggerFactory());

        var auditEvent = CreateValidAuditEvent("event-invalid");
        var envelope = CreateValidEventEnvelope(auditEvent, "event-invalid");
        var envelopeJson = EventEnvelopeSerializer.Serialize(envelope);

        var serviceMessage = CreateServiceBusMessage(envelopeJson);
        var functionContext = CreateMockFunctionContext();

        // Act & Assert: Should throw InvalidOperationException with validation error reason.
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => trigger.RunAsync(serviceMessage, functionContext));
        Assert.Contains("Unsupported entity type", ex.Message);
    }

    // Scenario: Business transient failure throws InvalidOperationException (allow retry)

    [Fact]
    public async Task RunAsync_TransientFailure_ThrowsInvalidOperationExceptionAllowingRetry()
    {
        var mockAuditBusiness = new Mock<IAuditEventBusiness>();
        mockAuditBusiness
            .Setup(x => x.ProcessAuditEventAsync(
                It.IsAny<EntityMutationAudited>(),
                It.IsAny<InboxMessage>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuditEventProcessingResult.TransientFailure
            {
                ErrorMessage = "Database connection timeout",
                EventId = "event-timeout",
            });

        var mockDbContext = new Mock<AuditDbContext>(new Microsoft.EntityFrameworkCore.DbContextOptions<AuditDbContext>());

        var trigger = new ProcessAuditEventTrigger(
            mockAuditBusiness.Object,
            mockDbContext.Object,
            CreateMockLoggerFactory());

        var auditEvent = CreateValidAuditEvent("event-timeout");
        var envelope = CreateValidEventEnvelope(auditEvent, "event-timeout");
        var envelopeJson = EventEnvelopeSerializer.Serialize(envelope);

        var serviceMessage = CreateServiceBusMessage(envelopeJson);
        var functionContext = CreateMockFunctionContext();

        // Act & Assert: Should throw InvalidOperationException (allowing Service Bus to retry).
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => trigger.RunAsync(serviceMessage, functionContext));
        Assert.Contains("Database connection timeout", ex.Message);
    }

    // Scenario: Unexpected exception type is propagated (allow retry)

    [Fact]
    public async Task RunAsync_UnexpectedBusinessException_PropagatesException()
    {
        var mockAuditBusiness = new Mock<IAuditEventBusiness>();
        mockAuditBusiness
            .Setup(x => x.ProcessAuditEventAsync(
                It.IsAny<EntityMutationAudited>(),
                It.IsAny<InboxMessage>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NullReferenceException("Unexpected null reference"));

        var mockDbContext = new Mock<AuditDbContext>(new Microsoft.EntityFrameworkCore.DbContextOptions<AuditDbContext>());

        var trigger = new ProcessAuditEventTrigger(
            mockAuditBusiness.Object,
            mockDbContext.Object,
            CreateMockLoggerFactory());

        var auditEvent = CreateValidAuditEvent("event-error");
        var envelope = CreateValidEventEnvelope(auditEvent, "event-error");
        var envelopeJson = EventEnvelopeSerializer.Serialize(envelope);

        var serviceMessage = CreateServiceBusMessage(envelopeJson);
        var functionContext = CreateMockFunctionContext();

        // Act & Assert: Should propagate the underlying exception.
        await Assert.ThrowsAsync<NullReferenceException>(
            () => trigger.RunAsync(serviceMessage, functionContext));
    }

    // Scenario: Cancellation is propagated (not swallowed)

    [Fact]
    public async Task RunAsync_CancellationRequested_PropagateCancellationException()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var mockAuditBusiness = new Mock<IAuditEventBusiness>();
        mockAuditBusiness
            .Setup(x => x.ProcessAuditEventAsync(
                It.IsAny<EntityMutationAudited>(),
                It.IsAny<InboxMessage>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var mockDbContext = new Mock<AuditDbContext>(new Microsoft.EntityFrameworkCore.DbContextOptions<AuditDbContext>());

        var trigger = new ProcessAuditEventTrigger(
            mockAuditBusiness.Object,
            mockDbContext.Object,
            CreateMockLoggerFactory());

        var auditEvent = CreateValidAuditEvent("event-cancel");
        var envelope = CreateValidEventEnvelope(auditEvent, "event-cancel");
        var envelopeJson = EventEnvelopeSerializer.Serialize(envelope);

        var serviceMessage = CreateServiceBusMessage(envelopeJson);

        var mockContext = new Mock<FunctionContext>();
        mockContext
            .Setup(c => c.InvocationId)
            .Returns(Guid.NewGuid().ToString());
        mockContext
            .Setup(c => c.CancellationToken)
            .Returns(cts.Token);

        // Act & Assert: Should propagate OperationCanceledException.
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => trigger.RunAsync(serviceMessage, mockContext.Object));
    }

    // Scenario: Trace/correlation/causation context is preserved

    [Fact]
    public async Task RunAsync_ValidEvent_PreservesTraceCorrelationCausationContext()
    {
        var capturedAuditEvent = default(EntityMutationAudited);
        var capturedInboxMessage = default(InboxMessage);

        var mockAuditBusiness = new Mock<IAuditEventBusiness>();
        mockAuditBusiness
            .Setup(x => x.ProcessAuditEventAsync(
                It.IsAny<EntityMutationAudited>(),
                It.IsAny<InboxMessage>(),
                It.IsAny<CancellationToken>()))
            .Callback<EntityMutationAudited, InboxMessage, CancellationToken>(
                (audit, inbox, _) =>
                {
                    capturedAuditEvent = audit;
                    capturedInboxMessage = inbox;
                })
            .ReturnsAsync(new AuditEventProcessingResult.Success { EventId = "event-trace" });

        var mockDbContext = new Mock<AuditDbContext>(new Microsoft.EntityFrameworkCore.DbContextOptions<AuditDbContext>());

        var trigger = new ProcessAuditEventTrigger(
            mockAuditBusiness.Object,
            mockDbContext.Object,
            CreateMockLoggerFactory());

        var traceId = Guid.NewGuid().ToString("N");
        var correlationId = Guid.NewGuid().ToString();
        var causationId = Guid.NewGuid().ToString();

        var auditEvent = new EntityMutationAudited
        {
            EventId = "event-trace",
            Version = EntityMutationAudited.CurrentVersion,
            OccurredAtUtc = new DateTimeOffset(OccurredAtUtc),
            SourceService = AuditSourceServices.Crm,
            EntityType = AuditEntityTypes.Client,
            EntityId = Guid.NewGuid(),
            Action = AuditActions.Created,
            ActorId = Guid.NewGuid().ToString(),
            ActorType = AuditActorTypes.User,
            TraceId = traceId,
            CorrelationId = correlationId,
            CausationId = causationId,
            ChangedFields = Array.Empty<string>(),
        };

        var envelope = new EventEnvelope<EntityMutationAudited>
        {
            EventId = "event-trace",
            ContractType = "Audit.EntityMutationAudited",
            ContractVersion = EntityMutationAudited.CurrentVersion,
            OccurredAtUtc = new DateTimeOffset(OccurredAtUtc),
            CorrelationId = correlationId,
            CausationId = causationId,
            TraceId = traceId,
            Payload = auditEvent,
        };

        var envelopeJson = EventEnvelopeSerializer.Serialize(envelope);
        var serviceMessage = CreateServiceBusMessage(envelopeJson, "msg-trace");
        var functionContext = CreateMockFunctionContext();

        // Act
        await trigger.RunAsync(serviceMessage, functionContext);

        // Assert: Trace/correlation/causation context should be preserved.
        Assert.NotNull(capturedAuditEvent);
        Assert.NotNull(capturedInboxMessage);

        Assert.Equal(traceId, capturedAuditEvent.TraceId);
        Assert.Equal(correlationId, capturedAuditEvent.CorrelationId);
        Assert.Equal(causationId, capturedAuditEvent.CausationId);

        Assert.Equal(traceId, capturedInboxMessage.TraceId);
        Assert.Equal(correlationId, capturedInboxMessage.CorrelationId);
        Assert.Equal(causationId, capturedInboxMessage.CausationId);
    }
}

using Moq;
using ProjectChicago.Audit.Core.Business;
using ProjectChicago.Audit.Core.Data;
using ProjectChicago.Audit.Core.Persistence;
using ProjectChicago.Contracts.Audit;
using ProjectChicago.Shared.Inbox;
using Xunit;

namespace ProjectChicago.Audit.Core.Tests.Business;

/// <summary>
/// Unit tests for AuditEventBusiness ingestion/translation layer (ADR-0016, AUDIT-001..008, PRIV-001..005, ASYNC-005..008).
/// Tests validate: event structure, redaction, version support, duplicate handling, and Data layer delegation.
/// Does NOT execute against real database (use mocks); real transaction atomicity is tested in AuditDataTests.
/// </summary>
public class AuditEventBusinessTests
{
    private static readonly DateTime OccurredAtUtc = new(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);

    private static EntityMutationAudited CreateValidEvent(
        string eventId = "event-1",
        string action = AuditActions.Created,
        string entityType = AuditEntityTypes.Client) =>
        new()
        {
            EventId = eventId,
            Version = EntityMutationAudited.CurrentVersion,
            OccurredAtUtc = new DateTimeOffset(OccurredAtUtc),
            SourceService = AuditSourceServices.Crm,
            EntityType = entityType,
            EntityId = Guid.NewGuid(),
            Action = action,
            ActorId = Guid.NewGuid().ToString(),
            ActorType = AuditActorTypes.User,
            TraceId = Guid.NewGuid().ToString("N"),
            CorrelationId = Guid.NewGuid().ToString(),
            CausationId = Guid.NewGuid().ToString(),
            ChangedFields = new[] { "Name", "Email" },
            PreviousValues = new Dictionary<string, string> { { "Name", "Old Corp" }, { "Email", "old@example.com" } },
            NewValues = new Dictionary<string, string> { { "Name", "New Corp" }, { "Email", "new@example.com" } },
        };

    private static InboxMessage CreateInboxMessage(string messageId = "msg-1") =>
        new()
        {
            MessageId = messageId,
            ContractType = "Audit.EntityMutationAudited",
            ContractVersion = 1,
            CorrelationId = Guid.NewGuid().ToString(),
            TraceId = Guid.NewGuid().ToString("N"),
            ReceivedAtUtc = DateTime.UtcNow,
            Status = InboxMessageStatus.Received,
            AttemptCount = 0,
        };

    // Scenario: Supported event with valid structure is processed successfully

    [Fact]
    public async Task ProcessAuditEventAsync_ValidEvent_DelegatesDataLayerAndReturnsSuccess()
    {
        var mockAuditData = new Mock<IAuditData>();
        var business = new AuditEventBusiness(mockAuditData.Object);

        var auditEvent = CreateValidEvent();
        var inboxMessage = CreateInboxMessage();

        var result = await business.ProcessAuditEventAsync(auditEvent, inboxMessage, CancellationToken.None);

        Assert.IsType<AuditEventProcessingResult.Success>(result);
        var success = (AuditEventProcessingResult.Success)result;
        Assert.Equal(auditEvent.EventId, success.EventId);

        // Verify Data layer was called exactly once.
        mockAuditData.Verify(
            x => x.AppendAuditEntryIdempotentlyAsync(It.IsAny<AuditEntry>(), It.IsAny<InboxMessage>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessAuditEventAsync_ValidEvent_PreservesCorrelationAndTraceMetadata()
    {
        var capturedAuditEntry = default(AuditEntry);
        var mockAuditData = new Mock<IAuditData>();
        mockAuditData
            .Setup(x => x.AppendAuditEntryIdempotentlyAsync(It.IsAny<AuditEntry>(), It.IsAny<InboxMessage>(), It.IsAny<CancellationToken>()))
            .Callback<AuditEntry, InboxMessage, CancellationToken>((entry, _, _) => capturedAuditEntry = entry)
            .Returns(Task.CompletedTask);

        var business = new AuditEventBusiness(mockAuditData.Object);

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

        var inboxMessage = CreateInboxMessage();

        await business.ProcessAuditEventAsync(auditEvent, inboxMessage, CancellationToken.None);

        Assert.NotNull(capturedAuditEntry);
        Assert.Equal(traceId, capturedAuditEntry.TraceId);
        Assert.Equal(correlationId, capturedAuditEntry.CorrelationId);
        Assert.Equal(causationId, capturedAuditEntry.CausationId);
    }

    // Scenario: Sensitive fields are redacted per AuditSensitiveFieldNames (PRIV-002, AUDIT-008)

    [Fact]
    public async Task ProcessAuditEventAsync_Event_RedactsSensitiveFieldsFromPreviousAndNewValues()
    {
        var capturedAuditEntry = default(AuditEntry);
        var mockAuditData = new Mock<IAuditData>();
        mockAuditData
            .Setup(x => x.AppendAuditEntryIdempotentlyAsync(It.IsAny<AuditEntry>(), It.IsAny<InboxMessage>(), It.IsAny<CancellationToken>()))
            .Callback<AuditEntry, InboxMessage, CancellationToken>((entry, _, _) => capturedAuditEntry = entry)
            .Returns(Task.CompletedTask);

        var business = new AuditEventBusiness(mockAuditData.Object);

        var auditEvent = new EntityMutationAudited
        {
            EventId = "event-redact",
            Version = EntityMutationAudited.CurrentVersion,
            OccurredAtUtc = new DateTimeOffset(OccurredAtUtc),
            SourceService = AuditSourceServices.Crm,
            EntityType = AuditEntityTypes.ApplicationUser,
            EntityId = Guid.NewGuid(),
            Action = AuditActions.PasswordChanged,
            ActorId = Guid.NewGuid().ToString(),
            ActorType = AuditActorTypes.User,
            TraceId = Guid.NewGuid().ToString("N"),
            CorrelationId = Guid.NewGuid().ToString(),
            ChangedFields = new[] { "Password", "SecretQuestion" },
            // Both Password and SecretQuestion are forbidden; they should be redacted.
            PreviousValues = new Dictionary<string, string>
            {
                { "Password", "old_secret_hash" },
                { "SecretQuestion", "What is your pet's name?" },
                { "Email", "user@example.com" }, // Safe field, should be kept.
            },
            NewValues = new Dictionary<string, string>
            {
                { "Password", "new_secret_hash" },
                { "SecretQuestion", "What is your favorite color?" },
                { "Email", "newemail@example.com" }, // Safe field, should be kept.
            },
        };

        var inboxMessage = CreateInboxMessage();

        await business.ProcessAuditEventAsync(auditEvent, inboxMessage, CancellationToken.None);

        Assert.NotNull(capturedAuditEntry);

        // Verify that PreviousValues contains only Email (Password and SecretQuestion redacted).
        if (!string.IsNullOrEmpty(capturedAuditEntry.PreviousValues))
        {
            var prev = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(capturedAuditEntry.PreviousValues);
            Assert.NotNull(prev);
            Assert.Single(prev); // Only Email
            Assert.True(prev.ContainsKey("Email"));
            Assert.False(prev.ContainsKey("Password"));
            Assert.False(prev.ContainsKey("SecretQuestion"));
        }

        // Verify that NewValues contains only Email.
        if (!string.IsNullOrEmpty(capturedAuditEntry.NewValues))
        {
            var newv = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(capturedAuditEntry.NewValues);
            Assert.NotNull(newv);
            Assert.Single(newv); // Only Email
            Assert.True(newv.ContainsKey("Email"));
            Assert.False(newv.ContainsKey("Password"));
            Assert.False(newv.ContainsKey("SecretQuestion"));
        }
    }

    [Theory]
    [InlineData("Password")]
    [InlineData("pwd")]
    [InlineData("Secret")]
    [InlineData("Token")]
    [InlineData("ApiKey")]
    [InlineData("PrivateKey")]
    [InlineData("ConnectionString")]
    [InlineData("SSN")]
    [InlineData("CreditCard")]
    [InlineData("CVV")]
    public async Task ProcessAuditEventAsync_Event_RedactsForbiddenFieldVariations(string forbiddenField)
    {
        var capturedAuditEntry = default(AuditEntry);
        var mockAuditData = new Mock<IAuditData>();
        mockAuditData
            .Setup(x => x.AppendAuditEntryIdempotentlyAsync(It.IsAny<AuditEntry>(), It.IsAny<InboxMessage>(), It.IsAny<CancellationToken>()))
            .Callback<AuditEntry, InboxMessage, CancellationToken>((entry, _, _) => capturedAuditEntry = entry)
            .Returns(Task.CompletedTask);

        var business = new AuditEventBusiness(mockAuditData.Object);

        var auditEvent = new EntityMutationAudited
        {
            EventId = "event-forbidden",
            Version = EntityMutationAudited.CurrentVersion,
            OccurredAtUtc = new DateTimeOffset(OccurredAtUtc),
            SourceService = AuditSourceServices.Crm,
            EntityType = AuditEntityTypes.ApplicationUser,
            EntityId = Guid.NewGuid(),
            Action = AuditActions.UserCreated,
            ActorId = Guid.NewGuid().ToString(),
            ActorType = AuditActorTypes.User,
            TraceId = Guid.NewGuid().ToString("N"),
            CorrelationId = Guid.NewGuid().ToString(),
            ChangedFields = new[] { forbiddenField, "Name" },
            NewValues = new Dictionary<string, string>
            {
                { forbiddenField, "secret_value" },
                { "Name", "John Doe" },
            },
        };

        var inboxMessage = CreateInboxMessage();

        await business.ProcessAuditEventAsync(auditEvent, inboxMessage, CancellationToken.None);

        // Forbidden field should be redacted; safe field should remain.
        if (capturedAuditEntry.NewValues != null)
        {
            var newv = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(capturedAuditEntry.NewValues);
            Assert.NotNull(newv);
            Assert.Single(newv); // Only Name
            Assert.True(newv.ContainsKey("Name"));
            Assert.False(newv.ContainsKey(forbiddenField));
        }
    }

    // Scenario: Unsupported event version is rejected (ASYNC-005: bad contract)

    [Fact]
    public async Task ProcessAuditEventAsync_UnsupportedVersion_ReturnsValidationFailure()
    {
        var mockAuditData = new Mock<IAuditData>();
        var business = new AuditEventBusiness(mockAuditData.Object);

        var auditEvent = CreateValidEvent();
        auditEvent = auditEvent with { Version = 99 }; // Unsupported version

        var inboxMessage = CreateInboxMessage();

        var result = await business.ProcessAuditEventAsync(auditEvent, inboxMessage, CancellationToken.None);

        Assert.IsType<AuditEventProcessingResult.ValidationFailure>(result);
        var failure = (AuditEventProcessingResult.ValidationFailure)result;
        Assert.Contains("Unsupported event version", failure.Reason);

        // Data layer should not be called.
        mockAuditData.Verify(
            x => x.AppendAuditEntryIdempotentlyAsync(It.IsAny<AuditEntry>(), It.IsAny<InboxMessage>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // Scenario: Malformed event (missing required fields) is rejected

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task ProcessAuditEventAsync_MissingEventId_ReturnsValidationFailure(string eventId)
    {
        var mockAuditData = new Mock<IAuditData>();
        var business = new AuditEventBusiness(mockAuditData.Object);

        var auditEvent = CreateValidEvent(eventId: eventId);
        var inboxMessage = CreateInboxMessage();

        var result = await business.ProcessAuditEventAsync(auditEvent, inboxMessage, CancellationToken.None);

        Assert.IsType<AuditEventProcessingResult.ValidationFailure>(result);
        var failure = (AuditEventProcessingResult.ValidationFailure)result;
        Assert.Contains("EventId is required", failure.Reason);

        mockAuditData.Verify(
            x => x.AppendAuditEntryIdempotentlyAsync(It.IsAny<AuditEntry>(), It.IsAny<InboxMessage>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessAuditEventAsync_EmptyEntityId_ReturnsValidationFailure()
    {
        var mockAuditData = new Mock<IAuditData>();
        var business = new AuditEventBusiness(mockAuditData.Object);

        var auditEvent = CreateValidEvent();
        auditEvent = auditEvent with { EntityId = Guid.Empty };

        var inboxMessage = CreateInboxMessage();

        var result = await business.ProcessAuditEventAsync(auditEvent, inboxMessage, CancellationToken.None);

        Assert.IsType<AuditEventProcessingResult.ValidationFailure>(result);
        var failure = (AuditEventProcessingResult.ValidationFailure)result;
        Assert.Contains("EntityId must be a non-empty GUID", failure.Reason);

        mockAuditData.Verify(
            x => x.AppendAuditEntryIdempotentlyAsync(It.IsAny<AuditEntry>(), It.IsAny<InboxMessage>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // Scenario: Duplicate already-processed returns idempotent result (ASYNC-005)

    [Fact]
    public async Task ProcessAuditEventAsync_InboxAlreadyCompleted_ReturnsDuplicateAlreadyProcessed()
    {
        var mockAuditData = new Mock<IAuditData>();
        var business = new AuditEventBusiness(mockAuditData.Object);

        var auditEvent = CreateValidEvent();
        var inboxMessage = CreateInboxMessage();
        inboxMessage.Status = InboxMessageStatus.Completed; // Already completed (duplicate)

        var result = await business.ProcessAuditEventAsync(auditEvent, inboxMessage, CancellationToken.None);

        Assert.IsType<AuditEventProcessingResult.DuplicateAlreadyProcessed>(result);
        var duplicate = (AuditEventProcessingResult.DuplicateAlreadyProcessed)result;
        Assert.Equal(auditEvent.EventId, duplicate.EventId);

        // Data layer should not be called for duplicate.
        mockAuditData.Verify(
            x => x.AppendAuditEntryIdempotentlyAsync(It.IsAny<AuditEntry>(), It.IsAny<InboxMessage>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // Scenario: Transient failure (e.g., database error) returns TransientFailure

    [Fact]
    public async Task ProcessAuditEventAsync_DataLayerThrowsException_ReturnsTransientFailure()
    {
        var mockAuditData = new Mock<IAuditData>();
        mockAuditData
            .Setup(x => x.AppendAuditEntryIdempotentlyAsync(It.IsAny<AuditEntry>(), It.IsAny<InboxMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database connection timeout"));

        var business = new AuditEventBusiness(mockAuditData.Object);

        var auditEvent = CreateValidEvent();
        var inboxMessage = CreateInboxMessage();

        var result = await business.ProcessAuditEventAsync(auditEvent, inboxMessage, CancellationToken.None);

        Assert.IsType<AuditEventProcessingResult.TransientFailure>(result);
        var failure = (AuditEventProcessingResult.TransientFailure)result;
        Assert.Contains("InvalidOperationException", failure.ErrorMessage);
        Assert.Contains("Database connection timeout", failure.ErrorMessage);
        Assert.Equal(auditEvent.EventId, failure.EventId);
    }

    // Scenario: ActionCategory is correctly determined from Action

    [Theory]
    [InlineData(AuditActions.Created, "WRITE")]
    [InlineData(AuditActions.Updated, "WRITE")]
    [InlineData(AuditActions.StatusChanged, "TRANSITION")]
    [InlineData(AuditActions.Assigned, "ASSIGN")]
    [InlineData(AuditActions.Completed, "LIFECYCLE")]
    [InlineData(AuditActions.Archived, "ARCHIVE")]
    [InlineData(AuditActions.LoggedIn, "AUTH")]
    [InlineData(AuditActions.FailedLogin, "AUTH_FAILURE")]
    [InlineData(AuditActions.PasswordChanged, "PASSWORD")]
    public async Task ProcessAuditEventAsync_Event_CorrectlyDeterminesActionCategory(string action, string expectedCategory)
    {
        var capturedAuditEntry = default(AuditEntry);
        var mockAuditData = new Mock<IAuditData>();
        mockAuditData
            .Setup(x => x.AppendAuditEntryIdempotentlyAsync(It.IsAny<AuditEntry>(), It.IsAny<InboxMessage>(), It.IsAny<CancellationToken>()))
            .Callback<AuditEntry, InboxMessage, CancellationToken>((entry, _, _) => capturedAuditEntry = entry)
            .Returns(Task.CompletedTask);

        var business = new AuditEventBusiness(mockAuditData.Object);

        var auditEvent = CreateValidEvent(action: action);
        var inboxMessage = CreateInboxMessage();

        await business.ProcessAuditEventAsync(auditEvent, inboxMessage, CancellationToken.None);

        Assert.NotNull(capturedAuditEntry);
        Assert.Equal(expectedCategory, capturedAuditEntry.ActionCategory);
    }

    // Scenario: Actor information is correctly extracted and mapped

    [Fact]
    public async Task ProcessAuditEventAsync_UserActor_CorrectlyExtractsActorUserId()
    {
        var capturedAuditEntry = default(AuditEntry);
        var mockAuditData = new Mock<IAuditData>();
        mockAuditData
            .Setup(x => x.AppendAuditEntryIdempotentlyAsync(It.IsAny<AuditEntry>(), It.IsAny<InboxMessage>(), It.IsAny<CancellationToken>()))
            .Callback<AuditEntry, InboxMessage, CancellationToken>((entry, _, _) => capturedAuditEntry = entry)
            .Returns(Task.CompletedTask);

        var business = new AuditEventBusiness(mockAuditData.Object);

        var userId = Guid.NewGuid();
        var auditEvent = new EntityMutationAudited
        {
            EventId = "event-actor",
            Version = EntityMutationAudited.CurrentVersion,
            OccurredAtUtc = new DateTimeOffset(OccurredAtUtc),
            SourceService = AuditSourceServices.Crm,
            EntityType = AuditEntityTypes.Client,
            EntityId = Guid.NewGuid(),
            Action = AuditActions.Created,
            ActorId = userId.ToString(),
            ActorType = AuditActorTypes.User,
            TraceId = Guid.NewGuid().ToString("N"),
            CorrelationId = Guid.NewGuid().ToString(),
            ChangedFields = Array.Empty<string>(),
        };

        var inboxMessage = CreateInboxMessage();

        await business.ProcessAuditEventAsync(auditEvent, inboxMessage, CancellationToken.None);

        Assert.NotNull(capturedAuditEntry);
        Assert.Equal(userId, capturedAuditEntry.ActorUserId);
    }

    [Fact]
    public async Task ProcessAuditEventAsync_SystemActor_ActorUserIdIsNull()
    {
        var capturedAuditEntry = default(AuditEntry);
        var mockAuditData = new Mock<IAuditData>();
        mockAuditData
            .Setup(x => x.AppendAuditEntryIdempotentlyAsync(It.IsAny<AuditEntry>(), It.IsAny<InboxMessage>(), It.IsAny<CancellationToken>()))
            .Callback<AuditEntry, InboxMessage, CancellationToken>((entry, _, _) => capturedAuditEntry = entry)
            .Returns(Task.CompletedTask);

        var business = new AuditEventBusiness(mockAuditData.Object);

        var auditEvent = CreateValidEvent();
        auditEvent = auditEvent with { ActorType = AuditActorTypes.System, ActorId = null };

        var inboxMessage = CreateInboxMessage();

        await business.ProcessAuditEventAsync(auditEvent, inboxMessage, CancellationToken.None);

        Assert.NotNull(capturedAuditEntry);
        Assert.Null(capturedAuditEntry.ActorUserId);
        Assert.Equal("System", capturedAuditEntry.ActorDisplayName);
    }
}

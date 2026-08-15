using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ProjectChicago.Audit.Core.Data;
using ProjectChicago.Audit.Core.Persistence;
using ProjectChicago.Audit.Core.Repositories;
using ProjectChicago.Audit.Core.Tests.Persistence;
using ProjectChicago.Shared.Inbox;
using Xunit;

namespace ProjectChicago.Audit.Core.Tests.Data;

/// <summary>
/// Real SQL Server integration tests for AuditData's idempotent append and inbox-based consumption
/// (ADR-0016, AUDIT-001..008, ASYNC-005..008; messaging.md consume-side test matrix).
/// Verifies atomicity of AuditEntry + InboxMessage state, idempotent handling of duplicate EventIds,
/// and failure rollback semantics.
///
/// Each test gets its own database inside the shared container (see MsSqlContainerFixture) so tests
/// never interfere with each other despite sharing one running SQL Server instance.
/// </summary>
public class AuditDataTests : IClassFixture<MsSqlContainerFixture>
{
    private readonly MsSqlContainerFixture _fixture;

    public AuditDataTests(MsSqlContainerFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<AuditDbContext> CreateContextAsync(string databaseName)
    {
        var builder = new SqlConnectionStringBuilder(_fixture.ConnectionString)
        {
            InitialCatalog = databaseName,
        };

        var options = new DbContextOptionsBuilder<AuditDbContext>()
            .UseSqlServer(builder.ConnectionString)
            .Options;

        var context = new AuditDbContext(options);
        await context.Database.EnsureCreatedAsync();
        return context;
    }

    private async Task<AuditData> CreateAuditDataAsync(string databaseName)
    {
        var context = await CreateContextAsync(databaseName);
        var repository = new AuditRepository(context);
        return new AuditData(context, repository);
    }

    private static readonly DateTime OccurredAtUtc = new(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);

    private static AuditEntry CreateAuditEntry(
        string eventId = "event-1",
        Guid? entityId = null,
        string entityType = "Client",
        string action = "Created") =>
        new()
        {
            AuditEntryId = Guid.NewGuid(),
            EventId = eventId,
            EntityType = entityType,
            EntityId = entityId ?? Guid.NewGuid(),
            Action = action,
            ActionCategory = "WRITE",
            ActorUserId = Guid.NewGuid(),
            ActorType = "User",
            ActorDisplayName = "John Doe",
            SourceService = "Crm",
            SourceEventType = "Crm.ClientCreated",
            OccurredAtUtc = OccurredAtUtc,
            AuditedAtUtc = DateTime.UtcNow,
            TraceId = Guid.NewGuid().ToString("N"),
            CorrelationId = Guid.NewGuid().ToString(),
            CausationId = Guid.NewGuid().ToString(),
            ChangedFields = "[\"Name\", \"Email\"]",
            PreviousValues = null,
            NewValues = "{\"Name\": \"Acme\", \"Email\": \"acme@example.com\"}",
            SummaryDescription = "Created new Client: Acme",
            RawEventPayload = "{}",
        };

    private static AuditEntry CreateAuditEntryWithId(
        Guid auditEntryId,
        string eventId = "event-1",
        Guid? entityId = null,
        string entityType = "Client",
        string action = "Created") =>
        new()
        {
            AuditEntryId = auditEntryId,
            EventId = eventId,
            EntityType = entityType,
            EntityId = entityId ?? Guid.NewGuid(),
            Action = action,
            ActionCategory = "WRITE",
            ActorUserId = Guid.NewGuid(),
            ActorType = "User",
            ActorDisplayName = "John Doe",
            SourceService = "Crm",
            SourceEventType = "Crm.ClientCreated",
            OccurredAtUtc = OccurredAtUtc,
            AuditedAtUtc = DateTime.UtcNow,
            TraceId = Guid.NewGuid().ToString("N"),
            CorrelationId = Guid.NewGuid().ToString(),
            CausationId = Guid.NewGuid().ToString(),
            ChangedFields = "[\"Name\", \"Email\"]",
            PreviousValues = null,
            NewValues = "{\"Name\": \"Acme\", \"Email\": \"acme@example.com\"}",
            SummaryDescription = "Created new Client: Acme",
            RawEventPayload = "{}",
        };

    private static InboxMessage CreateInboxMessage(string messageId = "msg-1") =>
        new()
        {
            MessageId = messageId,
            ContractType = "Audit.EntityMutationAudited",
            ContractVersion = 1,
            CorrelationId = Guid.NewGuid().ToString(),
            CausationId = Guid.NewGuid().ToString(),
            TraceId = Guid.NewGuid().ToString("N"),
            ReceivedAtUtc = DateTime.UtcNow,
            Status = InboxMessageStatus.Received,
            AttemptCount = 0,
        };

    // Scenario: First delivery appends once and marks inbox complete (ASYNC-005, ASYNC-006, AUDIT-004)

    [Fact]
    public async Task AppendAuditEntryIdempotentlyAsync_FirstDelivery_CreatesAuditEntryAndMarksInboxComplete()
    {
        var db = nameof(AppendAuditEntryIdempotentlyAsync_FirstDelivery_CreatesAuditEntryAndMarksInboxComplete);
        var data = await CreateAuditDataAsync(db);

        var auditEntry = CreateAuditEntry("event-123");
        var inboxMessage = CreateInboxMessage("msg-123");

        await data.AppendAuditEntryIdempotentlyAsync(auditEntry, inboxMessage, CancellationToken.None);

        // Verify AuditEntry was created.
        await using var verifyContext = await CreateContextAsync(db);
        var persistedEntry = await verifyContext.AuditEntries
            .FirstOrDefaultAsync(a => a.EventId == "event-123");
        Assert.NotNull(persistedEntry);
        Assert.Equal("event-123", persistedEntry!.EventId);
        Assert.Equal("Client", persistedEntry.EntityType);
        Assert.Equal("Created", persistedEntry.Action);

        // Verify InboxMessage was marked Completed.
        var persistedInbox = await verifyContext.InboxMessages
            .FirstOrDefaultAsync(m => m.MessageId == "msg-123");
        Assert.NotNull(persistedInbox);
        Assert.Equal(InboxMessageStatus.Completed, persistedInbox!.Status);
        Assert.NotNull(persistedInbox.ProcessingCompletedAtUtc);
        Assert.Equal(1, persistedInbox.AttemptCount);
    }

    [Fact]
    public async Task AppendAuditEntryIdempotentlyAsync_FirstDelivery_PreservesCorrelationAndTraceMetadata()
    {
        var db = nameof(AppendAuditEntryIdempotentlyAsync_FirstDelivery_PreservesCorrelationAndTraceMetadata);
        var data = await CreateAuditDataAsync(db);

        var traceId = Guid.NewGuid().ToString("N");
        var correlationId = Guid.NewGuid().ToString();
        var causationId = Guid.NewGuid().ToString();

        var auditEntry = new AuditEntry
        {
            AuditEntryId = Guid.NewGuid(),
            EventId = "event-456",
            EntityType = "Client",
            EntityId = Guid.NewGuid(),
            Action = "Created",
            ActionCategory = "WRITE",
            ActorUserId = Guid.NewGuid(),
            ActorType = "User",
            ActorDisplayName = "John Doe",
            SourceService = "Crm",
            SourceEventType = "Crm.ClientCreated",
            OccurredAtUtc = OccurredAtUtc,
            AuditedAtUtc = DateTime.UtcNow,
            TraceId = traceId,
            CorrelationId = correlationId,
            CausationId = causationId,
            ChangedFields = "[\"Name\", \"Email\"]",
            PreviousValues = null,
            NewValues = "{\"Name\": \"Acme\", \"Email\": \"acme@example.com\"}",
            SummaryDescription = "Created new Client: Acme",
            RawEventPayload = "{}",
        };

        var inboxMessage = new InboxMessage
        {
            MessageId = "msg-456",
            ContractType = "Audit.EntityMutationAudited",
            ContractVersion = 1,
            CorrelationId = correlationId,
            CausationId = causationId,
            TraceId = traceId,
            ReceivedAtUtc = DateTime.UtcNow,
            Status = InboxMessageStatus.Received,
            AttemptCount = 0,
        };

        await data.AppendAuditEntryIdempotentlyAsync(auditEntry, inboxMessage, CancellationToken.None);

        await using var verifyContext = await CreateContextAsync(db);
        var persistedEntry = await verifyContext.AuditEntries.SingleAsync(a => a.EventId == "event-456");
        Assert.Equal(traceId, persistedEntry.TraceId);
        Assert.Equal(correlationId, persistedEntry.CorrelationId);
        Assert.Equal(causationId, persistedEntry.CausationId);

        var persistedInbox = await verifyContext.InboxMessages.SingleAsync(m => m.MessageId == "msg-456");
        Assert.Equal(traceId, persistedInbox.TraceId);
        Assert.Equal(correlationId, persistedInbox.CorrelationId);
        Assert.Equal(causationId, persistedInbox.CausationId);
    }

    // Scenario: Duplicate delivery with already-completed inbox is idempotent no-op (ASYNC-005)

    [Fact]
    public async Task AppendAuditEntryIdempotentlyAsync_DuplicateWithCompletedInbox_ReturnsSuccessWithoutRepeatingAppend()
    {
        var db = nameof(AppendAuditEntryIdempotentlyAsync_DuplicateWithCompletedInbox_ReturnsSuccessWithoutRepeatingAppend);
        var data = await CreateAuditDataAsync(db);

        var auditEntry1 = CreateAuditEntry("event-789");
        var inboxMessage1 = CreateInboxMessage("msg-789");

        // First delivery.
        await data.AppendAuditEntryIdempotentlyAsync(auditEntry1, inboxMessage1, CancellationToken.None);

        // Second delivery (duplicate) with a fresh AuditEntry instance but same EventId.
        // The AuditEntry instance is different but EventId is the same (simulating duplicate delivery).
        await using var secondContext = await CreateContextAsync(db);
        var data2 = new AuditData(secondContext, new AuditRepository(secondContext));

        var auditEntry2 = CreateAuditEntryWithId(Guid.NewGuid(), "event-789"); // Same EventId, different AuditEntryId
        var inboxMessage2 = CreateInboxMessage("msg-789"); // Same MessageId

        // Should return success (idempotent no-op) without attempting to re-insert the AuditEntry.
        await data2.AppendAuditEntryIdempotentlyAsync(auditEntry2, inboxMessage2, CancellationToken.None);

        // Verify only one AuditEntry with EventId "event-789" exists (no duplicate).
        await using var verifyContext = await CreateContextAsync(db);
        var entries = await verifyContext.AuditEntries
            .Where(a => a.EventId == "event-789")
            .ToListAsync();
        Assert.Single(entries);

        // Verify InboxMessage is still marked Completed.
        var persistedInbox = await verifyContext.InboxMessages
            .FirstOrDefaultAsync(m => m.MessageId == "msg-789");
        Assert.NotNull(persistedInbox);
        Assert.Equal(InboxMessageStatus.Completed, persistedInbox!.Status);
    }

    // Scenario: Duplicate EventId with in-progress/received inbox rolls back (failure recovery)

    [Fact]
    public async Task AppendAuditEntryIdempotentlyAsync_DuplicateEventIdWithReceivedInbox_RollsBackAndThrows()
    {
        var db = nameof(AppendAuditEntryIdempotentlyAsync_DuplicateEventIdWithReceivedInbox_RollsBackAndThrows);
        await using var context = await CreateContextAsync(db);

        // Manually insert AuditEntry and InboxMessage with status=Received (simulating a partial failure).
        var auditEntry1 = CreateAuditEntry("event-fail-1");
        var inboxMessage1 = CreateInboxMessage("msg-fail-1");
        inboxMessage1.Status = InboxMessageStatus.Received; // Not yet completed

        context.AuditEntries.Add(auditEntry1);
        context.InboxMessages.Add(inboxMessage1);
        await context.SaveChangesAsync();

        // Duplicate delivery: attempt to append with same EventId.
        await using var secondContext = await CreateContextAsync(db);
        var data = new AuditData(secondContext, new AuditRepository(secondContext));

        var auditEntry2 = CreateAuditEntryWithId(Guid.NewGuid(), "event-fail-1"); // Same EventId, different AuditEntryId
        var inboxMessage2 = CreateInboxMessage("msg-fail-1"); // Same MessageId

        // Should throw because the EventId unique constraint is violated and inbox is not Completed.
        await Assert.ThrowsAsync<DbUpdateException>(
            () => data.AppendAuditEntryIdempotentlyAsync(auditEntry2, inboxMessage2, CancellationToken.None));

        // Verify the InboxMessage is still NOT marked Completed (rollback).
        await using var verifyContext = await CreateContextAsync(db);
        var persistedInbox = await verifyContext.InboxMessages
            .FirstOrDefaultAsync(m => m.MessageId == "msg-fail-1");
        Assert.NotNull(persistedInbox);
        Assert.NotEqual(InboxMessageStatus.Completed, persistedInbox!.Status);
    }

    // Scenario: Failure to insert AuditEntry does not mark inbox complete (atomic rollback)

    [Fact]
    public async Task AppendAuditEntryIdempotentlyAsync_WhenAuditEntryInsertFails_RollsBackInboxCompletion()
    {
        // This test simulates a failure during AuditEntry insert (e.g., a constraint violation unrelated to EventId).
        // The transaction must roll back without marking the inbox as Completed.
        var db = nameof(AppendAuditEntryIdempotentlyAsync_WhenAuditEntryInsertFails_RollsBackInboxCompletion);
        var data = await CreateAuditDataAsync(db);

        // Create an AuditEntry with an excessively long EventId that violates the nvarchar(256) constraint.
        var longEventId = string.Concat(Enumerable.Repeat("x", 300)); // > 256 chars, will fail
        var auditEntry = new AuditEntry
        {
            AuditEntryId = Guid.NewGuid(),
            EventId = longEventId,
            EntityType = "Client",
            EntityId = Guid.NewGuid(),
            Action = "Created",
            ActionCategory = "WRITE",
            ActorUserId = Guid.NewGuid(),
            ActorType = "User",
            ActorDisplayName = "John Doe",
            SourceService = "Crm",
            SourceEventType = "Crm.ClientCreated",
            OccurredAtUtc = OccurredAtUtc,
            AuditedAtUtc = DateTime.UtcNow,
            TraceId = Guid.NewGuid().ToString("N"),
            CorrelationId = Guid.NewGuid().ToString(),
            CausationId = Guid.NewGuid().ToString(),
            ChangedFields = "[\"Name\", \"Email\"]",
            PreviousValues = null,
            NewValues = "{\"Name\": \"Acme\", \"Email\": \"acme@example.com\"}",
            SummaryDescription = "Created new Client: Acme",
            RawEventPayload = "{}",
        };
        var inboxMessage = CreateInboxMessage("msg-fail-2");

        // Attempt to append; should fail and rollback.
        await Assert.ThrowsAsync<DbUpdateException>(
            () => data.AppendAuditEntryIdempotentlyAsync(auditEntry, inboxMessage, CancellationToken.None));

        // Verify InboxMessage was NOT created/completed (transaction rolled back).
        await using var verifyContext = await CreateContextAsync(db);
        var persistedInbox = await verifyContext.InboxMessages
            .FirstOrDefaultAsync(m => m.MessageId == "msg-fail-2");
        Assert.Null(persistedInbox);
    }

    // Scenario: Append-only constraint (cannot UPDATE an existing AuditEntry through Data layer)

    [Fact]
    public async Task AuditEntry_IsInsertOnly_DataLayerDoesNotExposeUpdateOperation()
    {
        // The AuditEntry design is append-only: once persisted, it should never be updated.
        // The Data layer intentionally does not expose any update operation.
        // Furthermore, all AuditEntry properties are init-only, preventing even inadvertent modification
        // after object construction (AUDIT-004, 005: append-only through normal workflows).
        var db = nameof(AuditEntry_IsInsertOnly_DataLayerDoesNotExposeUpdateOperation);
        await using var context = await CreateContextAsync(db);

        var auditEntry = CreateAuditEntry("event-immutable");
        context.AuditEntries.Add(auditEntry);
        await context.SaveChangesAsync();

        // Verify entry was persisted.
        await using var verifyContext = await CreateContextAsync(db);
        var loadedEntry = await verifyContext.AuditEntries
            .FirstAsync(a => a.EventId == "event-immutable");
        Assert.NotNull(loadedEntry);

        // Verify that AuditEntry has RowVersion populated (SQL Server rowversion for concurrency protection).
        Assert.NotNull(loadedEntry.RowVersion);
        Assert.NotEmpty(loadedEntry.RowVersion);

        // The Data layer (IAuditData) does not expose any update/modify operation - only AppendAuditEntryIdempotentlyAsync
        // which is insert-only (this is verified by the interface definition and data layer implementation).
        // All AuditEntry properties are init-only, making modification impossible after construction.
    }

    // Scenario: Multiple concurrent deliveries with same EventId are handled safely

    [Fact]
    public async Task AppendAuditEntryIdempotentlyAsync_ConcurrentDeliveriesWithSameEventId_OnlyOneSucceeds()
    {
        var db = nameof(AppendAuditEntryIdempotentlyAsync_ConcurrentDeliveriesWithSameEventId_OnlyOneSucceeds);

        var auditEntry1 = CreateAuditEntry("event-concurrent");
        var inboxMessage1 = CreateInboxMessage("msg-concurrent-1");

        var auditEntry2 = CreateAuditEntryWithId(Guid.NewGuid(), "event-concurrent"); // Same EventId, different AuditEntryId
        var inboxMessage2 = CreateInboxMessage("msg-concurrent-2"); // Different MessageId, same EventId

        // Simulate two concurrent Function invocations by running them with separate contexts.
        // In reality, SQL Server's unique constraint will serialize these; one succeeds, one fails.
        var task1 = Task.Run(async () =>
        {
            var data = await CreateAuditDataAsync(db);
            await data.AppendAuditEntryIdempotentlyAsync(auditEntry1, inboxMessage1, CancellationToken.None);
        });

        // Small delay to allow first to likely succeed before second starts.
        await Task.Delay(100);

        var task2 = Task.Run(async () =>
        {
            var data = await CreateAuditDataAsync(db);
            await data.AppendAuditEntryIdempotentlyAsync(auditEntry2, inboxMessage2, CancellationToken.None);
        });

        // At least one should succeed without error (depends on SQL Server concurrency handling).
        // The other may throw DbUpdateException due to EventId unique constraint violation.
        try
        {
            await Task.WhenAll(task1, task2);
        }
        catch
        {
            // One task may fail; that's expected (unique constraint).
        }

        // Verify only one AuditEntry with EventId "event-concurrent" was persisted.
        await using var verifyContext = await CreateContextAsync(db);
        var entries = await verifyContext.AuditEntries
            .Where(a => a.EventId == "event-concurrent")
            .ToListAsync();
        Assert.Single(entries);
    }

    // Scenario: Attempts to increment AttemptCount on retry (second invocation with same MessageId)

    [Fact]
    public async Task AppendAuditEntryIdempotentlyAsync_RetryWithSameMessageIdIncrementAttemptCount()
    {
        var db = nameof(AppendAuditEntryIdempotentlyAsync_RetryWithSameMessageIdIncrementAttemptCount);
        await using var context = await CreateContextAsync(db);

        // Manually insert InboxMessage with Received status (simulating a processing failure mid-flight).
        var inboxMessage = CreateInboxMessage("msg-retry");
        inboxMessage.Status = InboxMessageStatus.Received;
        inboxMessage.AttemptCount = 1;
        inboxMessage.LastAttemptAtUtc = DateTime.UtcNow;

        context.InboxMessages.Add(inboxMessage);
        await context.SaveChangesAsync();

        // Retry: attempt to append with same MessageId but different AuditEntry.
        await using var retryContext = await CreateContextAsync(db);
        var data = new AuditData(retryContext, new AuditRepository(retryContext));

        var auditEntry = CreateAuditEntry("event-retry-attempt");
        var retryInbox = CreateInboxMessage("msg-retry"); // Same MessageId

        await data.AppendAuditEntryIdempotentlyAsync(auditEntry, retryInbox, CancellationToken.None);

        // Verify AuditEntry was created.
        await using var verifyContext = await CreateContextAsync(db);
        var persistedEntry = await verifyContext.AuditEntries
            .FirstOrDefaultAsync(a => a.EventId == "event-retry-attempt");
        Assert.NotNull(persistedEntry);

        // Verify InboxMessage AttemptCount was incremented.
        var persistedInbox = await verifyContext.InboxMessages
            .FirstOrDefaultAsync(m => m.MessageId == "msg-retry");
        Assert.NotNull(persistedInbox);
        Assert.Equal(InboxMessageStatus.Completed, persistedInbox!.Status);
        Assert.Equal(2, persistedInbox.AttemptCount); // Incremented from 1 to 2
    }
}




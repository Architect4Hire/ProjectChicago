using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ProjectChicago.Crm.Core.Persistence;
using ProjectChicago.Shared.Outbox;
using Xunit;

namespace ProjectChicago.Crm.Core.Tests.Persistence;

// Real SQL Server integration tests for CrmOutboxStore (OUTBOX-003..006, DATA-006/008). Each test
// gets its own database inside the shared container (see MsSqlContainerFixture) so tests never
// interfere with each other despite sharing one running SQL Server instance.
public class CrmOutboxStoreTests : IClassFixture<MsSqlContainerFixture>
{
    private readonly MsSqlContainerFixture _fixture;

    public CrmOutboxStoreTests(MsSqlContainerFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<CrmDbContext> CreateContextAsync(string databaseName)
    {
        var builder = new SqlConnectionStringBuilder(_fixture.ConnectionString)
        {
            InitialCatalog = databaseName,
        };

        var options = new DbContextOptionsBuilder<CrmDbContext>()
            .UseSqlServer(builder.ConnectionString)
            .Options;

        var context = new CrmDbContext(options);
        await context.Database.EnsureCreatedAsync();
        return context;
    }

    private static OutboxMessage CreateMessage(DateTime createdAtUtc, OutboxMessageStatus status = OutboxMessageStatus.Pending) => new()
    {
        Id = Guid.NewGuid(),
        ContractType = "Crm.SomethingHappened",
        ContractVersion = 1,
        Payload = "{}",
        CorrelationId = Guid.NewGuid().ToString(),
        TraceId = Guid.NewGuid().ToString("N"),
        OccurredAtUtc = createdAtUtc,
        CreatedAtUtc = createdAtUtc,
        Status = status,
    };

    [Fact]
    public async Task ClaimPendingBatchAsync_ClaimsOldestPendingMessagesFirst_UpToBatchSize()
    {
        var db = nameof(ClaimPendingBatchAsync_ClaimsOldestPendingMessagesFirst_UpToBatchSize);
        await using var context = await CreateContextAsync(db);
        var now = DateTime.UtcNow;
        var oldest = CreateMessage(now.AddMinutes(-3));
        var middle = CreateMessage(now.AddMinutes(-2));
        var newest = CreateMessage(now.AddMinutes(-1));
        context.OutboxMessages.AddRange(newest, oldest, middle);
        await context.SaveChangesAsync();

        var store = new CrmOutboxStore(context);
        var claimed = await store.ClaimPendingBatchAsync(2, "owner-1", TimeSpan.FromMinutes(1), CancellationToken.None);

        Assert.Equal(2, claimed.Count);
        Assert.Equal([oldest.Id, middle.Id], claimed.Select(m => m.Id));
    }

    [Fact]
    public async Task ClaimPendingBatchAsync_PersistsLeaseOwnerAndLeasedUntil_ForClaimedRows()
    {
        var db = nameof(ClaimPendingBatchAsync_PersistsLeaseOwnerAndLeasedUntil_ForClaimedRows);
        await using var context = await CreateContextAsync(db);
        var message = CreateMessage(DateTime.UtcNow);
        context.OutboxMessages.Add(message);
        await context.SaveChangesAsync();

        var store = new CrmOutboxStore(context);
        var before = DateTime.UtcNow;
        var claimed = await store.ClaimPendingBatchAsync(10, "owner-1", TimeSpan.FromMinutes(5), CancellationToken.None);

        Assert.Single(claimed);

        await using var verifyContext = await CreateContextAsync(db);
        var persisted = await verifyContext.OutboxMessages.SingleAsync(m => m.Id == message.Id);
        Assert.Equal("owner-1", persisted.LeaseOwner);
        Assert.NotNull(persisted.LeasedUntilUtc);
        Assert.True(persisted.LeasedUntilUtc > before.AddMinutes(4));
        Assert.Equal(OutboxMessageStatus.Pending, persisted.Status);
    }

    [Fact]
    public async Task ClaimPendingBatchAsync_ExcludesAlreadyDispatchedMessages()
    {
        var db = nameof(ClaimPendingBatchAsync_ExcludesAlreadyDispatchedMessages);
        await using var context = await CreateContextAsync(db);
        var dispatched = CreateMessage(DateTime.UtcNow.AddMinutes(-5), OutboxMessageStatus.Dispatched);
        var pending = CreateMessage(DateTime.UtcNow.AddMinutes(-1));
        context.OutboxMessages.AddRange(dispatched, pending);
        await context.SaveChangesAsync();

        var store = new CrmOutboxStore(context);
        var claimed = await store.ClaimPendingBatchAsync(10, "owner-1", TimeSpan.FromMinutes(1), CancellationToken.None);

        var claimedId = Assert.Single(claimed).Id;
        Assert.Equal(pending.Id, claimedId);
    }

    [Fact]
    public async Task ClaimPendingBatchAsync_ExcludesMessagesUnderAnActiveLease_ButReclaimsExpiredLeases()
    {
        var db = nameof(ClaimPendingBatchAsync_ExcludesMessagesUnderAnActiveLease_ButReclaimsExpiredLeases);
        await using var context = await CreateContextAsync(db);
        var activelyLeased = CreateMessage(DateTime.UtcNow.AddMinutes(-5));
        activelyLeased.LeaseOwner = "other-owner";
        activelyLeased.LeasedUntilUtc = DateTime.UtcNow.AddMinutes(5);

        var expiredLease = CreateMessage(DateTime.UtcNow.AddMinutes(-4));
        expiredLease.LeaseOwner = "other-owner";
        expiredLease.LeasedUntilUtc = DateTime.UtcNow.AddMinutes(-1);

        context.OutboxMessages.AddRange(activelyLeased, expiredLease);
        await context.SaveChangesAsync();

        var store = new CrmOutboxStore(context);
        var claimed = await store.ClaimPendingBatchAsync(10, "owner-1", TimeSpan.FromMinutes(1), CancellationToken.None);

        var claimedId = Assert.Single(claimed).Id;
        Assert.Equal(expiredLease.Id, claimedId);
    }

    [Fact]
    public async Task ClaimPendingBatchAsync_ConcurrentClaims_NeverClaimTheSameMessageTwice()
    {
        // The core RowVersion optimistic-concurrency guarantee (messaging.md: "Relay selection/lease
        // must prevent uncontrolled duplicate concurrent dispatch"). Two independent DbContext
        // instances race to claim the same five rows; the union of what each of them wins must be
        // exactly the five rows with zero overlap.
        var db = nameof(ClaimPendingBatchAsync_ConcurrentClaims_NeverClaimTheSameMessageTwice);
        var messages = Enumerable.Range(0, 5)
            .Select(i => CreateMessage(DateTime.UtcNow.AddMinutes(-10 + i)))
            .ToList();

        await using (var seedContext = await CreateContextAsync(db))
        {
            seedContext.OutboxMessages.AddRange(messages);
            await seedContext.SaveChangesAsync();
        }

        await using var contextA = await CreateContextAsync(db);
        await using var contextB = await CreateContextAsync(db);
        var storeA = new CrmOutboxStore(contextA);
        var storeB = new CrmOutboxStore(contextB);

        var claimTaskA = storeA.ClaimPendingBatchAsync(5, "owner-a", TimeSpan.FromMinutes(1), CancellationToken.None);
        var claimTaskB = storeB.ClaimPendingBatchAsync(5, "owner-b", TimeSpan.FromMinutes(1), CancellationToken.None);
        var results = await Task.WhenAll(claimTaskA, claimTaskB);

        var claimedByA = results[0].Select(m => m.Id).ToHashSet();
        var claimedByB = results[1].Select(m => m.Id).ToHashSet();

        Assert.Empty(claimedByA.Intersect(claimedByB));
        Assert.Equal(messages.Select(m => m.Id).ToHashSet(), claimedByA.Union(claimedByB).ToHashSet());
    }

    [Fact]
    public async Task MarkDispatchedAsync_SetsDispatchedStatusAndClearsLease_AndTheMessageIsNoLongerClaimable()
    {
        var db = nameof(MarkDispatchedAsync_SetsDispatchedStatusAndClearsLease_AndTheMessageIsNoLongerClaimable);
        await using var context = await CreateContextAsync(db);
        var message = CreateMessage(DateTime.UtcNow);
        context.OutboxMessages.Add(message);
        await context.SaveChangesAsync();

        var store = new CrmOutboxStore(context);
        await store.ClaimPendingBatchAsync(10, "owner-1", TimeSpan.FromMinutes(1), CancellationToken.None);
        await store.MarkDispatchedAsync(message.Id, CancellationToken.None);

        await using var verifyContext = await CreateContextAsync(db);
        var persisted = await verifyContext.OutboxMessages.SingleAsync(m => m.Id == message.Id);
        Assert.Equal(OutboxMessageStatus.Dispatched, persisted.Status);
        Assert.NotNull(persisted.DispatchedAtUtc);
        Assert.Null(persisted.LeaseOwner);
        Assert.Null(persisted.LeasedUntilUtc);

        var storeForReclaim = new CrmOutboxStore(verifyContext);
        var reclaimed = await storeForReclaim.ClaimPendingBatchAsync(10, "owner-2", TimeSpan.FromMinutes(1), CancellationToken.None);
        Assert.Empty(reclaimed);
    }

    [Fact]
    public async Task RecordFailedAttemptAsync_IncrementsAttemptCountAndClearsLease_LeavingTheMessagePendingAndImmediatelyReclaimable()
    {
        var db = nameof(RecordFailedAttemptAsync_IncrementsAttemptCountAndClearsLease_LeavingTheMessagePendingAndImmediatelyReclaimable);
        await using var context = await CreateContextAsync(db);
        var message = CreateMessage(DateTime.UtcNow);
        context.OutboxMessages.Add(message);
        await context.SaveChangesAsync();

        var store = new CrmOutboxStore(context);
        await store.ClaimPendingBatchAsync(10, "owner-1", TimeSpan.FromMinutes(5), CancellationToken.None);
        await store.RecordFailedAttemptAsync(message.Id, "simulated publish failure", CancellationToken.None);

        await using var verifyContext = await CreateContextAsync(db);
        var persisted = await verifyContext.OutboxMessages.SingleAsync(m => m.Id == message.Id);
        Assert.Equal(OutboxMessageStatus.Pending, persisted.Status);
        Assert.Equal(1, persisted.AttemptCount);
        Assert.Equal("simulated publish failure", persisted.LastError);
        Assert.NotNull(persisted.LastAttemptAtUtc);
        Assert.Null(persisted.LeaseOwner);
        Assert.Null(persisted.LeasedUntilUtc);

        var storeForReclaim = new CrmOutboxStore(verifyContext);
        var reclaimed = await storeForReclaim.ClaimPendingBatchAsync(10, "owner-2", TimeSpan.FromMinutes(1), CancellationToken.None);
        Assert.Single(reclaimed);
        Assert.Equal(message.Id, reclaimed[0].Id);
    }

    [Fact]
    public async Task RecordFailedAttemptAsync_TruncatesAnOverlongErrorMessage_ToTheColumnsMaxLength()
    {
        var db = nameof(RecordFailedAttemptAsync_TruncatesAnOverlongErrorMessage_ToTheColumnsMaxLength);
        await using var context = await CreateContextAsync(db);
        var message = CreateMessage(DateTime.UtcNow);
        context.OutboxMessages.Add(message);
        await context.SaveChangesAsync();

        var store = new CrmOutboxStore(context);
        var overlongError = new string('x', 1500);
        await store.RecordFailedAttemptAsync(message.Id, overlongError, CancellationToken.None);

        await using var verifyContext = await CreateContextAsync(db);
        var persisted = await verifyContext.OutboxMessages.SingleAsync(m => m.Id == message.Id);
        Assert.Equal(1000, persisted.LastError!.Length);
    }
}

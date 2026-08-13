using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ProjectChicago.Contracts.Audit;
using ProjectChicago.Crm.Core.Data;
using ProjectChicago.Crm.Core.Models.DataModels.Entities;
using ProjectChicago.Crm.Core.Persistence;
using ProjectChicago.Crm.Core.Repositories;
using ProjectChicago.Crm.Core.Tests.Persistence;
using ProjectChicago.Shared.Messaging;
using ProjectChicago.Shared.Outbox;
using Xunit;

namespace ProjectChicago.Crm.Core.Tests.Data;

// Real SQL Server integration tests for ClientData's create transaction (CLIENT-001..004,
// AUDIT-001..008, OUTBOX-001/002; messaging.md publish-side test matrix: "state + outbox commit
// together" / "rollback removes both"). Each test gets its own database inside the shared container
// (see MsSqlContainerFixture) so tests never interfere with each other despite sharing one running
// SQL Server instance.
public class ClientDataTests : IClassFixture<MsSqlContainerFixture>
{
    private readonly MsSqlContainerFixture _fixture;

    public ClientDataTests(MsSqlContainerFixture fixture)
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

    private static readonly DateTime CreatedAtUtc = new(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);

    private static Client CreateClient(Guid id, string name = "Acme Corporation") =>
        Client.Create(
            id: id,
            name: name,
            lifecycleStatus: ClientLifecycleStatus.Lead,
            ownerUserId: "owner-1",
            createdBy: "creator-1",
            createdAtUtc: CreatedAtUtc,
            primaryEmail: "jane@acme.example",
            primaryPhone: "+1-555-0100");

    private static EntityMutationAudited CreateAuditFact(Guid clientId, Guid? eventId = null) => new()
    {
        EventId = (eventId ?? Guid.NewGuid()).ToString(),
        OccurredAtUtc = new DateTimeOffset(CreatedAtUtc),
        SourceService = AuditSourceServices.Crm,
        EntityType = AuditEntityTypes.Client,
        EntityId = clientId,
        Action = AuditActions.Created,
        ActorId = "user-1",
        ActorType = AuditActorTypes.User,
        TraceId = Guid.NewGuid().ToString("N"),
        CorrelationId = Guid.NewGuid().ToString(),
        CausationId = Guid.NewGuid().ToString(),
        ChangedFields = ["Name", "PrimaryEmail", "PrimaryPhone"],
    };

    [Fact]
    public async Task CreateAsync_PersistsTheClientAndOneOutboxMessage_InTheSameCommit()
    {
        var db = nameof(CreateAsync_PersistsTheClientAndOneOutboxMessage_InTheSameCommit);
        await using var context = await CreateContextAsync(db);
        var data = new ClientData(context, new ClientRepository(context));
        var clientId = Guid.NewGuid();
        var client = CreateClient(clientId);
        var auditFact = CreateAuditFact(clientId);

        await data.CreateAsync(client, auditFact, CancellationToken.None);

        await using var verifyContext = await CreateContextAsync(db);
        var persistedClient = await verifyContext.Clients.SingleAsync(c => c.Id == clientId);
        Assert.Equal("Acme Corporation", persistedClient.Name);

        var persistedOutbox = await verifyContext.OutboxMessages.SingleAsync(m => m.Id == Guid.Parse(auditFact.EventId));
        Assert.Equal("Audit.EntityMutationAudited", persistedOutbox.ContractType);
        Assert.Equal(EntityMutationAudited.CurrentVersion, persistedOutbox.ContractVersion);
        Assert.Equal(OutboxMessageStatus.Pending, persistedOutbox.Status);
    }

    [Fact]
    public async Task CreateAsync_PreservesActorAndCorrelationMetadata_OnTheOutboxRow()
    {
        var db = nameof(CreateAsync_PreservesActorAndCorrelationMetadata_OnTheOutboxRow);
        await using var context = await CreateContextAsync(db);
        var data = new ClientData(context, new ClientRepository(context));
        var clientId = Guid.NewGuid();
        var client = CreateClient(clientId);
        var auditFact = CreateAuditFact(clientId);

        await data.CreateAsync(client, auditFact, CancellationToken.None);

        await using var verifyContext = await CreateContextAsync(db);
        var persistedOutbox = await verifyContext.OutboxMessages.SingleAsync(m => m.Id == Guid.Parse(auditFact.EventId));
        Assert.Equal(auditFact.CorrelationId, persistedOutbox.CorrelationId);
        Assert.Equal(auditFact.CausationId, persistedOutbox.CausationId);
        Assert.Equal(auditFact.TraceId, persistedOutbox.TraceId);
        Assert.Equal(auditFact.OccurredAtUtc.UtcDateTime, persistedOutbox.OccurredAtUtc);

        var envelope = EventEnvelopeSerializer.Deserialize<EntityMutationAudited>(
            persistedOutbox.Payload, [EntityMutationAudited.CurrentVersion]);
        Assert.Equal(auditFact, envelope.Payload);
        Assert.Equal(auditFact.CorrelationId, envelope.CorrelationId);
        Assert.Equal(auditFact.TraceId, envelope.TraceId);
        Assert.Equal(auditFact.CausationId, envelope.CausationId);
        Assert.Equal(auditFact.EventId, envelope.EventId);
    }

    [Fact]
    public async Task CreateAsync_WhenTheClientInsertFails_RollsBackTheOutboxMessageToo()
    {
        // Proves atomicity, not just an OutboxMessages-table constraint: the second attempt's
        // OutboxMessage has its own fresh, non-conflicting Id, yet must still disappear because it
        // was staged on the same SaveChangesAsync call as the Client insert that fails on a
        // duplicate primary key (messaging.md test matrix: "rollback removes both").
        var db = nameof(CreateAsync_WhenTheClientInsertFails_RollsBackTheOutboxMessageToo);
        await using var context = await CreateContextAsync(db);
        var data = new ClientData(context, new ClientRepository(context));
        var clientId = Guid.NewGuid();

        var firstAuditFact = CreateAuditFact(clientId);
        await data.CreateAsync(CreateClient(clientId, "Acme Corporation"), firstAuditFact, CancellationToken.None);

        await using var conflictingContext = await CreateContextAsync(db);
        var conflictingData = new ClientData(conflictingContext, new ClientRepository(conflictingContext));
        var conflictingClient = CreateClient(clientId, "Duplicate Acme Corporation");
        var secondAuditFact = CreateAuditFact(clientId);

        await Assert.ThrowsAsync<DbUpdateException>(
            () => conflictingData.CreateAsync(conflictingClient, secondAuditFact, CancellationToken.None));

        await using var verifyContext = await CreateContextAsync(db);
        var persistedClient = await verifyContext.Clients.SingleAsync(c => c.Id == clientId);
        Assert.Equal("Acme Corporation", persistedClient.Name);

        Assert.False(await verifyContext.OutboxMessages.AnyAsync(m => m.Id == Guid.Parse(secondAuditFact.EventId)));

        var outboxCount = await verifyContext.OutboxMessages.CountAsync();
        Assert.Equal(1, outboxCount);
    }

    [Fact]
    public async Task CreateAsync_WithANonGuidEventId_ThrowsBeforePersistingAnything()
    {
        var db = nameof(CreateAsync_WithANonGuidEventId_ThrowsBeforePersistingAnything);
        await using var context = await CreateContextAsync(db);
        var data = new ClientData(context, new ClientRepository(context));
        var clientId = Guid.NewGuid();
        var client = CreateClient(clientId);
        var auditFact = CreateAuditFact(clientId) with { EventId = "not-a-guid" };

        await Assert.ThrowsAsync<ArgumentException>(
            () => data.CreateAsync(client, auditFact, CancellationToken.None));

        await using var verifyContext = await CreateContextAsync(db);
        Assert.False(await verifyContext.Clients.AnyAsync(c => c.Id == clientId));
        Assert.Equal(0, await verifyContext.OutboxMessages.CountAsync());
    }

    [Fact]
    public async Task FindDuplicateCandidatesAsync_ReturnsMatchesFromTheRepository()
    {
        // Proves ClientData's passthrough reaches ClientRepository.FindDuplicateCandidatesAsync
        // (CLIENT-004) - the matching logic itself is covered by ClientRepositoryTests.
        var db = nameof(FindDuplicateCandidatesAsync_ReturnsMatchesFromTheRepository);
        await using var context = await CreateContextAsync(db);
        var data = new ClientData(context, new ClientRepository(context));
        var clientId = Guid.NewGuid();
        await data.CreateAsync(CreateClient(clientId, "Acme Corporation"), CreateAuditFact(clientId), CancellationToken.None);

        await using var lookupContext = await CreateContextAsync(db);
        var lookupData = new ClientData(lookupContext, new ClientRepository(lookupContext));
        var candidates = await lookupData.FindDuplicateCandidatesAsync(
            normalizedName: "Acme Corporation", normalizedEmail: null, normalizedPhone: null, CancellationToken.None);

        var candidate = Assert.Single(candidates);
        Assert.Equal(clientId, candidate.Id);
    }

    // --- GetForLifecycleChangeAsync / SaveLifecycleChangeAsync (CLIENT-010..015, AUDIT-001..008,
    // OUTBOX-001/002, DATA-008) ---

    private static EntityMutationAudited CreateLifecycleAuditFact(Guid clientId, ClientLifecycleStatus previous, ClientLifecycleStatus next) => new()
    {
        EventId = Guid.NewGuid().ToString(),
        OccurredAtUtc = new DateTimeOffset(CreatedAtUtc.AddDays(1)),
        SourceService = AuditSourceServices.Crm,
        EntityType = AuditEntityTypes.Client,
        EntityId = clientId,
        Action = AuditActions.StatusChanged,
        ActorId = "user-1",
        ActorType = AuditActorTypes.User,
        TraceId = Guid.NewGuid().ToString("N"),
        CorrelationId = Guid.NewGuid().ToString(),
        CausationId = Guid.NewGuid().ToString(),
        ChangedFields = [nameof(Client.LifecycleStatus)],
        PreviousValues = new Dictionary<string, string> { [nameof(Client.LifecycleStatus)] = previous.ToString() },
        NewValues = new Dictionary<string, string> { [nameof(Client.LifecycleStatus)] = next.ToString() },
    };

    [Fact]
    public async Task GetForLifecycleChangeAsync_WithTheCurrentConcurrencyToken_ReturnsTheClient()
    {
        var db = nameof(GetForLifecycleChangeAsync_WithTheCurrentConcurrencyToken_ReturnsTheClient);
        await using var context = await CreateContextAsync(db);
        var data = new ClientData(context, new ClientRepository(context));
        var clientId = Guid.NewGuid();
        await data.CreateAsync(CreateClient(clientId), CreateAuditFact(clientId), CancellationToken.None);

        await using var lookupContext = await CreateContextAsync(db);
        var lookupRepository = new ClientRepository(lookupContext);
        var persisted = await lookupRepository.GetForUpdateAsync(clientId, CancellationToken.None);
        var currentToken = Convert.ToBase64String(persisted!.RowVersion);

        await using var changeContext = await CreateContextAsync(db);
        var changeData = new ClientData(changeContext, new ClientRepository(changeContext));
        var loaded = await changeData.GetForLifecycleChangeAsync(clientId, currentToken, CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Equal(clientId, loaded!.Id);
    }

    [Fact]
    public async Task GetForLifecycleChangeAsync_WhenTheClientDoesNotExist_ReturnsNull()
    {
        var db = nameof(GetForLifecycleChangeAsync_WhenTheClientDoesNotExist_ReturnsNull);
        await using var context = await CreateContextAsync(db);
        var data = new ClientData(context, new ClientRepository(context));

        var loaded = await data.GetForLifecycleChangeAsync(
            Guid.NewGuid(), Convert.ToBase64String([1, 2, 3]), CancellationToken.None);

        Assert.Null(loaded);
    }

    [Fact]
    public async Task GetForLifecycleChangeAsync_WithAStaleConcurrencyToken_ThrowsWithoutMutatingAnything()
    {
        // Simulates DATA-008's core scenario: the caller last saw the Client at an earlier
        // RowVersion (the token from creation), but another request already changed it before
        // this transition was submitted.
        var db = nameof(GetForLifecycleChangeAsync_WithAStaleConcurrencyToken_ThrowsWithoutMutatingAnything);
        await using var context = await CreateContextAsync(db);
        var data = new ClientData(context, new ClientRepository(context));
        var clientId = Guid.NewGuid();
        await data.CreateAsync(CreateClient(clientId), CreateAuditFact(clientId), CancellationToken.None);

        await using var lookupContext = await CreateContextAsync(db);
        var lookupRepository = new ClientRepository(lookupContext);
        var initiallyPersisted = await lookupRepository.GetForUpdateAsync(clientId, CancellationToken.None);
        var staleToken = Convert.ToBase64String(initiallyPersisted!.RowVersion);

        // A concurrent request changes the Client first, advancing its RowVersion.
        await using var concurrentContext = await CreateContextAsync(db);
        var concurrentData = new ClientData(concurrentContext, new ClientRepository(concurrentContext));
        var concurrentClient = await concurrentData.GetForLifecycleChangeAsync(clientId, staleToken, CancellationToken.None);
        concurrentClient!.ChangeLifecycleStatus(ClientLifecycleStatus.Active, "concurrent-modifier", CreatedAtUtc.AddDays(1));
        await concurrentData.SaveLifecycleChangeAsync(
            concurrentClient, CreateLifecycleAuditFact(clientId, ClientLifecycleStatus.Lead, ClientLifecycleStatus.Active), CancellationToken.None);

        await using var staleContext = await CreateContextAsync(db);
        var staleData = new ClientData(staleContext, new ClientRepository(staleContext));

        await Assert.ThrowsAsync<ClientConcurrencyConflictException>(
            () => staleData.GetForLifecycleChangeAsync(clientId, staleToken, CancellationToken.None));

        await using var verifyContext = await CreateContextAsync(db);
        var persisted = await verifyContext.Clients.SingleAsync(c => c.Id == clientId);
        Assert.Equal(ClientLifecycleStatus.Active, persisted.LifecycleStatus);
        Assert.Equal("concurrent-modifier", persisted.LastModifiedBy);
    }

    [Fact]
    public async Task SaveLifecycleChangeAsync_PersistsTheStateChangeAndOneOutboxMessage_InTheSameCommit()
    {
        var db = nameof(SaveLifecycleChangeAsync_PersistsTheStateChangeAndOneOutboxMessage_InTheSameCommit);
        await using var context = await CreateContextAsync(db);
        var data = new ClientData(context, new ClientRepository(context));
        var clientId = Guid.NewGuid();
        await data.CreateAsync(CreateClient(clientId), CreateAuditFact(clientId), CancellationToken.None);

        await using var changeContext = await CreateContextAsync(db);
        var changeData = new ClientData(changeContext, new ClientRepository(changeContext));
        var lookupRepository = new ClientRepository(changeContext);
        var persisted = await lookupRepository.GetForUpdateAsync(clientId, CancellationToken.None);
        var token = Convert.ToBase64String(persisted!.RowVersion);
        var client = await changeData.GetForLifecycleChangeAsync(clientId, token, CancellationToken.None);
        client!.ChangeLifecycleStatus(ClientLifecycleStatus.Active, "modifier-1", CreatedAtUtc.AddDays(1));
        var auditFact = CreateLifecycleAuditFact(clientId, ClientLifecycleStatus.Lead, ClientLifecycleStatus.Active);

        await changeData.SaveLifecycleChangeAsync(client, auditFact, CancellationToken.None);

        await using var verifyContext = await CreateContextAsync(db);
        var persistedClient = await verifyContext.Clients.SingleAsync(c => c.Id == clientId);
        Assert.Equal(ClientLifecycleStatus.Active, persistedClient.LifecycleStatus);
        Assert.Equal("modifier-1", persistedClient.LastModifiedBy);

        var persistedOutbox = await verifyContext.OutboxMessages.SingleAsync(m => m.Id == Guid.Parse(auditFact.EventId));
        Assert.Equal("Audit.EntityMutationAudited", persistedOutbox.ContractType);
        Assert.Equal(OutboxMessageStatus.Pending, persistedOutbox.Status);

        var envelope = EventEnvelopeSerializer.Deserialize<EntityMutationAudited>(
            persistedOutbox.Payload, [EntityMutationAudited.CurrentVersion]);
        Assert.Equal(auditFact, envelope.Payload);
    }

    [Fact]
    public async Task SaveLifecycleChangeAsync_WhenAConcurrentWriteWonTheRace_ThrowsAndRollsBackTheOutboxMessage()
    {
        // Proves the belt-and-suspenders DbUpdateConcurrencyException path in
        // SaveLifecycleChangeAsync itself, not just GetForLifecycleChangeAsync's own token check:
        // both changeData and concurrentData load the same starting RowVersion, concurrentData
        // saves first, and changeData's later save must still fail even though its own token check
        // already passed when it read.
        var db = nameof(SaveLifecycleChangeAsync_WhenAConcurrentWriteWonTheRace_ThrowsAndRollsBackTheOutboxMessage);
        await using var context = await CreateContextAsync(db);
        var data = new ClientData(context, new ClientRepository(context));
        var clientId = Guid.NewGuid();
        await data.CreateAsync(CreateClient(clientId), CreateAuditFact(clientId), CancellationToken.None);

        await using var lookupContext = await CreateContextAsync(db);
        var lookupRepository = new ClientRepository(lookupContext);
        var initiallyPersisted = await lookupRepository.GetForUpdateAsync(clientId, CancellationToken.None);
        var sharedStartingToken = Convert.ToBase64String(initiallyPersisted!.RowVersion);

        await using var firstContext = await CreateContextAsync(db);
        var firstData = new ClientData(firstContext, new ClientRepository(firstContext));
        var firstClient = await firstData.GetForLifecycleChangeAsync(clientId, sharedStartingToken, CancellationToken.None);

        await using var secondContext = await CreateContextAsync(db);
        var secondData = new ClientData(secondContext, new ClientRepository(secondContext));
        var secondClient = await secondData.GetForLifecycleChangeAsync(clientId, sharedStartingToken, CancellationToken.None);

        firstClient!.ChangeLifecycleStatus(ClientLifecycleStatus.Active, "first-modifier", CreatedAtUtc.AddDays(1));
        await firstData.SaveLifecycleChangeAsync(
            firstClient, CreateLifecycleAuditFact(clientId, ClientLifecycleStatus.Lead, ClientLifecycleStatus.Active), CancellationToken.None);

        secondClient!.ChangeLifecycleStatus(ClientLifecycleStatus.Inactive, "second-modifier", CreatedAtUtc.AddDays(1));
        var secondAuditFact = CreateLifecycleAuditFact(clientId, ClientLifecycleStatus.Lead, ClientLifecycleStatus.Inactive);

        await Assert.ThrowsAsync<ClientConcurrencyConflictException>(
            () => secondData.SaveLifecycleChangeAsync(secondClient, secondAuditFact, CancellationToken.None));

        await using var verifyContext = await CreateContextAsync(db);
        var persistedClient = await verifyContext.Clients.SingleAsync(c => c.Id == clientId);
        Assert.Equal(ClientLifecycleStatus.Active, persistedClient.LifecycleStatus);
        Assert.Equal("first-modifier", persistedClient.LastModifiedBy);

        Assert.False(await verifyContext.OutboxMessages.AnyAsync(m => m.Id == Guid.Parse(secondAuditFact.EventId)));
    }

    // --- ListAsync passthrough (CLIENT-020..024) ---
    //
    // ClientRepositoryTests already covers the query-shaping behavior (search/filter/sort/paging
    // correctness) in depth; this test only proves ClientData.ListAsync reaches
    // ClientRepository.ListAsync unchanged and that the returned pagination metadata (TotalCount
    // spanning the whole filtered set, Items bounded to one page) survives the passthrough.
    [Fact]
    public async Task ListAsync_ReturnsPagedItemsAndTheTotalMatchingCount_FromTheRepository()
    {
        var db = nameof(ListAsync_ReturnsPagedItemsAndTheTotalMatchingCount_FromTheRepository);
        await using var context = await CreateContextAsync(db);
        var data = new ClientData(context, new ClientRepository(context));

        for (var i = 0; i < 5; i++)
        {
            await data.CreateAsync(
                CreateClient(Guid.NewGuid(), $"Acme {i}"),
                CreateAuditFact(Guid.NewGuid()),
                CancellationToken.None);
        }

        await using var listContext = await CreateContextAsync(db);
        var listData = new ClientData(listContext, new ClientRepository(listContext));

        var filter = new ClientListFilter
        {
            SortBy = ClientListSortField.Name,
            SortDirection = ClientListSortDirection.Ascending,
            Page = 2,
            PageSize = 2,
        };

        var result = await listData.ListAsync(filter, CancellationToken.None);

        Assert.Equal(5, result.TotalCount);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal("Acme 2", result.Items[0].Name);
        Assert.Equal("Acme 3", result.Items[1].Name);
    }

    // --- GetDetailAsync passthrough (CLIENT-030..032) ---
    //
    // ClientRepositoryTests already covers the query-shaping behavior (active/historical Project
    // split, open/recently-completed Task split, ordering) in depth; these tests only prove
    // ClientData.GetDetailAsync reaches ClientRepository.GetDetailAsync unchanged.

    [Fact]
    public async Task GetDetailAsync_ReturnsTheDetailFromTheRepository()
    {
        var db = nameof(GetDetailAsync_ReturnsTheDetailFromTheRepository);
        await using var context = await CreateContextAsync(db);
        var data = new ClientData(context, new ClientRepository(context));
        var clientId = Guid.NewGuid();
        await data.CreateAsync(CreateClient(clientId, "Acme Corporation"), CreateAuditFact(clientId), CancellationToken.None);

        await using var lookupContext = await CreateContextAsync(db);
        var lookupData = new ClientData(lookupContext, new ClientRepository(lookupContext));
        var result = await lookupData.GetDetailAsync(clientId, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(clientId, result!.Client.Id);
        Assert.Empty(result.ActiveProjects);
        Assert.Empty(result.HistoricalProjects);
        Assert.Empty(result.OpenTasks);
        Assert.Empty(result.RecentlyCompletedTasks);
    }

    [Fact]
    public async Task GetDetailAsync_WhenTheClientDoesNotExist_ReturnsNull()
    {
        var db = nameof(GetDetailAsync_WhenTheClientDoesNotExist_ReturnsNull);
        await using var context = await CreateContextAsync(db);
        var data = new ClientData(context, new ClientRepository(context));

        var result = await data.GetDetailAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Null(result);
    }
}

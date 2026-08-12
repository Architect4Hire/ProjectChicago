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
}

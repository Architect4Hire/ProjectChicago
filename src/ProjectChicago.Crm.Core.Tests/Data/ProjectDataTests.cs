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

// Real SQL Server integration tests for ProjectData's create transaction (PROJECT-001..002,
// DATA-001..005, AUDIT-001..008, OUTBOX-001/002; messaging.md publish-side test matrix: "state +
// outbox commit together" / "rollback removes both" / "rollback on validation"). Each test gets its
// own database inside the shared container (see MsSqlContainerFixture) so tests never interfere with
// each other despite sharing one running SQL Server instance.
public class ProjectDataTests : IClassFixture<MsSqlContainerFixture>
{
    private readonly MsSqlContainerFixture _fixture;

    public ProjectDataTests(MsSqlContainerFixture fixture)
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

    private static Project CreateProject(Guid id, Guid clientId, string name = "Website Redesign") =>
        Project.Create(
            id: id,
            clientId: clientId,
            name: name,
            status: ProjectStatus.Planned,
            priority: ProjectPriority.Normal,
            ownerUserId: "owner-1",
            createdBy: "creator-1",
            createdAtUtc: CreatedAtUtc,
            description: "Redesign the client website",
            startDateUtc: CreatedAtUtc.AddDays(1),
            targetCompletionDateUtc: CreatedAtUtc.AddDays(30));

    private static EntityMutationAudited CreateAuditFact(Guid projectId, Guid? eventId = null) => new()
    {
        EventId = (eventId ?? Guid.NewGuid()).ToString(),
        OccurredAtUtc = new DateTimeOffset(CreatedAtUtc),
        SourceService = AuditSourceServices.Crm,
        EntityType = AuditEntityTypes.Project,
        EntityId = projectId,
        Action = AuditActions.Created,
        ActorId = "user-1",
        ActorType = AuditActorTypes.User,
        TraceId = Guid.NewGuid().ToString("N"),
        CorrelationId = Guid.NewGuid().ToString(),
        CausationId = Guid.NewGuid().ToString(),
        ChangedFields = ["Name", "Status", "Priority"],
    };

    [Fact]
    public async Task CreateAsync_WhenClientExists_PersistsTheProjectAndOneOutboxMessage_InTheSameCommit()
    {
        var db = nameof(CreateAsync_WhenClientExists_PersistsTheProjectAndOneOutboxMessage_InTheSameCommit);
        await using var setupContext = await CreateContextAsync(db);
        var setupData = new ClientData(setupContext, new ClientRepository(setupContext));
        var clientId = Guid.NewGuid();
        var client = CreateClient(clientId);
        await setupData.CreateAsync(client, new EntityMutationAudited
        {
            EventId = Guid.NewGuid().ToString(),
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
        }, CancellationToken.None);

        await using var context = await CreateContextAsync(db);
        var data = new ProjectData(context, new ProjectRepository(context));
        var projectId = Guid.NewGuid();
        var project = CreateProject(projectId, clientId);
        var auditFact = CreateAuditFact(projectId);

        await data.CreateAsync(project, auditFact, CancellationToken.None);

        await using var verifyContext = await CreateContextAsync(db);
        var persistedProject = await verifyContext.Projects.SingleAsync(p => p.Id == projectId);
        Assert.Equal("Website Redesign", persistedProject.Name);
        Assert.Equal(clientId, persistedProject.ClientId);

        var persistedOutbox = await verifyContext.OutboxMessages.SingleAsync(m => m.Id == Guid.Parse(auditFact.EventId));
        Assert.Equal("Audit.EntityMutationAudited", persistedOutbox.ContractType);
        Assert.Equal(EntityMutationAudited.CurrentVersion, persistedOutbox.ContractVersion);
        Assert.Equal(OutboxMessageStatus.Pending, persistedOutbox.Status);
    }

    [Fact]
    public async Task CreateAsync_WhenClientDoesNotExist_ThrowsProjectClientNotFoundException_WithoutPersistingAnything()
    {
        var db = nameof(CreateAsync_WhenClientDoesNotExist_ThrowsProjectClientNotFoundException_WithoutPersistingAnything);
        await using var context = await CreateContextAsync(db);
        var data = new ProjectData(context, new ProjectRepository(context));
        var projectId = Guid.NewGuid();
        var nonexistentClientId = Guid.NewGuid();
        var project = CreateProject(projectId, nonexistentClientId);
        var auditFact = CreateAuditFact(projectId);

        await Assert.ThrowsAsync<ProjectClientNotFoundException>(
            () => data.CreateAsync(project, auditFact, CancellationToken.None));

        await using var verifyContext = await CreateContextAsync(db);
        Assert.False(await verifyContext.Projects.AnyAsync(p => p.Id == projectId));
        Assert.False(await verifyContext.OutboxMessages.AnyAsync(m => m.Id == Guid.Parse(auditFact.EventId)));
    }

    [Fact]
    public async Task CreateAsync_PreservesActorAndCorrelationMetadata_OnTheOutboxRow()
    {
        var db = nameof(CreateAsync_PreservesActorAndCorrelationMetadata_OnTheOutboxRow);
        await using var setupContext = await CreateContextAsync(db);
        var setupData = new ClientData(setupContext, new ClientRepository(setupContext));
        var clientId = Guid.NewGuid();
        var client = CreateClient(clientId);
        await setupData.CreateAsync(client, new EntityMutationAudited
        {
            EventId = Guid.NewGuid().ToString(),
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
        }, CancellationToken.None);

        await using var context = await CreateContextAsync(db);
        var data = new ProjectData(context, new ProjectRepository(context));
        var projectId = Guid.NewGuid();
        var project = CreateProject(projectId, clientId);
        var auditFact = CreateAuditFact(projectId);

        await data.CreateAsync(project, auditFact, CancellationToken.None);

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
    public async Task CreateAsync_WhenTheProjectInsertFails_RollsBackTheOutboxMessageToo()
    {
        // Proves atomicity: the second attempt's OutboxMessage has its own fresh, non-conflicting
        // Id, yet must still disappear because it was staged on the same SaveChangesAsync call as
        // the Project insert that fails (messaging.md test matrix: "rollback removes both").
        var db = nameof(CreateAsync_WhenTheProjectInsertFails_RollsBackTheOutboxMessageToo);
        await using var setupContext = await CreateContextAsync(db);
        var setupData = new ClientData(setupContext, new ClientRepository(setupContext));
        var clientId = Guid.NewGuid();
        var client = CreateClient(clientId);
        var setupAuditFact = new EntityMutationAudited
        {
            EventId = Guid.NewGuid().ToString(),
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
        };
        await setupData.CreateAsync(client, setupAuditFact, CancellationToken.None);

        await using var context = await CreateContextAsync(db);
        var data = new ProjectData(context, new ProjectRepository(context));
        var projectId = Guid.NewGuid();

        var firstAuditFact = CreateAuditFact(projectId);
        await data.CreateAsync(CreateProject(projectId, clientId, "First Project"), firstAuditFact, CancellationToken.None);

        await using var conflictingContext = await CreateContextAsync(db);
        var conflictingData = new ProjectData(conflictingContext, new ProjectRepository(conflictingContext));
        var conflictingProject = CreateProject(projectId, clientId, "Duplicate Project");
        var secondAuditFact = CreateAuditFact(projectId);

        await Assert.ThrowsAsync<DbUpdateException>(
            () => conflictingData.CreateAsync(conflictingProject, secondAuditFact, CancellationToken.None));

        await using var verifyContext = await CreateContextAsync(db);
        var persistedProject = await verifyContext.Projects.SingleAsync(p => p.Id == projectId);
        Assert.Equal("First Project", persistedProject.Name);

        Assert.False(await verifyContext.OutboxMessages.AnyAsync(m => m.Id == Guid.Parse(secondAuditFact.EventId)));

        var projectOutboxCount = await verifyContext.OutboxMessages.CountAsync(m => m.Id == Guid.Parse(firstAuditFact.EventId));
        Assert.Equal(1, projectOutboxCount);
    }

    [Fact]
    public async Task CreateAsync_WithANonGuidEventId_ThrowsBeforePersistingAnything()
    {
        var db = nameof(CreateAsync_WithANonGuidEventId_ThrowsBeforePersistingAnything);
        await using var setupContext = await CreateContextAsync(db);
        var setupData = new ClientData(setupContext, new ClientRepository(setupContext));
        var clientId = Guid.NewGuid();
        var client = CreateClient(clientId);
        var setupAuditFact = new EntityMutationAudited
        {
            EventId = Guid.NewGuid().ToString(),
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
        };
        await setupData.CreateAsync(client, setupAuditFact, CancellationToken.None);

        await using var context = await CreateContextAsync(db);
        var data = new ProjectData(context, new ProjectRepository(context));
        var projectId = Guid.NewGuid();
        var project = CreateProject(projectId, clientId);
        var auditFact = CreateAuditFact(projectId) with { EventId = "not-a-guid" };

        await Assert.ThrowsAsync<ArgumentException>(
            () => data.CreateAsync(project, auditFact, CancellationToken.None));

        await using var verifyContext = await CreateContextAsync(db);
        Assert.False(await verifyContext.Projects.AnyAsync(p => p.Id == projectId));
        Assert.Single(await verifyContext.OutboxMessages.ToListAsync());
        var persistedOutbox = await verifyContext.OutboxMessages.SingleAsync();
        Assert.Equal(setupAuditFact.EventId, persistedOutbox.Id.ToString());
    }
}

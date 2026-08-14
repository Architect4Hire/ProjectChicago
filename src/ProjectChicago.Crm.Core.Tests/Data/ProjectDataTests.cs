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

    // --- Edit (PROJECT-002, DATA-008, AUDIT-001..008) ---

    [Fact]
    public async Task EditAsync_UpdatesProjectAndCreatesAuditFactAtomically()
    {
        var db = nameof(EditAsync_UpdatesProjectAndCreatesAuditFactAtomically);
        await using var setupContext = await CreateContextAsync(db);
        var setupData = new ClientData(setupContext, new ClientRepository(setupContext));
        var clientId = Guid.NewGuid();
        var client = CreateClient(clientId);
        var clientAuditFact = new EntityMutationAudited
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
        await setupData.CreateAsync(client, clientAuditFact, CancellationToken.None);

        var projectId = Guid.NewGuid();
        var project = CreateProject(projectId, clientId);
        await using var createContext = await CreateContextAsync(db);
        var createData = new ProjectData(createContext, new ProjectRepository(createContext));
        await createData.CreateAsync(project, CreateAuditFact(projectId), CancellationToken.None);

        await using var context = await CreateContextAsync(db);
        var data = new ProjectData(context, new ProjectRepository(context));
        var retrievedProject = await data.GetAsync(projectId, CancellationToken.None);
        var concurrencyToken = Convert.ToBase64String(retrievedProject!.RowVersion);

        retrievedProject.Edit(
            name: "Updated Name",
            modifiedBy: "modifier-1",
            modifiedAtUtc: CreatedAtUtc.AddDays(1));

        var editAuditFact = new EntityMutationAudited
        {
            EventId = Guid.NewGuid().ToString(),
            OccurredAtUtc = new DateTimeOffset(CreatedAtUtc.AddDays(1)),
            SourceService = AuditSourceServices.Crm,
            EntityType = AuditEntityTypes.Project,
            EntityId = projectId,
            Action = AuditActions.Updated,
            ActorId = "user-1",
            ActorType = AuditActorTypes.User,
            TraceId = Guid.NewGuid().ToString("N"),
            CorrelationId = Guid.NewGuid().ToString(),
            CausationId = Guid.NewGuid().ToString(),
            ChangedFields = [nameof(Project.Name)],
        };
        await data.EditAsync(
            retrievedProject,
            "modifier-1",
            CreatedAtUtc.AddDays(1),
            concurrencyToken,
            editAuditFact,
            CancellationToken.None);

        await using var verifyContext = await CreateContextAsync(db);
        var persistedProject = await verifyContext.Projects.SingleAsync(p => p.Id == projectId);
        Assert.Equal("Updated Name", persistedProject.Name);

        var persistedOutbox = await verifyContext.OutboxMessages.SingleAsync(m => m.Id == Guid.Parse(editAuditFact.EventId));
        Assert.Equal(editAuditFact.CorrelationId, persistedOutbox.CorrelationId);
        Assert.Equal(editAuditFact.TraceId, persistedOutbox.TraceId);
    }

    [Fact]
    public async Task EditAsync_WithMismatchedConcurrencyToken_ThrowsDbUpdateConcurrencyException()
    {
        var db = nameof(EditAsync_WithMismatchedConcurrencyToken_ThrowsDbUpdateConcurrencyException);
        await using var setupContext = await CreateContextAsync(db);
        var setupData = new ClientData(setupContext, new ClientRepository(setupContext));
        var clientId = Guid.NewGuid();
        var client = CreateClient(clientId);
        var clientAuditFact = new EntityMutationAudited
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
        await setupData.CreateAsync(client, clientAuditFact, CancellationToken.None);

        var projectId = Guid.NewGuid();
        var project = CreateProject(projectId, clientId);
        await using var createContext = await CreateContextAsync(db);
        var createData = new ProjectData(createContext, new ProjectRepository(createContext));
        await createData.CreateAsync(project, CreateAuditFact(projectId), CancellationToken.None);

        await using var context = await CreateContextAsync(db);
        var data = new ProjectData(context, new ProjectRepository(context));
        var retrievedProject = await data.GetAsync(projectId, CancellationToken.None);

        // Modify the project through raw SQL to change its rowversion
        await context.Database.ExecuteSqlAsync($"UPDATE Projects SET Name = 'Modified' WHERE Id = {retrievedProject!.Id:D}");

        retrievedProject.Edit(
            name: "Updated Name",
            modifiedBy: "modifier-1",
            modifiedAtUtc: CreatedAtUtc.AddDays(1));

        var staleToken = Convert.ToBase64String(retrievedProject.RowVersion);
        var editAuditFact = new EntityMutationAudited
        {
            EventId = Guid.NewGuid().ToString(),
            OccurredAtUtc = new DateTimeOffset(CreatedAtUtc.AddDays(1)),
            SourceService = AuditSourceServices.Crm,
            EntityType = AuditEntityTypes.Project,
            EntityId = projectId,
            Action = AuditActions.Updated,
            ActorId = "user-1",
            ActorType = AuditActorTypes.User,
            TraceId = Guid.NewGuid().ToString("N"),
            CorrelationId = Guid.NewGuid().ToString(),
            CausationId = Guid.NewGuid().ToString(),
            ChangedFields = [nameof(Project.Name)],
        };

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
            () => data.EditAsync(
                retrievedProject,
                "modifier-1",
                CreatedAtUtc.AddDays(1),
                staleToken,
                editAuditFact,
                CancellationToken.None));
    }

    [Fact]
    public async Task EditAsync_WhenEditFails_RollsBackTheOutboxMessageToo()
    {
        var db = nameof(EditAsync_WhenEditFails_RollsBackTheOutboxMessageToo);
        await using var setupContext = await CreateContextAsync(db);
        var setupData = new ClientData(setupContext, new ClientRepository(setupContext));
        var clientId = Guid.NewGuid();
        var client = CreateClient(clientId);
        var clientAuditFact = new EntityMutationAudited
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
        await setupData.CreateAsync(client, clientAuditFact, CancellationToken.None);

        var projectId = Guid.NewGuid();
        var project = CreateProject(projectId, clientId);
        await using var createContext = await CreateContextAsync(db);
        var createData = new ProjectData(createContext, new ProjectRepository(createContext));
        await createData.CreateAsync(project, CreateAuditFact(projectId), CancellationToken.None);

        // Create two concurrent edit attempts, one will succeed and one will fail with concurrency conflict
        await using var context1 = await CreateContextAsync(db);
        var data1 = new ProjectData(context1, new ProjectRepository(context1));
        var retrieved1 = await data1.GetAsync(projectId, CancellationToken.None);
        var concurrencyToken1 = Convert.ToBase64String(retrieved1!.RowVersion);

        retrieved1.Edit(
            name: "First Edit",
            modifiedBy: "modifier-1",
            modifiedAtUtc: CreatedAtUtc.AddDays(1));

        var auditFact1 = new EntityMutationAudited
        {
            EventId = Guid.NewGuid().ToString(),
            OccurredAtUtc = new DateTimeOffset(CreatedAtUtc.AddDays(1)),
            SourceService = AuditSourceServices.Crm,
            EntityType = AuditEntityTypes.Project,
            EntityId = projectId,
            Action = AuditActions.Updated,
            ActorId = "user-1",
            ActorType = AuditActorTypes.User,
            TraceId = Guid.NewGuid().ToString("N"),
            CorrelationId = Guid.NewGuid().ToString(),
            CausationId = Guid.NewGuid().ToString(),
            ChangedFields = [nameof(Project.Name)],
        };
        await data1.EditAsync(
            retrieved1,
            "modifier-1",
            CreatedAtUtc.AddDays(1),
            concurrencyToken1,
            auditFact1,
            CancellationToken.None);

        await using var context2 = await CreateContextAsync(db);
        var data2 = new ProjectData(context2, new ProjectRepository(context2));
        var retrieved2 = await data2.GetAsync(projectId, CancellationToken.None);
        var concurrencyToken2 = Convert.ToBase64String(retrieved2!.RowVersion);

        retrieved2.Edit(
            name: "Second Edit",
            modifiedBy: "modifier-2",
            modifiedAtUtc: CreatedAtUtc.AddDays(2));

        var auditFact2 = new EntityMutationAudited
        {
            EventId = Guid.NewGuid().ToString(),
            OccurredAtUtc = new DateTimeOffset(CreatedAtUtc.AddDays(2)),
            SourceService = AuditSourceServices.Crm,
            EntityType = AuditEntityTypes.Project,
            EntityId = projectId,
            Action = AuditActions.Updated,
            ActorId = "user-1",
            ActorType = AuditActorTypes.User,
            TraceId = Guid.NewGuid().ToString("N"),
            CorrelationId = Guid.NewGuid().ToString(),
            CausationId = Guid.NewGuid().ToString(),
            ChangedFields = [nameof(Project.Name)],
        };

        // This should fail with concurrency conflict
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
            () => data2.EditAsync(
                retrieved2,
                "modifier-2",
                CreatedAtUtc.AddDays(2),
                concurrencyToken2,
                auditFact2,
                CancellationToken.None));

        // Verify that only the first edit's outbox message was persisted
        await using var verifyContext = await CreateContextAsync(db);
        var outboxMessages = await verifyContext.OutboxMessages.ToListAsync();
        Assert.Single(outboxMessages, m => m.Id == Guid.Parse(auditFact1.EventId));
        Assert.DoesNotContain(outboxMessages, m => m.Id == Guid.Parse(auditFact2.EventId));
    }

    // --- Audit before/after values (AUDIT-002) ---

    [Fact]
    public async Task EditAsync_PreservesPreviousAndNewValuesInAuditPayload()
    {
        var db = nameof(EditAsync_PreservesPreviousAndNewValuesInAuditPayload);
        await using var setupContext = await CreateContextAsync(db);
        var setupData = new ClientData(setupContext, new ClientRepository(setupContext));
        var clientId = Guid.NewGuid();
        var client = CreateClient(clientId);
        var clientAuditFact = new EntityMutationAudited
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
        await setupData.CreateAsync(client, clientAuditFact, CancellationToken.None);

        var projectId = Guid.NewGuid();
        var project = CreateProject(projectId, clientId);
        await using var createContext = await CreateContextAsync(db);
        var createData = new ProjectData(createContext, new ProjectRepository(createContext));
        await createData.CreateAsync(project, CreateAuditFact(projectId), CancellationToken.None);

        await using var context = await CreateContextAsync(db);
        var data = new ProjectData(context, new ProjectRepository(context));
        var retrievedProject = await data.GetAsync(projectId, CancellationToken.None);
        var concurrencyToken = Convert.ToBase64String(retrievedProject!.RowVersion);

        retrievedProject.Edit(
            name: "Updated Name",
            description: "Updated Description",
            priority: ProjectPriority.High,
            modifiedBy: "modifier-1",
            modifiedAtUtc: CreatedAtUtc.AddDays(1));

        var editAuditFact = new EntityMutationAudited
        {
            EventId = Guid.NewGuid().ToString(),
            OccurredAtUtc = new DateTimeOffset(CreatedAtUtc.AddDays(1)),
            SourceService = AuditSourceServices.Crm,
            EntityType = AuditEntityTypes.Project,
            EntityId = projectId,
            Action = AuditActions.Updated,
            ActorId = "user-1",
            ActorType = AuditActorTypes.User,
            TraceId = Guid.NewGuid().ToString("N"),
            CorrelationId = Guid.NewGuid().ToString(),
            CausationId = Guid.NewGuid().ToString(),
            ChangedFields = [nameof(Project.Name), nameof(Project.Description), nameof(Project.Priority)],
            PreviousValues = new Dictionary<string, string>
            {
                { nameof(Project.Name), "Website Redesign" },
                { nameof(Project.Description), "Redesign the client website" },
                { nameof(Project.Priority), "Normal" },
            },
            NewValues = new Dictionary<string, string>
            {
                { nameof(Project.Name), "Updated Name" },
                { nameof(Project.Description), "Updated Description" },
                { nameof(Project.Priority), "High" },
            },
        };

        await data.EditAsync(
            retrievedProject,
            "modifier-1",
            CreatedAtUtc.AddDays(1),
            concurrencyToken,
            editAuditFact,
            CancellationToken.None);

        await using var verifyContext = await CreateContextAsync(db);
        var persistedOutbox = await verifyContext.OutboxMessages.SingleAsync(m => m.Id == Guid.Parse(editAuditFact.EventId));

        var envelope = EventEnvelopeSerializer.Deserialize<EntityMutationAudited>(
            persistedOutbox.Payload, [EntityMutationAudited.CurrentVersion]);

        Assert.NotNull(envelope.Payload.PreviousValues);
        Assert.NotNull(envelope.Payload.NewValues);

        Assert.Equal("Website Redesign", envelope.Payload.PreviousValues[nameof(Project.Name)]);
        Assert.Equal("Updated Name", envelope.Payload.NewValues[nameof(Project.Name)]);

        Assert.Equal("Redesign the client website", envelope.Payload.PreviousValues[nameof(Project.Description)]);
        Assert.Equal("Updated Description", envelope.Payload.NewValues[nameof(Project.Description)]);

        Assert.Equal("Normal", envelope.Payload.PreviousValues[nameof(Project.Priority)]);
        Assert.Equal("High", envelope.Payload.NewValues[nameof(Project.Priority)]);
    }

    [Fact]
    public async Task EditAsync_WhenEditingOptionalFieldFromNullToValue_OnlyIncludesNewValue()
    {
        var db = nameof(EditAsync_WhenEditingOptionalFieldFromNullToValue_OnlyIncludesNewValue);
        await using var setupContext = await CreateContextAsync(db);
        var setupData = new ClientData(setupContext, new ClientRepository(setupContext));
        var clientId = Guid.NewGuid();
        var client = CreateClient(clientId);
        var clientAuditFact = new EntityMutationAudited
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
        await setupData.CreateAsync(client, clientAuditFact, CancellationToken.None);

        var projectId = Guid.NewGuid();
        var project = Project.Create(
            id: projectId,
            clientId: clientId,
            name: "Website Redesign",
            status: ProjectStatus.Planned,
            priority: ProjectPriority.Normal,
            ownerUserId: "owner-1",
            createdBy: "creator-1",
            createdAtUtc: CreatedAtUtc,
            description: null,
            notes: null);

        await using var createContext = await CreateContextAsync(db);
        var createData = new ProjectData(createContext, new ProjectRepository(createContext));
        await createData.CreateAsync(project, CreateAuditFact(projectId), CancellationToken.None);

        await using var context = await CreateContextAsync(db);
        var data = new ProjectData(context, new ProjectRepository(context));
        var retrievedProject = await data.GetAsync(projectId, CancellationToken.None);
        var concurrencyToken = Convert.ToBase64String(retrievedProject!.RowVersion);

        retrievedProject.Edit(
            description: "New Description",
            modifiedBy: "modifier-1",
            modifiedAtUtc: CreatedAtUtc.AddDays(1));

        var editAuditFact = new EntityMutationAudited
        {
            EventId = Guid.NewGuid().ToString(),
            OccurredAtUtc = new DateTimeOffset(CreatedAtUtc.AddDays(1)),
            SourceService = AuditSourceServices.Crm,
            EntityType = AuditEntityTypes.Project,
            EntityId = projectId,
            Action = AuditActions.Updated,
            ActorId = "user-1",
            ActorType = AuditActorTypes.User,
            TraceId = Guid.NewGuid().ToString("N"),
            CorrelationId = Guid.NewGuid().ToString(),
            CausationId = Guid.NewGuid().ToString(),
            ChangedFields = [nameof(Project.Description)],
            PreviousValues = null,
            NewValues = new Dictionary<string, string>
            {
                { nameof(Project.Description), "New Description" },
            },
        };

        await data.EditAsync(
            retrievedProject,
            "modifier-1",
            CreatedAtUtc.AddDays(1),
            concurrencyToken,
            editAuditFact,
            CancellationToken.None);

        await using var verifyContext = await CreateContextAsync(db);
        var persistedOutbox = await verifyContext.OutboxMessages.SingleAsync(m => m.Id == Guid.Parse(editAuditFact.EventId));

        var envelope = EventEnvelopeSerializer.Deserialize<EntityMutationAudited>(
            persistedOutbox.Payload, [EntityMutationAudited.CurrentVersion]);

        Assert.Null(envelope.Payload.PreviousValues);
        Assert.NotNull(envelope.Payload.NewValues);
        Assert.Equal("New Description", envelope.Payload.NewValues[nameof(Project.Description)]);
    }

    [Fact]
    public async Task EditAsync_ConcurrencyConflict_DoesNotPersistOutboxWithBeforeAndAfterValues()
    {
        var db = nameof(EditAsync_ConcurrencyConflict_DoesNotPersistOutboxWithBeforeAndAfterValues);
        await using var setupContext = await CreateContextAsync(db);
        var setupData = new ClientData(setupContext, new ClientRepository(setupContext));
        var clientId = Guid.NewGuid();
        var client = CreateClient(clientId);
        var clientAuditFact = new EntityMutationAudited
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
        await setupData.CreateAsync(client, clientAuditFact, CancellationToken.None);

        var projectId = Guid.NewGuid();
        var project = CreateProject(projectId, clientId);
        await using var createContext = await CreateContextAsync(db);
        var createData = new ProjectData(createContext, new ProjectRepository(createContext));
        await createData.CreateAsync(project, CreateAuditFact(projectId), CancellationToken.None);

        await using var context = await CreateContextAsync(db);
        var data = new ProjectData(context, new ProjectRepository(context));
        var retrievedProject = await data.GetAsync(projectId, CancellationToken.None);

        // Modify through raw SQL to change the rowversion
        await context.Database.ExecuteSqlAsync($"UPDATE Projects SET Name = 'Modified' WHERE Id = {retrievedProject!.Id:D}");

        retrievedProject.Edit(
            name: "Updated Name",
            modifiedBy: "modifier-1",
            modifiedAtUtc: CreatedAtUtc.AddDays(1));

        var staleToken = Convert.ToBase64String(retrievedProject.RowVersion);
        var editAuditFact = new EntityMutationAudited
        {
            EventId = Guid.NewGuid().ToString(),
            OccurredAtUtc = new DateTimeOffset(CreatedAtUtc.AddDays(1)),
            SourceService = AuditSourceServices.Crm,
            EntityType = AuditEntityTypes.Project,
            EntityId = projectId,
            Action = AuditActions.Updated,
            ActorId = "user-1",
            ActorType = AuditActorTypes.User,
            TraceId = Guid.NewGuid().ToString("N"),
            CorrelationId = Guid.NewGuid().ToString(),
            CausationId = Guid.NewGuid().ToString(),
            ChangedFields = [nameof(Project.Name)],
            PreviousValues = new Dictionary<string, string>
            {
                { nameof(Project.Name), "Website Redesign" },
            },
            NewValues = new Dictionary<string, string>
            {
                { nameof(Project.Name), "Updated Name" },
            },
        };

        // Verify that the concurrency conflict prevents persistence
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
            () => data.EditAsync(
                retrievedProject,
                "modifier-1",
                CreatedAtUtc.AddDays(1),
                staleToken,
                editAuditFact,
                CancellationToken.None));

        // Verify that the outbox message was not persisted
        await using var verifyContext = await CreateContextAsync(db);
        Assert.False(await verifyContext.OutboxMessages.AnyAsync(m => m.Id == Guid.Parse(editAuditFact.EventId)));
    }
}

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

// Real SQL Server integration tests for TaskData's create transaction (TASK-001..022,
// DATA-001..005, AUDIT-001..008, OUTBOX-001/002; messaging.md publish-side test matrix: "state +
// outbox commit together" / "rollback removes both" / "rollback on validation"). Each test gets its
// own database inside the shared container (see MsSqlContainerFixture) so tests never interfere with
// each other despite sharing one running SQL Server instance.
public class TaskDataTests : IClassFixture<MsSqlContainerFixture>
{
    private readonly MsSqlContainerFixture _fixture;

    public TaskDataTests(MsSqlContainerFixture fixture)
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

    private static TaskItem CreateTask(Guid id, Guid projectId, string title = "Implement Authentication") =>
        TaskItem.Create(
            id: id,
            projectId: projectId,
            title: title,
            status: TaskItemStatus.Backlog,
            priority: TaskItemPriority.Normal,
            createdBy: "creator-1",
            createdAtUtc: CreatedAtUtc,
            description: "Add ASP.NET Core Identity integration",
            assignedUserId: null,
            startDateUtc: CreatedAtUtc.AddDays(1),
            dueDateUtc: CreatedAtUtc.AddDays(14));

    private static EntityMutationAudited CreateAuditFact(Guid taskId, Guid? eventId = null) => new()
    {
        EventId = (eventId ?? Guid.NewGuid()).ToString(),
        OccurredAtUtc = new DateTimeOffset(CreatedAtUtc),
        SourceService = AuditSourceServices.Crm,
        EntityType = AuditEntityTypes.Task,
        EntityId = taskId,
        Action = AuditActions.Created,
        ActorId = "user-1",
        ActorType = AuditActorTypes.User,
        TraceId = Guid.NewGuid().ToString("N"),
        CorrelationId = Guid.NewGuid().ToString(),
        CausationId = Guid.NewGuid().ToString(),
        ChangedFields = ["Title", "Status", "Priority"],
    };

    [Fact]
    public async Task CreateAsync_WhenProjectExists_PersistsTheTaskAndOneOutboxMessage_InTheSameCommit()
    {
        var db = nameof(CreateAsync_WhenProjectExists_PersistsTheTaskAndOneOutboxMessage_InTheSameCommit);
        await using var setupContext = await CreateContextAsync(db);
        var clientId = Guid.NewGuid();
        var client = CreateClient(clientId);
        var setupClientData = new ClientData(setupContext, new ClientRepository(setupContext));
        await setupClientData.CreateAsync(client, new EntityMutationAudited
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

        var projectId = Guid.NewGuid();
        var project = CreateProject(projectId, clientId);
        var setupProjectData = new ProjectData(setupContext, new ProjectRepository(setupContext));
        await setupProjectData.CreateAsync(project, new EntityMutationAudited
        {
            EventId = Guid.NewGuid().ToString(),
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
        }, CancellationToken.None);

        await using var context = await CreateContextAsync(db);
        var data = new TaskData(context, new TaskRepository(context));
        var taskId = Guid.NewGuid();
        var task = CreateTask(taskId, projectId);
        var auditFact = CreateAuditFact(taskId);

        await data.CreateAsync(task, auditFact, CancellationToken.None);

        await using var verifyContext = await CreateContextAsync(db);
        var persistedTask = await verifyContext.Tasks.SingleAsync(t => t.Id == taskId);
        Assert.Equal("Implement Authentication", persistedTask.Title);
        Assert.Equal(projectId, persistedTask.ProjectId);

        var persistedOutbox = await verifyContext.OutboxMessages.SingleAsync(m => m.Id == Guid.Parse(auditFact.EventId));
        Assert.Equal("Audit.EntityMutationAudited", persistedOutbox.ContractType);
        Assert.Equal(EntityMutationAudited.CurrentVersion, persistedOutbox.ContractVersion);
        Assert.Equal(OutboxMessageStatus.Pending, persistedOutbox.Status);
    }

    [Fact]
    public async Task CreateAsync_WhenProjectDoesNotExist_ThrowsTaskProjectNotFoundException_WithoutPersistingAnything()
    {
        var db = nameof(CreateAsync_WhenProjectDoesNotExist_ThrowsTaskProjectNotFoundException_WithoutPersistingAnything);
        await using var context = await CreateContextAsync(db);
        var data = new TaskData(context, new TaskRepository(context));
        var taskId = Guid.NewGuid();
        var nonexistentProjectId = Guid.NewGuid();
        var task = CreateTask(taskId, nonexistentProjectId);
        var auditFact = CreateAuditFact(taskId);

        await Assert.ThrowsAsync<TaskProjectNotFoundException>(
            () => data.CreateAsync(task, auditFact, CancellationToken.None));

        await using var verifyContext = await CreateContextAsync(db);
        Assert.False(await verifyContext.Tasks.AnyAsync(t => t.Id == taskId));
        Assert.False(await verifyContext.OutboxMessages.AnyAsync(m => m.Id == Guid.Parse(auditFact.EventId)));
    }

    [Fact]
    public async Task CreateAsync_PreservesActorAndCorrelationMetadata_OnTheOutboxRow()
    {
        var db = nameof(CreateAsync_PreservesActorAndCorrelationMetadata_OnTheOutboxRow);
        await using var setupContext = await CreateContextAsync(db);
        var clientId = Guid.NewGuid();
        var client = CreateClient(clientId);
        var setupClientData = new ClientData(setupContext, new ClientRepository(setupContext));
        await setupClientData.CreateAsync(client, new EntityMutationAudited
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

        var projectId = Guid.NewGuid();
        var project = CreateProject(projectId, clientId);
        var setupProjectData = new ProjectData(setupContext, new ProjectRepository(setupContext));
        await setupProjectData.CreateAsync(project, new EntityMutationAudited
        {
            EventId = Guid.NewGuid().ToString(),
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
        }, CancellationToken.None);

        await using var context = await CreateContextAsync(db);
        var data = new TaskData(context, new TaskRepository(context));
        var taskId = Guid.NewGuid();
        var task = CreateTask(taskId, projectId);
        var auditFact = CreateAuditFact(taskId);

        await data.CreateAsync(task, auditFact, CancellationToken.None);

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
    public async Task CreateAsync_WhenTheTaskInsertFails_RollsBackTheOutboxMessageToo()
    {
        // Proves atomicity: the second attempt's OutboxMessage has its own fresh, non-conflicting
        // Id, yet must still disappear because it was staged on the same SaveChangesAsync call as
        // the Task insert that fails (messaging.md test matrix: "rollback removes both").
        var db = nameof(CreateAsync_WhenTheTaskInsertFails_RollsBackTheOutboxMessageToo);
        await using var setupContext = await CreateContextAsync(db);
        var clientId = Guid.NewGuid();
        var client = CreateClient(clientId);
        var setupClientData = new ClientData(setupContext, new ClientRepository(setupContext));
        await setupClientData.CreateAsync(client, new EntityMutationAudited
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

        var projectId = Guid.NewGuid();
        var project = CreateProject(projectId, clientId);
        var setupProjectData = new ProjectData(setupContext, new ProjectRepository(setupContext));
        await setupProjectData.CreateAsync(project, new EntityMutationAudited
        {
            EventId = Guid.NewGuid().ToString(),
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
        }, CancellationToken.None);

        await using var context = await CreateContextAsync(db);
        var data = new TaskData(context, new TaskRepository(context));
        var taskId = Guid.NewGuid();

        var firstAuditFact = CreateAuditFact(taskId);
        await data.CreateAsync(CreateTask(taskId, projectId, "First Task"), firstAuditFact, CancellationToken.None);

        await using var conflictingContext = await CreateContextAsync(db);
        var conflictingData = new TaskData(conflictingContext, new TaskRepository(conflictingContext));
        var conflictingTask = CreateTask(taskId, projectId, "Duplicate Task");
        var secondAuditFact = CreateAuditFact(taskId);

        await Assert.ThrowsAsync<DbUpdateException>(
            () => conflictingData.CreateAsync(conflictingTask, secondAuditFact, CancellationToken.None));

        await using var verifyContext = await CreateContextAsync(db);
        var persistedTask = await verifyContext.Tasks.SingleAsync(t => t.Id == taskId);
        Assert.Equal("First Task", persistedTask.Title);

        Assert.False(await verifyContext.OutboxMessages.AnyAsync(m => m.Id == Guid.Parse(secondAuditFact.EventId)));

        var taskOutboxCount = await verifyContext.OutboxMessages.CountAsync(m => m.Id == Guid.Parse(firstAuditFact.EventId));
        Assert.Equal(1, taskOutboxCount);
    }

    [Fact]
    public async Task CreateAsync_WithANonGuidEventId_ThrowsBeforePersistingAnything()
    {
        var db = nameof(CreateAsync_WithANonGuidEventId_ThrowsBeforePersistingAnything);
        await using var setupContext = await CreateContextAsync(db);
        var clientId = Guid.NewGuid();
        var client = CreateClient(clientId);
        var setupClientData = new ClientData(setupContext, new ClientRepository(setupContext));
        await setupClientData.CreateAsync(client, new EntityMutationAudited
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

        var projectId = Guid.NewGuid();
        var project = CreateProject(projectId, clientId);
        var setupProjectData = new ProjectData(setupContext, new ProjectRepository(setupContext));
        await setupProjectData.CreateAsync(project, new EntityMutationAudited
        {
            EventId = Guid.NewGuid().ToString(),
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
        }, CancellationToken.None);

        await using var context = await CreateContextAsync(db);
        var data = new TaskData(context, new TaskRepository(context));
        var taskId = Guid.NewGuid();
        var task = CreateTask(taskId, projectId);
        var auditFact = CreateAuditFact(taskId) with { EventId = "not-a-guid" };

        await Assert.ThrowsAsync<ArgumentException>(
            () => data.CreateAsync(task, auditFact, CancellationToken.None));

        await using var verifyContext = await CreateContextAsync(db);
        Assert.False(await verifyContext.Tasks.AnyAsync(t => t.Id == taskId));
        // Task creation failed: no task persisted, and no outbox message for this (invalid eventId)
        // task was added. The setup client + project outbox messages remain.
        var outboxMessages = await verifyContext.OutboxMessages.ToListAsync();
        Assert.Equal(2, outboxMessages.Count);
        Assert.DoesNotContain(outboxMessages, m => m.ContractType == "Audit.EntityMutationAudited" && m.CorrelationId == auditFact.CorrelationId);
    }

    #region AssignAsync Tests

    [Fact]
    public async Task AssignAsync_WithInitialAssignment_PersistsTaskAssignmentAndOutboxMessage()
    {
        // Arrange
        var db = nameof(AssignAsync_WithInitialAssignment_PersistsTaskAssignmentAndOutboxMessage);
        await using var setupContext = await CreateContextAsync(db);
        var clientId = Guid.NewGuid();
        var client = CreateClient(clientId);
        var setupClientData = new ClientData(setupContext, new ClientRepository(setupContext));
        await setupClientData.CreateAsync(client, new EntityMutationAudited
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

        var projectId = Guid.NewGuid();
        var project = CreateProject(projectId, clientId);
        var setupProjectData = new ProjectData(setupContext, new ProjectRepository(setupContext));
        await setupProjectData.CreateAsync(project, new EntityMutationAudited
        {
            EventId = Guid.NewGuid().ToString(),
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
        }, CancellationToken.None);

        // Create a task without assignment
        await using var createContext = await CreateContextAsync(db);
        var createData = new TaskData(createContext, new TaskRepository(createContext));
        var taskId = Guid.NewGuid();
        var task = CreateTask(taskId, projectId);
        var createAuditFact = CreateAuditFact(taskId);
        await createData.CreateAsync(task, createAuditFact, CancellationToken.None);

        // Now fetch and assign the task
        await using var assignContext = await CreateContextAsync(db);
        var assignData = new TaskData(assignContext, new TaskRepository(assignContext));
        var fetchedTask = await assignData.GetByIdAsync(taskId, CancellationToken.None);
        Assert.NotNull(fetchedTask);
        Assert.Null(fetchedTask.AssignedUserId);

        // Apply assignment
        var (previousUserId, newUserId) = fetchedTask.SetAssigned("assigned-user-123", "assigner-1", CreatedAtUtc.AddHours(1));
        Assert.Null(previousUserId);
        Assert.Equal("assigned-user-123", newUserId);

        // Persist assignment with audit fact
        var assignAuditFact = new EntityMutationAudited
        {
            EventId = Guid.NewGuid().ToString(),
            OccurredAtUtc = new DateTimeOffset(CreatedAtUtc.AddHours(1)),
            SourceService = AuditSourceServices.Crm,
            EntityType = AuditEntityTypes.Task,
            EntityId = taskId,
            Action = AuditActions.Assigned,
            ActorId = "assigner-1",
            ActorType = AuditActorTypes.User,
            TraceId = Guid.NewGuid().ToString("N"),
            CorrelationId = Guid.NewGuid().ToString(),
            CausationId = Guid.NewGuid().ToString(),
            ChangedFields = [nameof(TaskItem.AssignedUserId)],
            NewValues = new Dictionary<string, string> { { nameof(TaskItem.AssignedUserId), "assigned-user-123" } },
        };

        await assignData.AssignAsync(fetchedTask, assignAuditFact, CancellationToken.None);

        // Verify persistence
        await using var verifyContext = await CreateContextAsync(db);
        var persistedTask = await verifyContext.Tasks.SingleAsync(t => t.Id == taskId);
        Assert.Equal("assigned-user-123", persistedTask.AssignedUserId);
        Assert.Equal("assigner-1", persistedTask.LastModifiedBy);

        var assignOutbox = await verifyContext.OutboxMessages.SingleAsync(m => m.ContractType == "Audit.EntityMutationAudited" && m.CorrelationId == assignAuditFact.CorrelationId);
        Assert.Equal("Audit.EntityMutationAudited", assignOutbox.ContractType);
        Assert.Equal(EntityMutationAudited.CurrentVersion, assignOutbox.ContractVersion);
    }

    [Fact]
    public async Task AssignAsync_WithReassignment_TracksBeforeAndAfterValues()
    {
        // Arrange
        var db = nameof(AssignAsync_WithReassignment_TracksBeforeAndAfterValues);
        await using var setupContext = await CreateContextAsync(db);
        var clientId = Guid.NewGuid();
        var client = CreateClient(clientId);
        var setupClientData = new ClientData(setupContext, new ClientRepository(setupContext));
        await setupClientData.CreateAsync(client, new EntityMutationAudited
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

        var projectId = Guid.NewGuid();
        var project = CreateProject(projectId, clientId);
        var setupProjectData = new ProjectData(setupContext, new ProjectRepository(setupContext));
        await setupProjectData.CreateAsync(project, new EntityMutationAudited
        {
            EventId = Guid.NewGuid().ToString(),
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
        }, CancellationToken.None);

        // Create a task with initial assignment
        await using var createContext = await CreateContextAsync(db);
        var createData = new TaskData(createContext, new TaskRepository(createContext));
        var taskId = Guid.NewGuid();
        var task = CreateTask(taskId, projectId, "Implementation Task");
        task.SetAssigned("original-user", "creator-1", CreatedAtUtc);
        var createAuditFact = CreateAuditFact(taskId);
        await createData.CreateAsync(task, createAuditFact, CancellationToken.None);

        // Fetch and reassign
        await using var reassignContext = await CreateContextAsync(db);
        var reassignData = new TaskData(reassignContext, new TaskRepository(reassignContext));
        var fetchedTask = await reassignData.GetByIdAsync(taskId, CancellationToken.None);
        Assert.NotNull(fetchedTask);
        Assert.Equal("original-user", fetchedTask.AssignedUserId);

        var (previousUserId, newUserId) = fetchedTask.SetReassigned("new-user", "reassigner-1", CreatedAtUtc.AddHours(2));
        Assert.Equal("original-user", previousUserId);
        Assert.Equal("new-user", newUserId);

        var reassignAuditFact = new EntityMutationAudited
        {
            EventId = Guid.NewGuid().ToString(),
            OccurredAtUtc = new DateTimeOffset(CreatedAtUtc.AddHours(2)),
            SourceService = AuditSourceServices.Crm,
            EntityType = AuditEntityTypes.Task,
            EntityId = taskId,
            Action = AuditActions.Reassigned,
            ActorId = "reassigner-1",
            ActorType = AuditActorTypes.User,
            TraceId = Guid.NewGuid().ToString("N"),
            CorrelationId = Guid.NewGuid().ToString(),
            CausationId = Guid.NewGuid().ToString(),
            ChangedFields = [nameof(TaskItem.AssignedUserId)],
            PreviousValues = new Dictionary<string, string> { { nameof(TaskItem.AssignedUserId), "original-user" } },
            NewValues = new Dictionary<string, string> { { nameof(TaskItem.AssignedUserId), "new-user" } },
        };

        await reassignData.AssignAsync(fetchedTask, reassignAuditFact, CancellationToken.None);

        // Verify
        await using var verifyContext = await CreateContextAsync(db);
        var persistedTask = await verifyContext.Tasks.SingleAsync(t => t.Id == taskId);
        Assert.Equal("new-user", persistedTask.AssignedUserId);

        var reassignOutbox = await verifyContext.OutboxMessages.SingleAsync(m => m.ContractType == "Audit.EntityMutationAudited" && m.CorrelationId == reassignAuditFact.CorrelationId);
        Assert.Equal("Audit.EntityMutationAudited", reassignOutbox.ContractType);
    }

    #endregion

    #region ChangePriorityAsync Tests

    [Fact]
    public async Task ChangePriorityAsync_WithValidPriorityChange_PersistsTaskPriorityAndOutboxMessage()
    {
        // Arrange
        var db = nameof(ChangePriorityAsync_WithValidPriorityChange_PersistsTaskPriorityAndOutboxMessage);
        await using var setupContext = await CreateContextAsync(db);
        var clientId = Guid.NewGuid();
        var client = CreateClient(clientId);
        var setupClientData = new ClientData(setupContext, new ClientRepository(setupContext));
        await setupClientData.CreateAsync(client, new EntityMutationAudited
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

        var projectId = Guid.NewGuid();
        var project = CreateProject(projectId, clientId);
        var setupProjectData = new ProjectData(setupContext, new ProjectRepository(setupContext));
        await setupProjectData.CreateAsync(project, new EntityMutationAudited
        {
            EventId = Guid.NewGuid().ToString(),
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
        }, CancellationToken.None);

        // Create a task with Normal priority
        await using var createContext = await CreateContextAsync(db);
        var createData = new TaskData(createContext, new TaskRepository(createContext));
        var taskId = Guid.NewGuid();
        var task = CreateTask(taskId, projectId);
        Assert.Equal(TaskItemPriority.Normal, task.Priority);
        var createAuditFact = CreateAuditFact(taskId);
        await createData.CreateAsync(task, createAuditFact, CancellationToken.None);

        // Now fetch and change priority to High
        await using var changeContext = await CreateContextAsync(db);
        var changeData = new TaskData(changeContext, new TaskRepository(changeContext));
        var fetchedTask = await changeData.GetByIdAsync(taskId, CancellationToken.None);
        Assert.NotNull(fetchedTask);
        Assert.Equal(TaskItemPriority.Normal, fetchedTask.Priority);

        // Apply priority change
        var (previousPriority, newPriority) = fetchedTask.SetPriority(TaskItemPriority.High, "priority-changer-1", CreatedAtUtc.AddHours(1));
        Assert.Equal(TaskItemPriority.Normal, previousPriority);
        Assert.Equal(TaskItemPriority.High, newPriority);

        // Persist priority change with audit fact
        var priorityAuditFact = new EntityMutationAudited
        {
            EventId = Guid.NewGuid().ToString(),
            OccurredAtUtc = new DateTimeOffset(CreatedAtUtc.AddHours(1)),
            SourceService = AuditSourceServices.Crm,
            EntityType = AuditEntityTypes.Task,
            EntityId = taskId,
            Action = AuditActions.PriorityChanged,
            ActorId = "priority-changer-1",
            ActorType = AuditActorTypes.User,
            TraceId = Guid.NewGuid().ToString("N"),
            CorrelationId = Guid.NewGuid().ToString(),
            CausationId = Guid.NewGuid().ToString(),
            ChangedFields = [nameof(TaskItem.Priority)],
            PreviousValues = new Dictionary<string, string> { { nameof(TaskItem.Priority), TaskItemPriority.Normal.ToString() } },
            NewValues = new Dictionary<string, string> { { nameof(TaskItem.Priority), TaskItemPriority.High.ToString() } },
        };

        await changeData.ChangePriorityAsync(fetchedTask, priorityAuditFact, CancellationToken.None);

        // Verify persistence
        await using var verifyContext = await CreateContextAsync(db);
        var persistedTask = await verifyContext.Tasks.SingleAsync(t => t.Id == taskId);
        Assert.Equal(TaskItemPriority.High, persistedTask.Priority);
        Assert.Equal("priority-changer-1", persistedTask.LastModifiedBy);

        var priorityOutbox = await verifyContext.OutboxMessages.SingleAsync(m => m.ContractType == "Audit.EntityMutationAudited" && m.CorrelationId == priorityAuditFact.CorrelationId);
        Assert.Equal("Audit.EntityMutationAudited", priorityOutbox.ContractType);
        Assert.Equal(EntityMutationAudited.CurrentVersion, priorityOutbox.ContractVersion);
    }

    [Fact]
    public async Task ChangePriorityAsync_WithConcurrencyConflict_ThrowsDbUpdateConcurrencyException()
    {
        // Arrange
        var db = nameof(ChangePriorityAsync_WithConcurrencyConflict_ThrowsDbUpdateConcurrencyException);
        await using var setupContext = await CreateContextAsync(db);
        var clientId = Guid.NewGuid();
        var client = CreateClient(clientId);
        var setupClientData = new ClientData(setupContext, new ClientRepository(setupContext));
        await setupClientData.CreateAsync(client, new EntityMutationAudited
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

        var projectId = Guid.NewGuid();
        var project = CreateProject(projectId, clientId);
        var setupProjectData = new ProjectData(setupContext, new ProjectRepository(setupContext));
        await setupProjectData.CreateAsync(project, new EntityMutationAudited
        {
            EventId = Guid.NewGuid().ToString(),
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
        }, CancellationToken.None);

        // Create a task
        await using var createContext = await CreateContextAsync(db);
        var createData = new TaskData(createContext, new TaskRepository(createContext));
        var taskId = Guid.NewGuid();
        var task = CreateTask(taskId, projectId);
        var createAuditFact = CreateAuditFact(taskId);
        await createData.CreateAsync(task, createAuditFact, CancellationToken.None);

        // Fetch the task in one context
        await using var firstContext = await CreateContextAsync(db);
        var firstData = new TaskData(firstContext, new TaskRepository(firstContext));
        var firstFetchedTask = await firstData.GetByIdAsync(taskId, CancellationToken.None);
        Assert.NotNull(firstFetchedTask);
        var firstRowVersion = firstFetchedTask.RowVersion;

        // Fetch and modify in a second context to change the RowVersion
        await using var secondContext = await CreateContextAsync(db);
        var secondData = new TaskData(secondContext, new TaskRepository(secondContext));
        var secondFetchedTask = await secondData.GetByIdAsync(taskId, CancellationToken.None);
        Assert.NotNull(secondFetchedTask);
        secondFetchedTask.SetPriority(TaskItemPriority.High, "modifier-1", CreatedAtUtc.AddHours(1));
        var secondAuditFact = new EntityMutationAudited
        {
            EventId = Guid.NewGuid().ToString(),
            OccurredAtUtc = new DateTimeOffset(CreatedAtUtc.AddHours(1)),
            SourceService = AuditSourceServices.Crm,
            EntityType = AuditEntityTypes.Task,
            EntityId = taskId,
            Action = AuditActions.PriorityChanged,
            ActorId = "modifier-1",
            ActorType = AuditActorTypes.User,
            TraceId = Guid.NewGuid().ToString("N"),
            CorrelationId = Guid.NewGuid().ToString(),
            CausationId = Guid.NewGuid().ToString(),
            ChangedFields = [nameof(TaskItem.Priority)],
            PreviousValues = new Dictionary<string, string> { { nameof(TaskItem.Priority), TaskItemPriority.Normal.ToString() } },
            NewValues = new Dictionary<string, string> { { nameof(TaskItem.Priority), TaskItemPriority.High.ToString() } },
        };
        await secondData.ChangePriorityAsync(secondFetchedTask, secondAuditFact, CancellationToken.None);

        // Now try to modify with the stale RowVersion from the first context
        firstFetchedTask.SetPriority(TaskItemPriority.Critical, "modifier-2", CreatedAtUtc.AddHours(2));
        firstFetchedTask.RowVersion = firstRowVersion; // Use the stale version
        var firstAuditFact = new EntityMutationAudited
        {
            EventId = Guid.NewGuid().ToString(),
            OccurredAtUtc = new DateTimeOffset(CreatedAtUtc.AddHours(2)),
            SourceService = AuditSourceServices.Crm,
            EntityType = AuditEntityTypes.Task,
            EntityId = taskId,
            Action = AuditActions.PriorityChanged,
            ActorId = "modifier-2",
            ActorType = AuditActorTypes.User,
            TraceId = Guid.NewGuid().ToString("N"),
            CorrelationId = Guid.NewGuid().ToString(),
            CausationId = Guid.NewGuid().ToString(),
            ChangedFields = [nameof(TaskItem.Priority)],
            PreviousValues = new Dictionary<string, string> { { nameof(TaskItem.Priority), TaskItemPriority.Normal.ToString() } },
            NewValues = new Dictionary<string, string> { { nameof(TaskItem.Priority), TaskItemPriority.Critical.ToString() } },
        };

        // This should throw DbUpdateConcurrencyException because RowVersion has changed
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
            firstData.ChangePriorityAsync(firstFetchedTask, firstAuditFact, CancellationToken.None));
    }

    [Fact]
    public async Task ChangePriorityAsync_TracksBeforeAndAfterPriorityValues()
    {
        // Arrange
        var db = nameof(ChangePriorityAsync_TracksBeforeAndAfterPriorityValues);
        await using var setupContext = await CreateContextAsync(db);
        var clientId = Guid.NewGuid();
        var client = CreateClient(clientId);
        var setupClientData = new ClientData(setupContext, new ClientRepository(setupContext));
        await setupClientData.CreateAsync(client, new EntityMutationAudited
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

        var projectId = Guid.NewGuid();
        var project = CreateProject(projectId, clientId);
        var setupProjectData = new ProjectData(setupContext, new ProjectRepository(setupContext));
        await setupProjectData.CreateAsync(project, new EntityMutationAudited
        {
            EventId = Guid.NewGuid().ToString(),
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
        }, CancellationToken.None);

        // Create a task with Low priority
        await using var createContext = await CreateContextAsync(db);
        var createData = new TaskData(createContext, new TaskRepository(createContext));
        var taskId = Guid.NewGuid();
        var taskWithLowPriority = TaskItem.Create(
            id: taskId,
            projectId: projectId,
            title: "Test Task",
            status: TaskItemStatus.Backlog,
            priority: TaskItemPriority.Low,
            createdBy: "creator-1",
            createdAtUtc: CreatedAtUtc);
        var createAuditFact = CreateAuditFact(taskId);
        await createData.CreateAsync(taskWithLowPriority, createAuditFact, CancellationToken.None);

        // Fetch and change priority from Low to Critical
        await using var changeContext = await CreateContextAsync(db);
        var changeData = new TaskData(changeContext, new TaskRepository(changeContext));
        var fetchedTask = await changeData.GetByIdAsync(taskId, CancellationToken.None);
        Assert.NotNull(fetchedTask);
        Assert.Equal(TaskItemPriority.Low, fetchedTask.Priority);

        var (previousPriority, newPriority) = fetchedTask.SetPriority(TaskItemPriority.Critical, "priority-changer-1", CreatedAtUtc.AddHours(1));
        Assert.Equal(TaskItemPriority.Low, previousPriority);
        Assert.Equal(TaskItemPriority.Critical, newPriority);

        var priorityAuditFact = new EntityMutationAudited
        {
            EventId = Guid.NewGuid().ToString(),
            OccurredAtUtc = new DateTimeOffset(CreatedAtUtc.AddHours(1)),
            SourceService = AuditSourceServices.Crm,
            EntityType = AuditEntityTypes.Task,
            EntityId = taskId,
            Action = AuditActions.PriorityChanged,
            ActorId = "priority-changer-1",
            ActorType = AuditActorTypes.User,
            TraceId = Guid.NewGuid().ToString("N"),
            CorrelationId = Guid.NewGuid().ToString(),
            CausationId = Guid.NewGuid().ToString(),
            ChangedFields = [nameof(TaskItem.Priority)],
            PreviousValues = new Dictionary<string, string> { { nameof(TaskItem.Priority), TaskItemPriority.Low.ToString() } },
            NewValues = new Dictionary<string, string> { { nameof(TaskItem.Priority), TaskItemPriority.Critical.ToString() } },
        };

        await changeData.ChangePriorityAsync(fetchedTask, priorityAuditFact, CancellationToken.None);

        // Verify
        await using var verifyContext = await CreateContextAsync(db);
        var persistedTask = await verifyContext.Tasks.SingleAsync(t => t.Id == taskId);
        Assert.Equal(TaskItemPriority.Critical, persistedTask.Priority);

        var priorityOutbox = await verifyContext.OutboxMessages.SingleAsync(m => m.ContractType == "Audit.EntityMutationAudited" && m.CorrelationId == priorityAuditFact.CorrelationId);
        var envelope = EventEnvelopeSerializer.Deserialize<EntityMutationAudited>(
            priorityOutbox.Payload, [EntityMutationAudited.CurrentVersion]);
        Assert.NotNull(envelope.Payload.PreviousValues);
        Assert.Equal(TaskItemPriority.Low.ToString(), envelope.Payload.PreviousValues[nameof(TaskItem.Priority)]);
        Assert.NotNull(envelope.Payload.NewValues);
        Assert.Equal(TaskItemPriority.Critical.ToString(), envelope.Payload.NewValues[nameof(TaskItem.Priority)]);
    }

    #endregion

    #region EditAsync

    [Fact]
    public async Task EditAsync_UpdateTaskTitle_PersistsChangesAndOutboxMessage()
    {
        // Arrange
        var db = nameof(EditAsync_UpdateTaskTitle_PersistsChangesAndOutboxMessage);
        var clientId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var taskId = Guid.NewGuid();

        await using (var setupContext = await CreateContextAsync(db))
        {
            var client = CreateClient(clientId, "Test Client");
            var project = CreateProject(projectId, clientId, "Test Project");
            var task = TaskItem.Create(
                id: taskId,
                projectId: projectId,
                title: "Original Title",
                status: TaskItemStatus.Backlog,
                priority: TaskItemPriority.Normal,
                createdBy: "creator",
                createdAtUtc: CreatedAtUtc);

            setupContext.Clients.Add(client);
            setupContext.Projects.Add(project);
            setupContext.Tasks.Add(task);
            await setupContext.SaveChangesAsync();
        }

        await using (var updateContext = await CreateContextAsync(db))
        {
            var fetchedTask = await updateContext.Tasks.SingleAsync(t => t.Id == taskId);
            fetchedTask.RowVersion = [1, 2, 3];

            var updateData = new TaskData(updateContext, new TaskRepository(updateContext));
            // Apply the edit mutation using the Edit method
            var modifiedAtUtc = DateTime.UtcNow;
            var changes = fetchedTask.Edit(
                newTitle: "Updated Title",
                newDescription: null,
                newStartDateUtc: null,
                newDueDateUtc: null,
                newNotes: null,
                modifiedBy: "editor-1",
                modifiedAtUtc: modifiedAtUtc);

            var editAuditFact = new EntityMutationAudited
            {
                EventId = Guid.NewGuid().ToString(),
                OccurredAtUtc = new DateTimeOffset(modifiedAtUtc),
                SourceService = AuditSourceServices.Crm,
                EntityType = AuditEntityTypes.Task,
                EntityId = taskId,
                Action = AuditActions.Updated,
                ActorId = "editor-1",
                ActorType = AuditActorTypes.User,
                TraceId = "trace-edit-1",
                CorrelationId = "corr-edit-1",
                CausationId = "cause-edit-1",
                ChangedFields = [nameof(TaskItem.Title)],
                PreviousValues = new Dictionary<string, string> { { nameof(TaskItem.Title), changes.PreviousTitle ?? string.Empty } },
                NewValues = new Dictionary<string, string> { { nameof(TaskItem.Title), changes.NewTitle ?? string.Empty } },
            };

            // Act
            await updateData.EditAsync(fetchedTask, editAuditFact, CancellationToken.None);

            // Verify
            await using var verifyContext = await CreateContextAsync(db);
            var persistedTask = await verifyContext.Tasks.SingleAsync(t => t.Id == taskId);
            Assert.Equal("Updated Title", persistedTask.Title);

            var editOutbox = await verifyContext.OutboxMessages.SingleAsync(
                m => m.ContractType == "Audit.EntityMutationAudited" && m.CorrelationId == "corr-edit-1");
            var envelope = EventEnvelopeSerializer.Deserialize<EntityMutationAudited>(
                editOutbox.Payload, [EntityMutationAudited.CurrentVersion]);
            Assert.Equal(AuditActions.Updated, envelope.Payload.Action);
            Assert.Contains(nameof(TaskItem.Title), envelope.Payload.ChangedFields);
        }
    }

    [Fact]
    public async Task EditAsync_UpdateMultipleFields_PersistsAllChanges()
    {
        // Arrange
        var db = nameof(EditAsync_UpdateMultipleFields_PersistsAllChanges);
        var clientId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var newDueDate = CreatedAtUtc.AddDays(14);

        await using (var setupContext = await CreateContextAsync(db))
        {
            var client = CreateClient(clientId, "Test Client");
            var project = CreateProject(projectId, clientId, "Test Project");
            var task = TaskItem.Create(
                id: taskId,
                projectId: projectId,
                title: "Original Title",
                status: TaskItemStatus.Backlog,
                priority: TaskItemPriority.Normal,
                createdBy: "creator",
                createdAtUtc: CreatedAtUtc,
                description: "Old description",
                dueDateUtc: CreatedAtUtc.AddDays(7));

            setupContext.Clients.Add(client);
            setupContext.Projects.Add(project);
            setupContext.Tasks.Add(task);
            await setupContext.SaveChangesAsync();
        }

        await using (var updateContext = await CreateContextAsync(db))
        {
            var fetchedTask = await updateContext.Tasks.SingleAsync(t => t.Id == taskId);
            fetchedTask.RowVersion = [1, 2, 3];

            var updateData = new TaskData(updateContext, new TaskRepository(updateContext));
            // Apply the edit mutation using the Edit method
            var modifiedAtUtc = DateTime.UtcNow;
            var changes = fetchedTask.Edit(
                newTitle: "New Title",
                newDescription: "New description",
                newStartDateUtc: null,
                newDueDateUtc: newDueDate,
                newNotes: "Added notes",
                modifiedBy: "editor-2",
                modifiedAtUtc: modifiedAtUtc);

            var editAuditFact = new EntityMutationAudited
            {
                EventId = Guid.NewGuid().ToString(),
                OccurredAtUtc = new DateTimeOffset(modifiedAtUtc),
                SourceService = AuditSourceServices.Crm,
                EntityType = AuditEntityTypes.Task,
                EntityId = taskId,
                Action = AuditActions.Updated,
                ActorId = "editor-2",
                ActorType = AuditActorTypes.User,
                TraceId = "trace-edit-2",
                CorrelationId = "corr-edit-2",
                CausationId = "cause-edit-2",
                ChangedFields = [nameof(TaskItem.Title), nameof(TaskItem.Description), nameof(TaskItem.DueDateUtc), nameof(TaskItem.Notes)],
                PreviousValues = new Dictionary<string, string>
                {
                    { nameof(TaskItem.Title), changes.PreviousTitle ?? string.Empty },
                    { nameof(TaskItem.Description), changes.PreviousDescription ?? string.Empty },
                    { nameof(TaskItem.DueDateUtc), changes.PreviousDueDateUtc?.ToString("O") ?? string.Empty },
                    { nameof(TaskItem.Notes), changes.PreviousNotes ?? string.Empty },
                },
                NewValues = new Dictionary<string, string>
                {
                    { nameof(TaskItem.Title), changes.NewTitle ?? string.Empty },
                    { nameof(TaskItem.Description), changes.NewDescription ?? string.Empty },
                    { nameof(TaskItem.DueDateUtc), changes.NewDueDateUtc?.ToString("O") ?? string.Empty },
                    { nameof(TaskItem.Notes), changes.NewNotes ?? string.Empty },
                },
            };

            // Act
            await updateData.EditAsync(fetchedTask, editAuditFact, CancellationToken.None);

            // Verify
            await using var verifyContext = await CreateContextAsync(db);
            var persistedTask = await verifyContext.Tasks.SingleAsync(t => t.Id == taskId);
            Assert.Equal("New Title", persistedTask.Title);
            Assert.Equal("New description", persistedTask.Description);
            Assert.Equal(newDueDate, persistedTask.DueDateUtc);
            Assert.Equal("Added notes", persistedTask.Notes);

            var editOutbox = await verifyContext.OutboxMessages.SingleAsync(
                m => m.ContractType == "Audit.EntityMutationAudited" && m.CorrelationId == "corr-edit-2");
            Assert.NotNull(editOutbox);
        }
    }

    [Fact]
    public async Task EditAsync_ClearDescription_PersistsNullDescription()
    {
        // Arrange
        var db = nameof(EditAsync_ClearDescription_PersistsNullDescription);
        var clientId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var taskId = Guid.NewGuid();

        await using (var setupContext = await CreateContextAsync(db))
        {
            var client = CreateClient(clientId, "Test Client");
            var project = CreateProject(projectId, clientId, "Test Project");
            var task = TaskItem.Create(
                id: taskId,
                projectId: projectId,
                title: "Test Task",
                status: TaskItemStatus.Backlog,
                priority: TaskItemPriority.Normal,
                createdBy: "creator",
                createdAtUtc: CreatedAtUtc,
                description: "Old description");

            setupContext.Clients.Add(client);
            setupContext.Projects.Add(project);
            setupContext.Tasks.Add(task);
            await setupContext.SaveChangesAsync();
        }

        await using (var updateContext = await CreateContextAsync(db))
        {
            var fetchedTask = await updateContext.Tasks.SingleAsync(t => t.Id == taskId);
            fetchedTask.RowVersion = [1, 2, 3];

            var updateData = new TaskData(updateContext, new TaskRepository(updateContext));
            // Apply the edit mutation using the Edit method
            var modifiedAtUtc = DateTime.UtcNow;
            var changes = fetchedTask.Edit(
                newTitle: null,
                newDescription: "", // Empty string to clear
                newStartDateUtc: null,
                newDueDateUtc: null,
                newNotes: null,
                modifiedBy: "editor-3",
                modifiedAtUtc: modifiedAtUtc);

            var editAuditFact = new EntityMutationAudited
            {
                EventId = Guid.NewGuid().ToString(),
                OccurredAtUtc = new DateTimeOffset(modifiedAtUtc),
                SourceService = AuditSourceServices.Crm,
                EntityType = AuditEntityTypes.Task,
                EntityId = taskId,
                Action = AuditActions.Updated,
                ActorId = "editor-3",
                ActorType = AuditActorTypes.User,
                TraceId = "trace-edit-3",
                CorrelationId = "corr-edit-3",
                CausationId = "cause-edit-3",
                ChangedFields = [nameof(TaskItem.Description)],
                PreviousValues = new Dictionary<string, string> { { nameof(TaskItem.Description), changes.PreviousDescription ?? string.Empty } },
                NewValues = new Dictionary<string, string> { { nameof(TaskItem.Description), changes.NewDescription ?? string.Empty } },
            };

            // Act
            await updateData.EditAsync(fetchedTask, editAuditFact, CancellationToken.None);

            // Verify
            await using var verifyContext = await CreateContextAsync(db);
            var persistedTask = await verifyContext.Tasks.SingleAsync(t => t.Id == taskId);
            Assert.Null(persistedTask.Description);
        }
    }

    [Fact]
    public async Task EditAsync_ConcurrencyConflict_ThrowsDbUpdateConcurrencyException()
    {
        // Arrange
        var db = nameof(EditAsync_ConcurrencyConflict_ThrowsDbUpdateConcurrencyException);
        var clientId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var taskId = Guid.NewGuid();

        await using (var setupContext = await CreateContextAsync(db))
        {
            var client = CreateClient(clientId, "Test Client");
            var project = CreateProject(projectId, clientId, "Test Project");
            var task = TaskItem.Create(
                id: taskId,
                projectId: projectId,
                title: "Original Title",
                status: TaskItemStatus.Backlog,
                priority: TaskItemPriority.Normal,
                createdBy: "creator",
                createdAtUtc: CreatedAtUtc);

            setupContext.Clients.Add(client);
            setupContext.Projects.Add(project);
            setupContext.Tasks.Add(task);
            await setupContext.SaveChangesAsync();
        }

        // Simulate a concurrent update by changing RowVersion in the database
        await using (var conflictContext = await CreateContextAsync(db))
        {
            var taskToConflict = await conflictContext.Tasks.SingleAsync(t => t.Id == taskId);
            taskToConflict.Edit(
                newTitle: "Concurrently Modified Title",
                newDescription: null,
                newStartDateUtc: null,
                newDueDateUtc: null,
                newNotes: null,
                modifiedBy: "concurrent-editor",
                modifiedAtUtc: DateTime.UtcNow);
            taskToConflict.RowVersion = [9, 8, 7]; // This will trigger concurrency conflict
            await conflictContext.SaveChangesAsync();
        }

        await using (var updateContext = await CreateContextAsync(db))
        {
            var fetchedTask = await updateContext.Tasks.SingleAsync(t => t.Id == taskId);
            fetchedTask.RowVersion = [1, 2, 3]; // Old RowVersion

            var updateData = new TaskData(updateContext, new TaskRepository(updateContext));
            // Apply the edit mutation using the Edit method
            var modifiedAtUtc = DateTime.UtcNow;
            var changes = fetchedTask.Edit(
                newTitle: "Attempted Update",
                newDescription: null,
                newStartDateUtc: null,
                newDueDateUtc: null,
                newNotes: null,
                modifiedBy: "editor-conflict",
                modifiedAtUtc: modifiedAtUtc);

            var editAuditFact = new EntityMutationAudited
            {
                EventId = Guid.NewGuid().ToString(),
                OccurredAtUtc = new DateTimeOffset(modifiedAtUtc),
                SourceService = AuditSourceServices.Crm,
                EntityType = AuditEntityTypes.Task,
                EntityId = taskId,
                Action = AuditActions.Updated,
                ActorId = "editor-conflict",
                ActorType = AuditActorTypes.User,
                TraceId = "trace-edit-conflict",
                CorrelationId = "corr-edit-conflict",
                CausationId = "cause-edit-conflict",
                ChangedFields = [nameof(TaskItem.Title)],
                PreviousValues = new Dictionary<string, string> { { nameof(TaskItem.Title), changes.PreviousTitle ?? string.Empty } },
                NewValues = new Dictionary<string, string> { { nameof(TaskItem.Title), changes.NewTitle ?? string.Empty } },
            };

            // Act & Assert
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
                updateData.EditAsync(fetchedTask, editAuditFact, CancellationToken.None));
        }
    }

    #endregion
}

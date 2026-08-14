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

// Real SQL Server integration tests for TaskData status transition operations (TASK-010..012,
// DATA-001..005, DATA-008, AUDIT-001..008, OUTBOX-001/002; messaging.md publish-side test
// matrix and optimistic locking patterns). Each test gets its own database inside the shared
// container so tests never interfere despite sharing one SQL Server instance.
public class TaskStatusTransitionDataTests : IClassFixture<MsSqlContainerFixture>
{
    private readonly MsSqlContainerFixture _fixture;

    public TaskStatusTransitionDataTests(MsSqlContainerFixture fixture)
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

    private static Client CreateClient(Guid id) =>
        Client.Create(
            id: id,
            name: "Acme Corporation",
            lifecycleStatus: ClientLifecycleStatus.Lead,
            ownerUserId: "owner-1",
            createdBy: "creator-1",
            createdAtUtc: CreatedAtUtc,
            primaryEmail: "jane@acme.example",
            primaryPhone: "+1-555-0100");

    private static Project CreateProject(Guid id, Guid clientId) =>
        Project.Create(
            id: id,
            clientId: clientId,
            name: "Website Redesign",
            status: ProjectStatus.Planned,
            priority: ProjectPriority.Normal,
            ownerUserId: "owner-1",
            createdBy: "creator-1",
            createdAtUtc: CreatedAtUtc,
            description: "Redesign the client website",
            startDateUtc: CreatedAtUtc.AddDays(1),
            targetCompletionDateUtc: CreatedAtUtc.AddDays(30));

    private static TaskItem CreateTask(Guid id, Guid projectId, TaskItemStatus status = TaskItemStatus.Backlog, DateTime? completedAtUtc = null) =>
        TaskItem.Create(
            id: id,
            projectId: projectId,
            title: "Implement Feature",
            status: status,
            priority: TaskItemPriority.Normal,
            createdBy: "creator-1",
            createdAtUtc: CreatedAtUtc,
            description: "Add feature implementation",
            assignedUserId: null,
            startDateUtc: CreatedAtUtc.AddDays(1),
            dueDateUtc: CreatedAtUtc.AddDays(14),
            completedAtUtc: status == TaskItemStatus.Completed ? (completedAtUtc ?? CreatedAtUtc) : completedAtUtc);

    private static EntityMutationAudited CreateAuditFact(
        Guid taskId,
        string action = AuditActions.StatusChanged,
        Guid? eventId = null) => new()
    {
        EventId = (eventId ?? Guid.NewGuid()).ToString(),
        OccurredAtUtc = new DateTimeOffset(CreatedAtUtc.AddHours(1)),
        SourceService = AuditSourceServices.Crm,
        EntityType = AuditEntityTypes.Task,
        EntityId = taskId,
        Action = action,
        ActorId = "user-1",
        ActorType = AuditActorTypes.User,
        TraceId = Guid.NewGuid().ToString("N"),
        CorrelationId = Guid.NewGuid().ToString(),
        CausationId = Guid.NewGuid().ToString(),
        ChangedFields = ["Status"],
    };

    private async Task SetupClientAndProjectAsync(CrmDbContext context, Guid clientId, Guid projectId)
    {
        var client = CreateClient(clientId);
        var clientData = new ClientData(context, new ClientRepository(context));
        await clientData.CreateAsync(client, CreateAuditFact(clientId), CancellationToken.None);

        var project = CreateProject(projectId, clientId);
        var projectData = new ProjectData(context, new ProjectRepository(context));
        await projectData.CreateAsync(project, CreateAuditFact(projectId), CancellationToken.None);
    }

    #region ChangeStatusAsync Tests

    [Fact]
    public async Task ChangeStatusAsync_WhenTransitioningToCompleted_PersistsStatusChangeAndOutboxMessage()
    {
        // Arrange
        var db = nameof(ChangeStatusAsync_WhenTransitioningToCompleted_PersistsStatusChangeAndOutboxMessage);
        await using var setupContext = await CreateContextAsync(db);
        var clientId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        await SetupClientAndProjectAsync(setupContext, clientId, projectId);

        var taskId = Guid.NewGuid();
        var task = CreateTask(taskId, projectId, TaskItemStatus.ToDo);
        var setupData = new TaskData(setupContext, new TaskRepository(setupContext));
        await setupData.CreateAsync(task, CreateAuditFact(taskId), CancellationToken.None);

        // Fetch the task and transition it to Completed
        await using var context = await CreateContextAsync(db);
        var data = new TaskData(context, new TaskRepository(context));
        var fetchedTask = await context.Tasks.SingleAsync(t => t.Id == taskId);
        fetchedTask.RowVersion = [1, 2, 3]; // Use a dummy RowVersion for the test

        var completedAtUtc = CreatedAtUtc.AddHours(2);
        var (_, newStatus) = fetchedTask.SetStatus(TaskItemStatus.Completed, "modifier", completedAtUtc);
        var auditFact = CreateAuditFact(taskId, AuditActions.Completed);

        // Act
        await data.ChangeStatusAsync(fetchedTask, auditFact, CancellationToken.None);

        // Assert: verify status change and outbox message
        await using var verifyContext = await CreateContextAsync(db);
        var persistedTask = await verifyContext.Tasks.SingleAsync(t => t.Id == taskId);
        Assert.Equal(TaskItemStatus.Completed, persistedTask.Status);
        Assert.Equal(completedAtUtc, persistedTask.CompletedAtUtc);

        var persistedOutbox = await verifyContext.OutboxMessages.SingleAsync(
            m => m.Id == Guid.Parse(auditFact.EventId));
        Assert.Equal(AuditContractType, persistedOutbox.ContractType);
        Assert.Equal(OutboxMessageStatus.Pending, persistedOutbox.Status);
    }

    [Fact]
    public async Task ChangeStatusAsync_WhenStatusChangeTransactionFails_RollsBackBothStatusAndOutboxMessage()
    {
        // Proves atomicity: status and outbox commit together, or neither commits.
        var db = nameof(ChangeStatusAsync_WhenStatusChangeTransactionFails_RollsBackBothStatusAndOutboxMessage);
        await using var setupContext = await CreateContextAsync(db);
        var clientId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        await SetupClientAndProjectAsync(setupContext, clientId, projectId);

        var taskId = Guid.NewGuid();
        var task = CreateTask(taskId, projectId, TaskItemStatus.Backlog);
        var setupData = new TaskData(setupContext, new TaskRepository(setupContext));
        await setupData.CreateAsync(task, CreateAuditFact(taskId), CancellationToken.None);

        // Fetch task, transition it, then attempt an update with a mismatched RowVersion
        // to trigger an optimistic locking conflict.
        await using var context = await CreateContextAsync(db);
        var data = new TaskData(context, new TaskRepository(context));
        var fetchedTask = await context.Tasks.SingleAsync(t => t.Id == taskId);
        var originalRowVersion = fetchedTask.RowVersion;

        // Change the task
        fetchedTask.SetStatus(TaskItemStatus.ToDo, "modifier", CreatedAtUtc.AddHours(1));

        // Simulate a concurrent update by setting an incorrect RowVersion
        fetchedTask.RowVersion = [255, 255, 255];
        var auditFact = CreateAuditFact(taskId);

        // Act & Assert: expect concurrency exception
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
            () => data.ChangeStatusAsync(fetchedTask, auditFact, CancellationToken.None));

        // Verify: task status should remain unchanged and no outbox message persisted
        await using var verifyContext = await CreateContextAsync(db);
        var persistedTask = await verifyContext.Tasks.SingleAsync(t => t.Id == taskId);
        Assert.Equal(TaskItemStatus.Backlog, persistedTask.Status);
        Assert.False(await verifyContext.OutboxMessages.AnyAsync(
            m => m.Id == Guid.Parse(auditFact.EventId)));
    }

    [Fact]
    public async Task ChangeStatusAsync_PreservesCompletionTimestamp_WhenTransitioningToCompleted()
    {
        // Verify that CompletedAtUtc is set when transitioning to Completed status
        var db = nameof(ChangeStatusAsync_PreservesCompletionTimestamp_WhenTransitioningToCompleted);
        await using var setupContext = await CreateContextAsync(db);
        var clientId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        await SetupClientAndProjectAsync(setupContext, clientId, projectId);

        var taskId = Guid.NewGuid();
        var task = CreateTask(taskId, projectId, TaskItemStatus.InProgress);
        var setupData = new TaskData(setupContext, new TaskRepository(setupContext));
        await setupData.CreateAsync(task, CreateAuditFact(taskId), CancellationToken.None);

        await using var context = await CreateContextAsync(db);
        var data = new TaskData(context, new TaskRepository(context));
        var fetchedTask = await context.Tasks.SingleAsync(t => t.Id == taskId);
        Assert.Null(fetchedTask.CompletedAtUtc);

        var completedAtUtc = CreatedAtUtc.AddHours(5);
        fetchedTask.SetStatus(TaskItemStatus.Completed, "modifier", completedAtUtc);
        var auditFact = CreateAuditFact(taskId, AuditActions.Completed);

        await data.ChangeStatusAsync(fetchedTask, auditFact, CancellationToken.None);

        await using var verifyContext = await CreateContextAsync(db);
        var persistedTask = await verifyContext.Tasks.SingleAsync(t => t.Id == taskId);
        Assert.NotNull(persistedTask.CompletedAtUtc);
        Assert.Equal(completedAtUtc, persistedTask.CompletedAtUtc);
    }

    #endregion

    #region ReopenAsync Tests

    [Fact]
    public async Task ReopenAsync_WhenReopeningCompletedTask_ClearsCompletionTimestampAndPersistsOutboxMessage()
    {
        // Arrange
        var db = nameof(ReopenAsync_WhenReopeningCompletedTask_ClearsCompletionTimestampAndPersistsOutboxMessage);
        await using var setupContext = await CreateContextAsync(db);
        var clientId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        await SetupClientAndProjectAsync(setupContext, clientId, projectId);

        var completedAtUtc = CreatedAtUtc.AddHours(2);
        var taskId = Guid.NewGuid();
        var task = CreateTask(taskId, projectId, TaskItemStatus.Completed);
        // Manually set CompletedAtUtc by creating the task with it
        var completedTask = TaskItem.Create(
            id: taskId,
            projectId: projectId,
            title: "Implement Feature",
            status: TaskItemStatus.Completed,
            priority: TaskItemPriority.Normal,
            createdBy: "creator-1",
            createdAtUtc: CreatedAtUtc,
            completedAtUtc: completedAtUtc);

        var setupData = new TaskData(setupContext, new TaskRepository(setupContext));
        await setupData.CreateAsync(completedTask, CreateAuditFact(taskId), CancellationToken.None);

        // Fetch the completed task and reopen it
        await using var context = await CreateContextAsync(db);
        var data = new TaskData(context, new TaskRepository(context));
        var fetchedTask = await context.Tasks.SingleAsync(t => t.Id == taskId);
        Assert.NotNull(fetchedTask.CompletedAtUtc);

        fetchedTask.SetReopen(TaskItemStatus.ToDo, "reopener", CreatedAtUtc.AddHours(3));
        var auditFact = CreateAuditFact(taskId, AuditActions.Reopened);

        // Act
        await data.ReopenAsync(fetchedTask, auditFact, CancellationToken.None);

        // Assert: verify task reopened and completion timestamp cleared
        await using var verifyContext = await CreateContextAsync(db);
        var persistedTask = await verifyContext.Tasks.SingleAsync(t => t.Id == taskId);
        Assert.Equal(TaskItemStatus.ToDo, persistedTask.Status);
        Assert.Null(persistedTask.CompletedAtUtc);

        var persistedOutbox = await verifyContext.OutboxMessages.SingleAsync(
            m => m.Id == Guid.Parse(auditFact.EventId));
        Assert.Equal(AuditContractType, persistedOutbox.ContractType);
        Assert.Equal(OutboxMessageStatus.Pending, persistedOutbox.Status);
    }

    [Fact]
    public async Task ReopenAsync_WithConcurrencyConflict_RollsBackBothReopenAndOutboxMessage()
    {
        // Proves atomicity and optimistic locking: when RowVersion is stale, both reopen and
        // outbox message rollback.
        var db = nameof(ReopenAsync_WithConcurrencyConflict_RollsBackBothReopenAndOutboxMessage);
        await using var setupContext = await CreateContextAsync(db);
        var clientId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        await SetupClientAndProjectAsync(setupContext, clientId, projectId);

        var completedAtUtc = CreatedAtUtc.AddHours(2);
        var taskId = Guid.NewGuid();
        var completedTask = TaskItem.Create(
            id: taskId,
            projectId: projectId,
            title: "Implement Feature",
            status: TaskItemStatus.Completed,
            priority: TaskItemPriority.Normal,
            createdBy: "creator-1",
            createdAtUtc: CreatedAtUtc,
            completedAtUtc: completedAtUtc);

        var setupData = new TaskData(setupContext, new TaskRepository(setupContext));
        await setupData.CreateAsync(completedTask, CreateAuditFact(taskId), CancellationToken.None);

        // Fetch and attempt to reopen with stale RowVersion
        await using var context = await CreateContextAsync(db);
        var data = new TaskData(context, new TaskRepository(context));
        var fetchedTask = await context.Tasks.SingleAsync(t => t.Id == taskId);

        fetchedTask.SetReopen(TaskItemStatus.Backlog, "reopener", CreatedAtUtc.AddHours(3));
        fetchedTask.RowVersion = [99, 99, 99]; // Stale version
        var auditFact = CreateAuditFact(taskId, AuditActions.Reopened);

        // Act & Assert
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
            () => data.ReopenAsync(fetchedTask, auditFact, CancellationToken.None));

        // Verify: task remains Completed, no outbox message persisted
        await using var verifyContext = await CreateContextAsync(db);
        var persistedTask = await verifyContext.Tasks.SingleAsync(t => t.Id == taskId);
        Assert.Equal(TaskItemStatus.Completed, persistedTask.Status);
        Assert.Equal(completedAtUtc, persistedTask.CompletedAtUtc);
        Assert.False(await verifyContext.OutboxMessages.AnyAsync(
            m => m.Id == Guid.Parse(auditFact.EventId)));
    }

    #endregion

    private const string AuditContractType = "Audit.EntityMutationAudited";
}

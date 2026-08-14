using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ProjectChicago.Crm.Core.Models.DataModels.Entities;
using ProjectChicago.Crm.Core.Persistence;
using ProjectChicago.Crm.Core.Repositories;
using ProjectChicago.Crm.Core.Tests.Persistence;
using Xunit;

namespace ProjectChicago.Crm.Core.Tests.Repositories;

// Real SQL Server integration tests for TaskRepository.ListAsync (TASK-020..022, PERF-001..004).
// Tests verify filter composition (AND semantics), sorting, pagination, tie-breaker determinism,
// due-date range semantics, null handling, and N+1 avoidance. Each test gets its own database
// inside the shared SQL Server container so tests never interfere with each other.
public class TaskRepositoryListTests : IClassFixture<MsSqlContainerFixture>
{
    private readonly MsSqlContainerFixture _fixture;

    public TaskRepositoryListTests(MsSqlContainerFixture fixture)
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

    private static readonly DateTime BaseTimeUtc = new(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);

    private static Client CreateClient(Guid id, string name) =>
        Client.Create(
            id: id,
            name: name,
            lifecycleStatus: ClientLifecycleStatus.Lead,
            ownerUserId: "owner-1",
            createdBy: "creator-1",
            createdAtUtc: BaseTimeUtc,
            primaryEmail: $"{name.ToLower().Replace(" ", "")}@example.com",
            primaryPhone: "+1-555-0100");

    private static Project CreateProject(Guid id, Guid clientId, string name) =>
        Project.Create(
            id: id,
            clientId: clientId,
            name: name,
            status: ProjectStatus.Active,
            priority: ProjectPriority.Normal,
            ownerUserId: "owner-1",
            createdBy: "creator-1",
            createdAtUtc: BaseTimeUtc);

    private static TaskItem CreateTask(
        Guid id,
        Guid projectId,
        string title,
        TaskItemStatus status = TaskItemStatus.Backlog,
        TaskItemPriority priority = TaskItemPriority.Normal,
        string? assignedUserId = null,
        DateTime? dueDateUtc = null,
        DateTime? createdAtUtc = null,
        DateTime? completedAtUtc = null) =>
        TaskItem.Create(
            id: id,
            projectId: projectId,
            title: title,
            status: status,
            priority: priority,
            createdBy: "creator-1",
            createdAtUtc: createdAtUtc ?? BaseTimeUtc,
            assignedUserId: assignedUserId,
            dueDateUtc: dueDateUtc,
            completedAtUtc: completedAtUtc);

    [Fact]
    public async Task ListAsync_WithNoFilters_ReturnsAllTasks()
    {
        // Arrange
        var dbName = $"TaskRepo_NoFilters_{Guid.NewGuid():N}";
        await using var context = await CreateContextAsync(dbName);
        var repository = new TaskRepository(context);

        var clientId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var client = CreateClient(clientId, "Acme");
        var project = CreateProject(projectId, clientId, "Website");

        await context.Clients.AddAsync(client);
        await context.Projects.AddAsync(project);

        var task1 = CreateTask(Guid.NewGuid(), projectId, "Task 1");
        var task2 = CreateTask(Guid.NewGuid(), projectId, "Task 2");
        var task3 = CreateTask(Guid.NewGuid(), projectId, "Task 3");

        await context.Tasks.AddRangeAsync(task1, task2, task3);
        await context.SaveChangesAsync();

        var filter = new TaskListFilter
        {
            Page = 1,
            PageSize = 10,
            SortBy = TaskListSortField.CreatedAtUtc,
            SortDirection = TaskListSortDirection.Ascending,
        };

        // Act
        var result = await repository.ListAsync(filter, CancellationToken.None);

        // Assert
        Assert.Equal(3, result.Items.Count);
        Assert.Equal(3, result.TotalCount);
        Assert.Equal(task1.Id, result.Items[0].Id);
        Assert.Equal(task2.Id, result.Items[1].Id);
        Assert.Equal(task3.Id, result.Items[2].Id);
    }

    [Fact]
    public async Task ListAsync_FilterByStatus_ReturnsOnlyMatchingTasks()
    {
        // Arrange
        var dbName = $"TaskRepo_StatusFilter_{Guid.NewGuid():N}";
        await using var context = await CreateContextAsync(dbName);
        var repository = new TaskRepository(context);

        var clientId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var client = CreateClient(clientId, "Acme");
        var project = CreateProject(projectId, clientId, "Website");

        await context.Clients.AddAsync(client);
        await context.Projects.AddAsync(project);

        var openTask = CreateTask(Guid.NewGuid(), projectId, "Open", TaskItemStatus.ToDo);
        var completedTask = CreateTask(Guid.NewGuid(), projectId, "Done", TaskItemStatus.Completed, completedAtUtc: BaseTimeUtc);
        var cancelledTask = CreateTask(Guid.NewGuid(), projectId, "Cancelled", TaskItemStatus.Cancelled);

        await context.Tasks.AddRangeAsync(openTask, completedTask, cancelledTask);
        await context.SaveChangesAsync();

        var filter = new TaskListFilter
        {
            Statuses = new HashSet<TaskItemStatus> { TaskItemStatus.ToDo },
            Page = 1,
            PageSize = 10,
            SortBy = TaskListSortField.CreatedAtUtc,
            SortDirection = TaskListSortDirection.Ascending,
        };

        // Act
        var result = await repository.ListAsync(filter, CancellationToken.None);

        // Assert
        Assert.Single(result.Items);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal(openTask.Id, result.Items[0].Id);
    }

    [Fact]
    public async Task ListAsync_FilterByStatusSet_ReturnsTasksMatchingAny()
    {
        // Arrange: TASK-021 status filter uses OR semantics within the set
        var dbName = $"TaskRepo_StatusSet_{Guid.NewGuid():N}";
        await using var context = await CreateContextAsync(dbName);
        var repository = new TaskRepository(context);

        var clientId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var client = CreateClient(clientId, "Acme");
        var project = CreateProject(projectId, clientId, "Website");

        await context.Clients.AddAsync(client);
        await context.Projects.AddAsync(project);

        var task1 = CreateTask(Guid.NewGuid(), projectId, "Task 1", TaskItemStatus.ToDo);
        var task2 = CreateTask(Guid.NewGuid(), projectId, "Task 2", TaskItemStatus.InProgress);
        var task3 = CreateTask(Guid.NewGuid(), projectId, "Task 3", TaskItemStatus.Blocked);
        var completed = CreateTask(Guid.NewGuid(), projectId, "Done", TaskItemStatus.Completed, completedAtUtc: BaseTimeUtc);

        await context.Tasks.AddRangeAsync(task1, task2, task3, completed);
        await context.SaveChangesAsync();

        var filter = new TaskListFilter
        {
            Statuses = new HashSet<TaskItemStatus> { TaskItemStatus.ToDo, TaskItemStatus.InProgress, TaskItemStatus.Blocked },
            Page = 1,
            PageSize = 10,
            SortBy = TaskListSortField.CreatedAtUtc,
            SortDirection = TaskListSortDirection.Ascending,
        };

        // Act
        var result = await repository.ListAsync(filter, CancellationToken.None);

        // Assert: TASK-020 "Open Tasks" = NOT Completed && NOT Cancelled
        Assert.Equal(3, result.Items.Count);
        Assert.Equal(3, result.TotalCount);
    }

    [Fact]
    public async Task ListAsync_FilterByPriority_ReturnsOnlyMatchingTasks()
    {
        // Arrange
        var dbName = $"TaskRepo_PriorityFilter_{Guid.NewGuid():N}";
        await using var context = await CreateContextAsync(dbName);
        var repository = new TaskRepository(context);

        var clientId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var client = CreateClient(clientId, "Acme");
        var project = CreateProject(projectId, clientId, "Website");

        await context.Clients.AddAsync(client);
        await context.Projects.AddAsync(project);

        var highPriority = CreateTask(Guid.NewGuid(), projectId, "Urgent", priority: TaskItemPriority.High);
        var normalPriority = CreateTask(Guid.NewGuid(), projectId, "Normal", priority: TaskItemPriority.Normal);
        var lowPriority = CreateTask(Guid.NewGuid(), projectId, "Low", priority: TaskItemPriority.Low);

        await context.Tasks.AddRangeAsync(highPriority, normalPriority, lowPriority);
        await context.SaveChangesAsync();

        var filter = new TaskListFilter
        {
            Priorities = new HashSet<TaskItemPriority> { TaskItemPriority.High },
            Page = 1,
            PageSize = 10,
            SortBy = TaskListSortField.CreatedAtUtc,
            SortDirection = TaskListSortDirection.Ascending,
        };

        // Act
        var result = await repository.ListAsync(filter, CancellationToken.None);

        // Assert
        Assert.Single(result.Items);
        Assert.Equal(highPriority.Id, result.Items[0].Id);
    }

    [Fact]
    public async Task ListAsync_FilterByAssignedUserId_ReturnsOnlyTasksAssignedToUser()
    {
        // Arrange
        var dbName = $"TaskRepo_AssigneeFilter_{Guid.NewGuid():N}";
        await using var context = await CreateContextAsync(dbName);
        var repository = new TaskRepository(context);

        var clientId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var client = CreateClient(clientId, "Acme");
        var project = CreateProject(projectId, clientId, "Website");

        await context.Clients.AddAsync(client);
        await context.Projects.AddAsync(project);

        var myTask = CreateTask(Guid.NewGuid(), projectId, "My Task", assignedUserId: "user-1");
        var othersTask = CreateTask(Guid.NewGuid(), projectId, "Others", assignedUserId: "user-2");
        var unassigned = CreateTask(Guid.NewGuid(), projectId, "Unassigned", assignedUserId: null);

        await context.Tasks.AddRangeAsync(myTask, othersTask, unassigned);
        await context.SaveChangesAsync();

        var filter = new TaskListFilter
        {
            AssignedUserId = "user-1",
            Page = 1,
            PageSize = 10,
            SortBy = TaskListSortField.CreatedAtUtc,
            SortDirection = TaskListSortDirection.Ascending,
        };

        // Act
        var result = await repository.ListAsync(filter, CancellationToken.None);

        // Assert: TASK-020 "My Tasks" = assigned to current user
        Assert.Single(result.Items);
        Assert.Equal(myTask.Id, result.Items[0].Id);
    }

    [Fact]
    public async Task ListAsync_FilterByProjectId_ReturnsOnlyProjectTasks()
    {
        // Arrange
        var dbName = $"TaskRepo_ProjectFilter_{Guid.NewGuid():N}";
        await using var context = await CreateContextAsync(dbName);
        var repository = new TaskRepository(context);

        var clientId = Guid.NewGuid();
        var project1Id = Guid.NewGuid();
        var project2Id = Guid.NewGuid();
        var client = CreateClient(clientId, "Acme");
        var project1 = CreateProject(project1Id, clientId, "Website");
        var project2 = CreateProject(project2Id, clientId, "API");

        await context.Clients.AddAsync(client);
        await context.Projects.AddRangeAsync(project1, project2);

        var project1Task = CreateTask(Guid.NewGuid(), project1Id, "Website Task");
        var project2Task = CreateTask(Guid.NewGuid(), project2Id, "API Task");

        await context.Tasks.AddRangeAsync(project1Task, project2Task);
        await context.SaveChangesAsync();

        var filter = new TaskListFilter
        {
            ProjectId = project1Id,
            Page = 1,
            PageSize = 10,
            SortBy = TaskListSortField.CreatedAtUtc,
            SortDirection = TaskListSortDirection.Ascending,
        };

        // Act
        var result = await repository.ListAsync(filter, CancellationToken.None);

        // Assert: TASK-020 "Project Tasks" = belong to specific Project
        Assert.Single(result.Items);
        Assert.Equal(project1Task.Id, result.Items[0].Id);
    }

    [Fact]
    public async Task ListAsync_FilterByClientId_ReturnsTasksAcrossClientProjects()
    {
        // Arrange: PERF-004 avoids N+1 with indexed join to Projects
        var dbName = $"TaskRepo_ClientFilter_{Guid.NewGuid():N}";
        await using var context = await CreateContextAsync(dbName);
        var repository = new TaskRepository(context);

        var client1Id = Guid.NewGuid();
        var client2Id = Guid.NewGuid();
        var project1Id = Guid.NewGuid();
        var project2Id = Guid.NewGuid();
        var project3Id = Guid.NewGuid();

        var client1 = CreateClient(client1Id, "Client A");
        var client2 = CreateClient(client2Id, "Client B");
        var project1 = CreateProject(project1Id, client1Id, "Project 1");
        var project2 = CreateProject(project2Id, client1Id, "Project 2");
        var project3 = CreateProject(project3Id, client2Id, "Project 3");

        await context.Clients.AddRangeAsync(client1, client2);
        await context.Projects.AddRangeAsync(project1, project2, project3);

        var client1Task1 = CreateTask(Guid.NewGuid(), project1Id, "Client A Task 1");
        var client1Task2 = CreateTask(Guid.NewGuid(), project2Id, "Client A Task 2");
        var client2Task = CreateTask(Guid.NewGuid(), project3Id, "Client B Task");

        await context.Tasks.AddRangeAsync(client1Task1, client1Task2, client2Task);
        await context.SaveChangesAsync();

        var filter = new TaskListFilter
        {
            ClientId = client1Id,
            Page = 1,
            PageSize = 10,
            SortBy = TaskListSortField.CreatedAtUtc,
            SortDirection = TaskListSortDirection.Ascending,
        };

        // Act
        var result = await repository.ListAsync(filter, CancellationToken.None);

        // Assert: TASK-021 Client filter should span all projects belonging to the client
        Assert.Equal(2, result.Items.Count);
        Assert.True(result.Items.Any(t => t.Id == client1Task1.Id));
        Assert.True(result.Items.Any(t => t.Id == client1Task2.Id));
    }

    [Fact]
    public async Task ListAsync_DueDateBefore_ReturnsTasksDueBeforeDate()
    {
        // Arrange: TASK-020 Overdue = DueDate < now
        var dbName = $"TaskRepo_DueDateBefore_{Guid.NewGuid():N}";
        await using var context = await CreateContextAsync(dbName);
        var repository = new TaskRepository(context);

        var clientId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var client = CreateClient(clientId, "Acme");
        var project = CreateProject(projectId, clientId, "Website");

        await context.Clients.AddAsync(client);
        await context.Projects.AddAsync(project);

        var now = DateTime.UtcNow;
        var overdue = CreateTask(Guid.NewGuid(), projectId, "Overdue", dueDateUtc: now.AddDays(-1));
        var dueSoon = CreateTask(Guid.NewGuid(), projectId, "Due Soon", dueDateUtc: now.AddDays(1));
        var noDueDate = CreateTask(Guid.NewGuid(), projectId, "No Due Date", dueDateUtc: null);

        await context.Tasks.AddRangeAsync(overdue, dueSoon, noDueDate);
        await context.SaveChangesAsync();

        var filter = new TaskListFilter
        {
            DueDateBefore = now,
            Page = 1,
            PageSize = 10,
            SortBy = TaskListSortField.CreatedAtUtc,
            SortDirection = TaskListSortDirection.Ascending,
        };

        // Act
        var result = await repository.ListAsync(filter, CancellationToken.None);

        // Assert: includes null due date and dates before the cutoff
        Assert.Equal(2, result.Items.Count);
        Assert.True(result.Items.Any(t => t.Id == overdue.Id));
        Assert.True(result.Items.Any(t => t.Id == noDueDate.Id));
    }

    [Fact]
    public async Task ListAsync_DueDateAfter_ReturnsTasksDueAfterDate()
    {
        // Arrange
        var dbName = $"TaskRepo_DueDateAfter_{Guid.NewGuid():N}";
        await using var context = await CreateContextAsync(dbName);
        var repository = new TaskRepository(context);

        var clientId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var client = CreateClient(clientId, "Acme");
        var project = CreateProject(projectId, clientId, "Website");

        await context.Clients.AddAsync(client);
        await context.Projects.AddAsync(project);

        var now = DateTime.UtcNow;
        var past = CreateTask(Guid.NewGuid(), projectId, "Past", dueDateUtc: now.AddDays(-5));
        var future = CreateTask(Guid.NewGuid(), projectId, "Future", dueDateUtc: now.AddDays(5));
        var noDueDate = CreateTask(Guid.NewGuid(), projectId, "No Due Date", dueDateUtc: null);

        await context.Tasks.AddRangeAsync(past, future, noDueDate);
        await context.SaveChangesAsync();

        var filter = new TaskListFilter
        {
            DueDateAfter = now,
            Page = 1,
            PageSize = 10,
            SortBy = TaskListSortField.CreatedAtUtc,
            SortDirection = TaskListSortDirection.Ascending,
        };

        // Act
        var result = await repository.ListAsync(filter, CancellationToken.None);

        // Assert: excludes null due date, includes only dates >= cutoff
        Assert.Single(result.Items);
        Assert.Equal(future.Id, result.Items[0].Id);
    }

    [Fact]
    public async Task ListAsync_FiltersCompose_WithAndSemantics()
    {
        // Arrange: TASK-021 filters compose with AND semantics
        var dbName = $"TaskRepo_ComposedFilters_{Guid.NewGuid():N}";
        await using var context = await CreateContextAsync(dbName);
        var repository = new TaskRepository(context);

        var clientId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var client = CreateClient(clientId, "Acme");
        var project = CreateProject(projectId, clientId, "Website");

        await context.Clients.AddAsync(client);
        await context.Projects.AddAsync(project);

        var highPriorityAssignedTask = CreateTask(
            Guid.NewGuid(), projectId, "Task A",
            status: TaskItemStatus.ToDo,
            priority: TaskItemPriority.High,
            assignedUserId: "user-1");

        var highPriorityUnassignedTask = CreateTask(
            Guid.NewGuid(), projectId, "Task B",
            status: TaskItemStatus.ToDo,
            priority: TaskItemPriority.High,
            assignedUserId: null);

        var lowPriorityAssignedTask = CreateTask(
            Guid.NewGuid(), projectId, "Task C",
            status: TaskItemStatus.ToDo,
            priority: TaskItemPriority.Low,
            assignedUserId: "user-1");

        await context.Tasks.AddRangeAsync(
            highPriorityAssignedTask, highPriorityUnassignedTask, lowPriorityAssignedTask);
        await context.SaveChangesAsync();

        var filter = new TaskListFilter
        {
            Statuses = new HashSet<TaskItemStatus> { TaskItemStatus.ToDo },
            Priorities = new HashSet<TaskItemPriority> { TaskItemPriority.High },
            AssignedUserId = "user-1",
            Page = 1,
            PageSize = 10,
            SortBy = TaskListSortField.CreatedAtUtc,
            SortDirection = TaskListSortDirection.Ascending,
        };

        // Act
        var result = await repository.ListAsync(filter, CancellationToken.None);

        // Assert: only Task A matches all three criteria
        Assert.Single(result.Items);
        Assert.Equal(highPriorityAssignedTask.Id, result.Items[0].Id);
    }

    [Fact]
    public async Task ListAsync_SortByDueDateUtc_WithNullsFirst()
    {
        // Arrange: TASK-022 sorting by due date; nulls sort first (no deadline)
        var dbName = $"TaskRepo_SortDueDate_{Guid.NewGuid():N}";
        await using var context = await CreateContextAsync(dbName);
        var repository = new TaskRepository(context);

        var clientId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var client = CreateClient(clientId, "Acme");
        var project = CreateProject(projectId, clientId, "Website");

        await context.Clients.AddAsync(client);
        await context.Projects.AddAsync(project);

        var now = DateTime.UtcNow;
        var due1 = CreateTask(Guid.NewGuid(), projectId, "Due 1", dueDateUtc: now.AddDays(1));
        var due2 = CreateTask(Guid.NewGuid(), projectId, "Due 2", dueDateUtc: now.AddDays(2));
        var noDue1 = CreateTask(Guid.NewGuid(), projectId, "No Due 1", dueDateUtc: null);
        var noDue2 = CreateTask(Guid.NewGuid(), projectId, "No Due 2", dueDateUtc: null);

        await context.Tasks.AddRangeAsync(due2, due1, noDue2, noDue1);
        await context.SaveChangesAsync();

        var filter = new TaskListFilter
        {
            Page = 1,
            PageSize = 10,
            SortBy = TaskListSortField.DueDateUtc,
            SortDirection = TaskListSortDirection.Ascending,
        };

        // Act
        var result = await repository.ListAsync(filter, CancellationToken.None);

        // Assert: nulls first, then sorted by due date
        Assert.Equal(4, result.Items.Count);
        Assert.True(result.Items[0].DueDateUtc == null);
        Assert.True(result.Items[1].DueDateUtc == null);
        Assert.True(result.Items[2].DueDateUtc < result.Items[3].DueDateUtc);
    }

    [Fact]
    public async Task ListAsync_SortByPriority_Ascending()
    {
        // Arrange: TASK-022 sort by priority
        var dbName = $"TaskRepo_SortPriority_{Guid.NewGuid():N}";
        await using var context = await CreateContextAsync(dbName);
        var repository = new TaskRepository(context);

        var clientId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var client = CreateClient(clientId, "Acme");
        var project = CreateProject(projectId, clientId, "Website");

        await context.Clients.AddAsync(client);
        await context.Projects.AddAsync(project);

        var critical = CreateTask(Guid.NewGuid(), projectId, "Critical", priority: TaskItemPriority.Critical);
        var high = CreateTask(Guid.NewGuid(), projectId, "High", priority: TaskItemPriority.High);
        var normal = CreateTask(Guid.NewGuid(), projectId, "Normal", priority: TaskItemPriority.Normal);
        var low = CreateTask(Guid.NewGuid(), projectId, "Low", priority: TaskItemPriority.Low);

        await context.Tasks.AddRangeAsync(critical, low, high, normal);
        await context.SaveChangesAsync();

        var filter = new TaskListFilter
        {
            Page = 1,
            PageSize = 10,
            SortBy = TaskListSortField.Priority,
            SortDirection = TaskListSortDirection.Ascending,
        };

        // Act
        var result = await repository.ListAsync(filter, CancellationToken.None);

        // Assert: sorted by priority ascending
        Assert.Equal(4, result.Items.Count);
        Assert.Equal(low.Id, result.Items[0].Id);
        Assert.Equal(normal.Id, result.Items[1].Id);
        Assert.Equal(high.Id, result.Items[2].Id);
        Assert.Equal(critical.Id, result.Items[3].Id);
    }

    [Fact]
    public async Task ListAsync_SortByCreatedAtUtc_Descending()
    {
        // Arrange: TASK-022 sort by created date descending
        var dbName = $"TaskRepo_SortCreated_{Guid.NewGuid():N}";
        await using var context = await CreateContextAsync(dbName);
        var repository = new TaskRepository(context);

        var clientId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var client = CreateClient(clientId, "Acme");
        var project = CreateProject(projectId, clientId, "Website");

        await context.Clients.AddAsync(client);
        await context.Projects.AddAsync(project);

        var task1 = CreateTask(Guid.NewGuid(), projectId, "Task 1", createdAtUtc: BaseTimeUtc);
        var task2 = CreateTask(Guid.NewGuid(), projectId, "Task 2", createdAtUtc: BaseTimeUtc.AddHours(1));
        var task3 = CreateTask(Guid.NewGuid(), projectId, "Task 3", createdAtUtc: BaseTimeUtc.AddHours(2));

        await context.Tasks.AddRangeAsync(task1, task2, task3);
        await context.SaveChangesAsync();

        var filter = new TaskListFilter
        {
            Page = 1,
            PageSize = 10,
            SortBy = TaskListSortField.CreatedAtUtc,
            SortDirection = TaskListSortDirection.Descending,
        };

        // Act
        var result = await repository.ListAsync(filter, CancellationToken.None);

        // Assert: sorted by created date descending (newest first)
        Assert.Equal(3, result.Items.Count);
        Assert.Equal(task3.Id, result.Items[0].Id);
        Assert.Equal(task2.Id, result.Items[1].Id);
        Assert.Equal(task1.Id, result.Items[2].Id);
    }

    [Fact]
    public async Task ListAsync_Pagination_WithTieBreaker()
    {
        // Arrange: PERF-003 bounded pagination with deterministic tie-breaker (Id)
        var dbName = $"TaskRepo_Pagination_{Guid.NewGuid():N}";
        await using var context = await CreateContextAsync(dbName);
        var repository = new TaskRepository(context);

        var clientId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var client = CreateClient(clientId, "Acme");
        var project = CreateProject(projectId, clientId, "Website");

        await context.Clients.AddAsync(client);
        await context.Projects.AddAsync(project);

        var tasks = Enumerable.Range(1, 15)
            .Select(i => CreateTask(Guid.NewGuid(), projectId, $"Task {i}"))
            .ToList();

        await context.Tasks.AddRangeAsync(tasks);
        await context.SaveChangesAsync();

        var filter = new TaskListFilter
        {
            Page = 2,
            PageSize = 5,
            SortBy = TaskListSortField.CreatedAtUtc,
            SortDirection = TaskListSortDirection.Ascending,
        };

        // Act
        var result = await repository.ListAsync(filter, CancellationToken.None);

        // Assert: second page with 5 items per page
        Assert.Equal(5, result.Items.Count);
        Assert.Equal(15, result.TotalCount);
        Assert.Equal(3, (result.TotalCount + filter.PageSize - 1) / filter.PageSize); // 3 pages total
    }

    [Fact]
    public async Task ListAsync_EmptyResult_ReturnsTotalCountZero()
    {
        // Arrange
        var dbName = $"TaskRepo_EmptyResult_{Guid.NewGuid():N}";
        await using var context = await CreateContextAsync(dbName);
        var repository = new TaskRepository(context);

        var clientId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var client = CreateClient(clientId, "Acme");
        var project = CreateProject(projectId, clientId, "Website");

        await context.Clients.AddAsync(client);
        await context.Projects.AddAsync(project);
        await context.SaveChangesAsync();

        var filter = new TaskListFilter
        {
            Statuses = new HashSet<TaskItemStatus> { TaskItemStatus.Completed },
            Page = 1,
            PageSize = 10,
            SortBy = TaskListSortField.CreatedAtUtc,
            SortDirection = TaskListSortDirection.Ascending,
        };

        // Act
        var result = await repository.ListAsync(filter, CancellationToken.None);

        // Assert: empty result set with correct total count
        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public async Task ListAsync_PageBeyondData_ReturnsEmptyItems()
    {
        // Arrange: PERF-003 pagination behavior at edge cases
        var dbName = $"TaskRepo_PageBeyond_{Guid.NewGuid():N}";
        await using var context = await CreateContextAsync(dbName);
        var repository = new TaskRepository(context);

        var clientId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var client = CreateClient(clientId, "Acme");
        var project = CreateProject(projectId, clientId, "Website");

        await context.Clients.AddAsync(client);
        await context.Projects.AddAsync(project);

        var task = CreateTask(Guid.NewGuid(), projectId, "Task 1");
        await context.Tasks.AddAsync(task);
        await context.SaveChangesAsync();

        var filter = new TaskListFilter
        {
            Page = 10,
            PageSize = 5,
            SortBy = TaskListSortField.CreatedAtUtc,
            SortDirection = TaskListSortDirection.Ascending,
        };

        // Act
        var result = await repository.ListAsync(filter, CancellationToken.None);

        // Assert: page beyond data returns empty items but preserves total count
        Assert.Empty(result.Items);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task ListAsync_DeterministicOrdering_WithSharedSortValue()
    {
        // Arrange: tasks with same sort key use Id as tie-breaker (PERF-002 determinism)
        var dbName = $"TaskRepo_Deterministic_{Guid.NewGuid():N}";
        await using var context = await CreateContextAsync(dbName);
        var repository = new TaskRepository(context);

        var clientId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var client = CreateClient(clientId, "Acme");
        var project = CreateProject(projectId, clientId, "Website");

        await context.Clients.AddAsync(client);
        await context.Projects.AddAsync(project);

        // All have same priority and created time - should sort by Id
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var id3 = Guid.NewGuid();
        var min = new[] { id1, id2, id3 }.Min();
        var max = new[] { id1, id2, id3 }.Max();

        var task1 = CreateTask(id1, projectId, "Task 1", priority: TaskItemPriority.Normal);
        var task2 = CreateTask(id2, projectId, "Task 2", priority: TaskItemPriority.Normal);
        var task3 = CreateTask(id3, projectId, "Task 3", priority: TaskItemPriority.Normal);

        // Add in reverse order to database
        await context.Tasks.AddRangeAsync(task3, task2, task1);
        await context.SaveChangesAsync();

        var filter = new TaskListFilter
        {
            Page = 1,
            PageSize = 10,
            SortBy = TaskListSortField.Priority,
            SortDirection = TaskListSortDirection.Ascending,
        };

        // Act - run twice to verify determinism
        var result1 = await repository.ListAsync(filter, CancellationToken.None);
        var result2 = await repository.ListAsync(filter, CancellationToken.None);

        // Assert: same order both times, sorted by Id when priority is equal
        Assert.Equal(3, result1.Items.Count);
        Assert.Equal(3, result2.Items.Count);
        Assert.Equal(min, result1.Items[0].Id);
        Assert.Equal(result1.Items[0].Id, result2.Items[0].Id);
        Assert.Equal(result1.Items[1].Id, result2.Items[1].Id);
        Assert.Equal(result1.Items[2].Id, result2.Items[2].Id);
    }
}

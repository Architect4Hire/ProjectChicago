using Microsoft.EntityFrameworkCore;
using ProjectChicago.Crm.Core.Models.DataModels.Entities;
using ProjectChicago.Crm.Core.Persistence;
using ProjectChicago.Crm.Core.Repositories;
using Xunit;

namespace ProjectChicago.Crm.Core.Tests.Repositories;

// SQL Server integration tests for IProjectRepository.GetDetailAsync (PROJECT-030).
// Uses a real test database to verify that the detail query correctly assembles Project,
// Client, open TaskItems, completed TaskItems, and activity metadata. Do not use EF InMemory -
// it does not validate SQL Server-specific query behavior (database.md Tests).
[Collection("ProjectRepository")]
public class ProjectRepositoryDetailTests
{
    private readonly CrmDbContextFactory _dbContextFactory;

    public ProjectRepositoryDetailTests()
    {
        _dbContextFactory = new CrmDbContextFactory();
    }

    [Fact(DisplayName = "GetDetailAsync returns project detail including client and tasks")]
    public async Task GetDetailAsync_ProjectExists_ReturnsCompleteDetail()
    {
        // Arrange
        await using var dbContext = _dbContextFactory.CreateDbContext();
        var client = CreateClient("Test Client");
        await dbContext.Clients.AddAsync(client);
        await dbContext.SaveChangesAsync();

        var project = CreateProject(client.Id, "Test Project");
        await dbContext.Projects.AddAsync(project);
        await dbContext.SaveChangesAsync();

        var openTask = CreateTaskItem(project.Id, "Open Task", TaskItemStatus.ToDo);
        var completedTask = CreateTaskItem(project.Id, "Completed Task", TaskItemStatus.Completed, DateTime.UtcNow);
        await dbContext.Tasks.AddRangeAsync(openTask, completedTask);
        await dbContext.SaveChangesAsync();

        var repository = new ProjectRepository(dbContext);

        // Act
        var result = await repository.GetDetailAsync(project.Id, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(project.Id, result!.Project.Id);
        Assert.Equal(client.Id, result.Client.Id);
        Assert.Equal(client.Name, result.Client.Name);
        Assert.Single(result.OpenTasks);
        Assert.Single(result.CompletedTasks);
        Assert.Equal(openTask.Title, result.OpenTasks[0].Title);
        Assert.Equal(completedTask.Title, result.CompletedTasks[0].Title);
    }

    [Fact(DisplayName = "GetDetailAsync returns null when project does not exist")]
    public async Task GetDetailAsync_ProjectNotFound_ReturnsNull()
    {
        // Arrange
        await using var dbContext = _dbContextFactory.CreateDbContext();
        var repository = new ProjectRepository(dbContext);
        var nonExistentProjectId = Guid.NewGuid();

        // Act
        var result = await repository.GetDetailAsync(nonExistentProjectId, CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    [Fact(DisplayName = "GetDetailAsync returns only open tasks (excludes completed and cancelled)")]
    public async Task GetDetailAsync_TasksWithVariousStatuses_CorrectlyPartitionsOpenAndCompleted()
    {
        // Arrange
        await using var dbContext = _dbContextFactory.CreateDbContext();
        var client = CreateClient("Client");
        await dbContext.Clients.AddAsync(client);
        await dbContext.SaveChangesAsync();

        var project = CreateProject(client.Id, "Project");
        await dbContext.Projects.AddAsync(project);
        await dbContext.SaveChangesAsync();

        var toDoTask = CreateTaskItem(project.Id, "To Do", TaskItemStatus.ToDo);
        var inProgressTask = CreateTaskItem(project.Id, "In Progress", TaskItemStatus.InProgress);
        var blockedTask = CreateTaskItem(project.Id, "Blocked", TaskItemStatus.Blocked);
        var completedTask = CreateTaskItem(project.Id, "Completed", TaskItemStatus.Completed, DateTime.UtcNow);
        var cancelledTask = CreateTaskItem(project.Id, "Cancelled", TaskItemStatus.Cancelled);
        var backlogTask = CreateTaskItem(project.Id, "Backlog", TaskItemStatus.Backlog);

        await dbContext.Tasks.AddRangeAsync(toDoTask, inProgressTask, blockedTask, completedTask, cancelledTask, backlogTask);
        await dbContext.SaveChangesAsync();

        var repository = new ProjectRepository(dbContext);

        // Act
        var result = await repository.GetDetailAsync(project.Id, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        // Open tasks are all non-Completed, non-Cancelled (ToDoTask, InProgressTask, BlockedTask, BacklogTask)
        Assert.Equal(4, result!.OpenTasks.Count);
        Assert.Single(result.CompletedTasks);
        Assert.All(result.OpenTasks, t => Assert.NotEqual(TaskItemStatus.Completed, t.Status));
        Assert.All(result.OpenTasks, t => Assert.NotEqual(TaskItemStatus.Cancelled, t.Status));
    }

    [Fact(DisplayName = "GetDetailAsync orders open tasks by due date")]
    public async Task GetDetailAsync_OpenTasksOrdering_OrderedByDueDateThenId()
    {
        // Arrange
        await using var dbContext = _dbContextFactory.CreateDbContext();
        var client = CreateClient("Client");
        await dbContext.Clients.AddAsync(client);
        await dbContext.SaveChangesAsync();

        var project = CreateProject(client.Id, "Project");
        await dbContext.Projects.AddAsync(project);
        await dbContext.SaveChangesAsync();

        var today = DateTime.UtcNow.Date;
        var task1 = CreateTaskItem(project.Id, "Task Due Tomorrow", TaskItemStatus.ToDo, dueDateUtc: today.AddDays(1).ToUniversalTime());
        var task2 = CreateTaskItem(project.Id, "Task Due Today", TaskItemStatus.ToDo, dueDateUtc: today.ToUniversalTime());
        var task3 = CreateTaskItem(project.Id, "No Due Date", TaskItemStatus.ToDo, dueDateUtc: null);

        await dbContext.Tasks.AddRangeAsync(task1, task2, task3);
        await dbContext.SaveChangesAsync();

        var repository = new ProjectRepository(dbContext);

        // Act
        var result = await repository.GetDetailAsync(project.Id, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result!.OpenTasks.Count);
        // Tasks with due dates come first (ordered by due date), tasks without come last
        Assert.NotNull(result.OpenTasks[0].DueDateUtc);
        Assert.NotNull(result.OpenTasks[1].DueDateUtc);
        Assert.Null(result.OpenTasks[2].DueDateUtc);
    }

    [Fact(DisplayName = "GetDetailAsync orders completed tasks by completion date descending")]
    public async Task GetDetailAsync_CompletedTasksOrdering_OrderedByCompletionDateDescending()
    {
        // Arrange
        await using var dbContext = _dbContextFactory.CreateDbContext();
        var client = CreateClient("Client");
        await dbContext.Clients.AddAsync(client);
        await dbContext.SaveChangesAsync();

        var project = CreateProject(client.Id, "Project");
        await dbContext.Projects.AddAsync(project);
        await dbContext.SaveChangesAsync();

        var now = DateTime.UtcNow;
        var task1 = CreateTaskItem(project.Id, "Completed Yesterday", TaskItemStatus.Completed, now.AddDays(-1));
        var task2 = CreateTaskItem(project.Id, "Completed Today", TaskItemStatus.Completed, now);
        var task3 = CreateTaskItem(project.Id, "Completed 2 Days Ago", TaskItemStatus.Completed, now.AddDays(-2));

        await dbContext.Tasks.AddRangeAsync(task1, task2, task3);
        await dbContext.SaveChangesAsync();

        var repository = new ProjectRepository(dbContext);

        // Act
        var result = await repository.GetDetailAsync(project.Id, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result!.CompletedTasks.Count);
        // Most recent completions first (descending)
        Assert.True(result.CompletedTasks[0].CompletedAtUtc >= result.CompletedTasks[1].CompletedAtUtc);
        Assert.True(result.CompletedTasks[1].CompletedAtUtc >= result.CompletedTasks[2].CompletedAtUtc);
    }

    [Fact(DisplayName = "GetDetailAsync throws when projectId is Guid.Empty")]
    public async Task GetDetailAsync_EmptyProjectId_ThrowsArgumentException()
    {
        // Arrange
        await using var dbContext = _dbContextFactory.CreateDbContext();
        var repository = new ProjectRepository(dbContext);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => repository.GetDetailAsync(Guid.Empty, CancellationToken.None));
        Assert.Contains("Project Id cannot be empty", ex.Message);
    }

    [Fact(DisplayName = "GetDetailAsync includes client summary info correctly")]
    public async Task GetDetailAsync_ClientSummary_IncludesAllRequiredFields()
    {
        // Arrange
        await using var dbContext = _dbContextFactory.CreateDbContext();
        var client = Client.Create(
            id: Guid.NewGuid(),
            name: "Test Client",
            lifecycleStatus: ClientLifecycleStatus.Active,
            ownerUserId: "client-owner",
            createdBy: "test-user",
            createdAtUtc: DateTime.UtcNow,
            primaryContactName: "John Doe",
            primaryEmail: "john@example.com");
        await dbContext.Clients.AddAsync(client);
        await dbContext.SaveChangesAsync();

        var project = CreateProject(client.Id, "Project");
        await dbContext.Projects.AddAsync(project);
        await dbContext.SaveChangesAsync();

        var repository = new ProjectRepository(dbContext);

        // Act
        var result = await repository.GetDetailAsync(project.Id, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(client.Id, result!.Client.Id);
        Assert.Equal("Test Client", result.Client.Name);
        Assert.Equal(ClientLifecycleStatus.Active, result.Client.LifecycleStatus);
        Assert.Equal("client-owner", result.Client.OwnerUserId);
        Assert.Equal("John Doe", result.Client.PrimaryContactName);
        Assert.Equal("john@example.com", result.Client.PrimaryEmail);
    }

    // Test helpers

    private static Client CreateClient(string name) =>
        Client.Create(
            id: Guid.NewGuid(),
            name: name,
            lifecycleStatus: ClientLifecycleStatus.Active,
            ownerUserId: "test-owner",
            createdBy: "test-user",
            createdAtUtc: DateTime.UtcNow);

    private static Project CreateProject(
        Guid clientId,
        string name,
        ProjectStatus status = ProjectStatus.Planned,
        ProjectPriority priority = ProjectPriority.Normal,
        string? description = null,
        string ownerUserId = "project-owner") =>
        Project.Create(
            id: Guid.NewGuid(),
            clientId: clientId,
            name: name,
            status: status,
            priority: priority,
            ownerUserId: ownerUserId,
            createdBy: "test-user",
            createdAtUtc: DateTime.UtcNow,
            description: description);

    private static TaskItem CreateTaskItem(
        Guid projectId,
        string title,
        TaskItemStatus status = TaskItemStatus.Backlog,
        DateTime? completedAtUtc = null,
        DateTime? dueDateUtc = null) =>
        TaskItem.Create(
            id: Guid.NewGuid(),
            projectId: projectId,
            title: title,
            status: status,
            priority: TaskItemPriority.Normal,
            createdBy: "test-user",
            createdAtUtc: DateTime.UtcNow,
            completedAtUtc: status == TaskItemStatus.Completed ? completedAtUtc ?? DateTime.UtcNow : null,
            dueDateUtc: dueDateUtc);
}

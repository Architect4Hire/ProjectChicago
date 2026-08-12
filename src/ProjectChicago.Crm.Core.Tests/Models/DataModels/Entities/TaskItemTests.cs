using ProjectChicago.Crm.Core.Models.DataModels.Entities;
using Xunit;

namespace ProjectChicago.Crm.Core.Tests.Models.DataModels.Entities;

// Entity-level invariant tests only (TASK-001..016, DATA-003, DATA-006..008). No EF/persistence
// involvement - these assert what TaskItem.Create enforces regardless of how it is later stored.
public class TaskItemTests
{
    private static readonly DateTime CreatedAtUtc = new(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);

    private static TaskItem CreateValidTask(
        TaskItemStatus status = TaskItemStatus.Backlog,
        TaskItemPriority priority = TaskItemPriority.Normal,
        DateTime? createdAtUtc = null,
        DateTime? completedAtUtc = null) =>
        TaskItem.Create(
            id: Guid.NewGuid(),
            projectId: Guid.NewGuid(),
            title: "Draft proposal",
            status: status,
            priority: priority,
            createdBy: "creator-1",
            createdAtUtc: createdAtUtc ?? CreatedAtUtc,
            description: "Draft the client proposal document.",
            assignedUserId: "assignee-1",
            startDateUtc: CreatedAtUtc,
            dueDateUtc: CreatedAtUtc.AddDays(7),
            completedAtUtc: completedAtUtc,
            notes: "Waiting on client input.");

    [Fact]
    public void Create_WithValidArguments_SetsAllProvidedValues()
    {
        var id = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var dueDateUtc = CreatedAtUtc.AddDays(7);

        var task = TaskItem.Create(
            id: id,
            projectId: projectId,
            title: "Draft proposal",
            status: TaskItemStatus.InProgress,
            priority: TaskItemPriority.High,
            createdBy: "creator-1",
            createdAtUtc: CreatedAtUtc,
            description: "Draft the client proposal document.",
            assignedUserId: "assignee-1",
            startDateUtc: CreatedAtUtc,
            dueDateUtc: dueDateUtc,
            notes: "Waiting on client input.");

        Assert.Equal(id, task.Id);
        Assert.Equal(projectId, task.ProjectId);
        Assert.Equal("Draft proposal", task.Title);
        Assert.Equal(TaskItemStatus.InProgress, task.Status);
        Assert.Equal(TaskItemPriority.High, task.Priority);
        Assert.Equal("assignee-1", task.AssignedUserId);
        Assert.Equal("Draft the client proposal document.", task.Description);
        Assert.Equal(CreatedAtUtc, task.StartDateUtc);
        Assert.Equal(dueDateUtc, task.DueDateUtc);
        Assert.Null(task.CompletedAtUtc);
        Assert.Equal("Waiting on client input.", task.Notes);
    }

    [Fact]
    public void Create_WithoutOptionalFields_LeavesThemNull()
    {
        var task = TaskItem.Create(
            id: Guid.NewGuid(),
            projectId: Guid.NewGuid(),
            title: "Draft proposal",
            status: TaskItemStatus.Backlog,
            priority: TaskItemPriority.Normal,
            createdBy: "creator-1",
            createdAtUtc: CreatedAtUtc);

        Assert.Null(task.Description);
        Assert.Null(task.AssignedUserId);
        Assert.Null(task.StartDateUtc);
        Assert.Null(task.DueDateUtc);
        Assert.Null(task.CompletedAtUtc);
        Assert.Null(task.Notes);
    }

    [Fact]
    public void Create_SetsLastModifiedMetadataEqualToCreatedMetadata()
    {
        var task = CreateValidTask();

        Assert.Equal(task.CreatedAtUtc, task.LastModifiedAtUtc);
        Assert.Equal(task.CreatedBy, task.LastModifiedBy);
    }

    [Fact]
    public void Create_AssignsAnEmptyRowVersion_UntilPersistence()
    {
        var task = CreateValidTask();

        Assert.Empty(task.RowVersion);
    }

    [Fact]
    public void Create_WithEmptyId_Throws()
    {
        var exception = Assert.Throws<ArgumentException>(() => TaskItem.Create(
            id: Guid.Empty,
            projectId: Guid.NewGuid(),
            title: "Draft proposal",
            status: TaskItemStatus.Backlog,
            priority: TaskItemPriority.Normal,
            createdBy: "creator-1",
            createdAtUtc: CreatedAtUtc));

        Assert.Equal("id", exception.ParamName);
    }

    [Fact]
    public void Create_WithEmptyProjectId_Throws_FromData003()
    {
        var exception = Assert.Throws<ArgumentException>(() => TaskItem.Create(
            id: Guid.NewGuid(),
            projectId: Guid.Empty,
            title: "Draft proposal",
            status: TaskItemStatus.Backlog,
            priority: TaskItemPriority.Normal,
            createdBy: "creator-1",
            createdAtUtc: CreatedAtUtc));

        Assert.Equal("projectId", exception.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithNullOrWhitespaceTitle_Throws(string? title)
    {
        var exception = Assert.Throws<ArgumentException>(() => TaskItem.Create(
            id: Guid.NewGuid(),
            projectId: Guid.NewGuid(),
            title: title!,
            status: TaskItemStatus.Backlog,
            priority: TaskItemPriority.Normal,
            createdBy: "creator-1",
            createdAtUtc: CreatedAtUtc));

        Assert.Equal("title", exception.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithNullOrWhitespaceCreatedBy_Throws(string? createdBy)
    {
        var exception = Assert.Throws<ArgumentException>(() => TaskItem.Create(
            id: Guid.NewGuid(),
            projectId: Guid.NewGuid(),
            title: "Draft proposal",
            status: TaskItemStatus.Backlog,
            priority: TaskItemPriority.Normal,
            createdBy: createdBy!,
            createdAtUtc: CreatedAtUtc));

        Assert.Equal("createdBy", exception.ParamName);
    }

    [Fact]
    public void Create_WithUndefinedStatus_Throws()
    {
        var exception = Assert.Throws<ArgumentException>(() => TaskItem.Create(
            id: Guid.NewGuid(),
            projectId: Guid.NewGuid(),
            title: "Draft proposal",
            status: (TaskItemStatus)999,
            priority: TaskItemPriority.Normal,
            createdBy: "creator-1",
            createdAtUtc: CreatedAtUtc));

        Assert.Equal("status", exception.ParamName);
    }

    [Fact]
    public void Create_WithUndefinedPriority_Throws()
    {
        var exception = Assert.Throws<ArgumentException>(() => TaskItem.Create(
            id: Guid.NewGuid(),
            projectId: Guid.NewGuid(),
            title: "Draft proposal",
            status: TaskItemStatus.Backlog,
            priority: (TaskItemPriority)999,
            createdBy: "creator-1",
            createdAtUtc: CreatedAtUtc));

        Assert.Equal("priority", exception.ParamName);
    }

    [Theory]
    [InlineData(TaskItemStatus.Backlog)]
    [InlineData(TaskItemStatus.ToDo)]
    [InlineData(TaskItemStatus.InProgress)]
    [InlineData(TaskItemStatus.Blocked)]
    [InlineData(TaskItemStatus.Cancelled)]
    public void Create_AllowsEveryNonCompletedInitialStatus_FromTask010(TaskItemStatus status)
    {
        var task = CreateValidTask(status: status);

        Assert.Equal(status, task.Status);
    }

    [Fact]
    public void Create_WithCompletedStatusAndCompletedAtUtc_Succeeds_FromTask011()
    {
        var task = CreateValidTask(
            status: TaskItemStatus.Completed,
            completedAtUtc: CreatedAtUtc.AddDays(3));

        Assert.Equal(TaskItemStatus.Completed, task.Status);
        Assert.Equal(CreatedAtUtc.AddDays(3), task.CompletedAtUtc);
    }

    [Fact]
    public void Create_WithCompletedStatusAndNoCompletedAtUtc_Throws_FromTask011()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => CreateValidTask(status: TaskItemStatus.Completed, completedAtUtc: null));

        Assert.Equal("completedAtUtc", exception.ParamName);
    }

    [Fact]
    public void Create_WithLocalCreatedAtUtc_Throws()
    {
        var localTime = DateTime.SpecifyKind(CreatedAtUtc, DateTimeKind.Local);

        var exception = Assert.Throws<ArgumentException>(() => CreateValidTask(createdAtUtc: localTime));

        Assert.Equal("createdAtUtc", exception.ParamName);
    }

    [Fact]
    public void Create_WithUnspecifiedCreatedAtUtcKind_Throws()
    {
        var unspecifiedTime = DateTime.SpecifyKind(CreatedAtUtc, DateTimeKind.Unspecified);

        var exception = Assert.Throws<ArgumentException>(() => CreateValidTask(createdAtUtc: unspecifiedTime));

        Assert.Equal("createdAtUtc", exception.ParamName);
    }

    [Fact]
    public void Create_WithLocalStartDateUtc_Throws()
    {
        var localTime = DateTime.SpecifyKind(CreatedAtUtc, DateTimeKind.Local);

        var exception = Assert.Throws<ArgumentException>(() => TaskItem.Create(
            id: Guid.NewGuid(),
            projectId: Guid.NewGuid(),
            title: "Draft proposal",
            status: TaskItemStatus.Backlog,
            priority: TaskItemPriority.Normal,
            createdBy: "creator-1",
            createdAtUtc: CreatedAtUtc,
            startDateUtc: localTime));

        Assert.Equal("startDateUtc", exception.ParamName);
    }

    [Fact]
    public void Create_WithLocalDueDateUtc_Throws()
    {
        var localTime = DateTime.SpecifyKind(CreatedAtUtc, DateTimeKind.Local);

        var exception = Assert.Throws<ArgumentException>(() => TaskItem.Create(
            id: Guid.NewGuid(),
            projectId: Guid.NewGuid(),
            title: "Draft proposal",
            status: TaskItemStatus.Backlog,
            priority: TaskItemPriority.Normal,
            createdBy: "creator-1",
            createdAtUtc: CreatedAtUtc,
            dueDateUtc: localTime));

        Assert.Equal("dueDateUtc", exception.ParamName);
    }

    [Fact]
    public void Create_WithLocalCompletedAtUtc_Throws()
    {
        var localTime = DateTime.SpecifyKind(CreatedAtUtc, DateTimeKind.Local);

        var exception = Assert.Throws<ArgumentException>(() => TaskItem.Create(
            id: Guid.NewGuid(),
            projectId: Guid.NewGuid(),
            title: "Draft proposal",
            status: TaskItemStatus.Completed,
            priority: TaskItemPriority.Normal,
            createdBy: "creator-1",
            createdAtUtc: CreatedAtUtc,
            completedAtUtc: localTime));

        Assert.Equal("completedAtUtc", exception.ParamName);
    }
}

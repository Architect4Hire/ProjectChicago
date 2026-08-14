using ProjectChicago.Crm.Core.Models.DataModels.Entities;
using Xunit;

namespace ProjectChicago.Crm.Core.Tests.Business;

// Unit tests for Task status transition state machine (TASK-010..012). Tests verify allowed
// transitions, rejected invalid transitions, and completion timestamp handling. All tests focus
// on the entity-layer state machine behavior; persistence and audit concerns belong to
// Data/Business-layer integration tests.
public class TaskStatusTransitionTests
{
    private static TaskItem CreateTask(
        TaskItemStatus status = TaskItemStatus.Backlog,
        DateTime? completedAtUtc = null)
    {
        var task = TaskItem.Create(
            id: Guid.NewGuid(),
            projectId: Guid.NewGuid(),
            title: "Test Task",
            status: status,
            priority: TaskItemPriority.Normal,
            createdBy: "creator",
            createdAtUtc: DateTime.UtcNow,
            completedAtUtc: completedAtUtc);
        return task;
    }

    #region Backlog Transitions

    [Fact]
    public void SetStatus_BacklogToToDo_Succeeds()
    {
        // Arrange
        var task = CreateTask(TaskItemStatus.Backlog);
        var modifiedAtUtc = DateTime.UtcNow;

        // Act
        var (previousStatus, newStatus) = task.SetStatus(
            TaskItemStatus.ToDo,
            "modifier",
            modifiedAtUtc);

        // Assert
        Assert.Equal(TaskItemStatus.Backlog, previousStatus);
        Assert.Equal(TaskItemStatus.ToDo, newStatus);
        Assert.Equal(TaskItemStatus.ToDo, task.Status);
        Assert.Null(task.CompletedAtUtc);
    }

    [Fact]
    public void SetStatus_BacklogToCancelled_Succeeds()
    {
        // Arrange
        var task = CreateTask(TaskItemStatus.Backlog);
        var modifiedAtUtc = DateTime.UtcNow;

        // Act
        var (previousStatus, newStatus) = task.SetStatus(
            TaskItemStatus.Cancelled,
            "modifier",
            modifiedAtUtc);

        // Assert
        Assert.Equal(TaskItemStatus.Backlog, previousStatus);
        Assert.Equal(TaskItemStatus.Cancelled, newStatus);
        Assert.Equal(TaskItemStatus.Cancelled, task.Status);
        Assert.Null(task.CompletedAtUtc);
    }

    [Fact]
    public void SetStatus_BacklogToCompleted_Succeeds_SetsCompletionTimestamp()
    {
        // Arrange
        var task = CreateTask(TaskItemStatus.Backlog);
        var modifiedAtUtc = DateTime.UtcNow.AddHours(-1);

        // Act
        var (previousStatus, newStatus) = task.SetStatus(
            TaskItemStatus.Completed,
            "modifier",
            modifiedAtUtc);

        // Assert
        Assert.Equal(TaskItemStatus.Backlog, previousStatus);
        Assert.Equal(TaskItemStatus.Completed, newStatus);
        Assert.Equal(TaskItemStatus.Completed, task.Status);
        Assert.NotNull(task.CompletedAtUtc);
        Assert.Equal(modifiedAtUtc, task.CompletedAtUtc);
    }

    #endregion

    #region ToDo Transitions

    [Fact]
    public void SetStatus_ToDoToInProgress_Succeeds()
    {
        // Arrange
        var task = CreateTask(TaskItemStatus.ToDo);

        // Act
        var (previousStatus, newStatus) = task.SetStatus(
            TaskItemStatus.InProgress,
            "modifier",
            DateTime.UtcNow);

        // Assert
        Assert.Equal(TaskItemStatus.ToDo, previousStatus);
        Assert.Equal(TaskItemStatus.InProgress, newStatus);
        Assert.Equal(TaskItemStatus.InProgress, task.Status);
        Assert.Null(task.CompletedAtUtc);
    }

    [Fact]
    public void SetStatus_ToDoToBlocked_Succeeds()
    {
        // Arrange
        var task = CreateTask(TaskItemStatus.ToDo);

        // Act
        var (previousStatus, newStatus) = task.SetStatus(
            TaskItemStatus.Blocked,
            "modifier",
            DateTime.UtcNow);

        // Assert
        Assert.Equal(TaskItemStatus.ToDo, previousStatus);
        Assert.Equal(TaskItemStatus.Blocked, newStatus);
        Assert.Equal(TaskItemStatus.Blocked, task.Status);
        Assert.Null(task.CompletedAtUtc);
    }

    [Fact]
    public void SetStatus_ToDoToCompleted_Succeeds_SetsCompletionTimestamp()
    {
        // Arrange
        var task = CreateTask(TaskItemStatus.ToDo);
        var modifiedAtUtc = DateTime.UtcNow.AddMinutes(-5);

        // Act
        var (previousStatus, newStatus) = task.SetStatus(
            TaskItemStatus.Completed,
            "modifier",
            modifiedAtUtc);

        // Assert
        Assert.Equal(TaskItemStatus.ToDo, previousStatus);
        Assert.Equal(TaskItemStatus.Completed, newStatus);
        Assert.Equal(TaskItemStatus.Completed, task.Status);
        Assert.Equal(modifiedAtUtc, task.CompletedAtUtc);
    }

    #endregion

    #region InProgress Transitions

    [Fact]
    public void SetStatus_InProgressToBlocked_Succeeds()
    {
        // Arrange
        var task = CreateTask(TaskItemStatus.InProgress);

        // Act
        var (previousStatus, newStatus) = task.SetStatus(
            TaskItemStatus.Blocked,
            "modifier",
            DateTime.UtcNow);

        // Assert
        Assert.Equal(TaskItemStatus.InProgress, previousStatus);
        Assert.Equal(TaskItemStatus.Blocked, newStatus);
    }

    [Fact]
    public void SetStatus_InProgressToCompleted_Succeeds_SetsCompletionTimestamp()
    {
        // Arrange
        var task = CreateTask(TaskItemStatus.InProgress);
        var modifiedAtUtc = DateTime.UtcNow;

        // Act
        var (previousStatus, newStatus) = task.SetStatus(
            TaskItemStatus.Completed,
            "modifier",
            modifiedAtUtc);

        // Assert
        Assert.Equal(TaskItemStatus.InProgress, previousStatus);
        Assert.Equal(TaskItemStatus.Completed, newStatus);
        Assert.Equal(modifiedAtUtc, task.CompletedAtUtc);
    }

    #endregion

    #region Blocked Transitions

    [Fact]
    public void SetStatus_BlockedToInProgress_Succeeds()
    {
        // Arrange
        var task = CreateTask(TaskItemStatus.Blocked);

        // Act
        var (previousStatus, newStatus) = task.SetStatus(
            TaskItemStatus.InProgress,
            "modifier",
            DateTime.UtcNow);

        // Assert
        Assert.Equal(TaskItemStatus.Blocked, previousStatus);
        Assert.Equal(TaskItemStatus.InProgress, newStatus);
        Assert.Null(task.CompletedAtUtc);
    }

    [Fact]
    public void SetStatus_BlockedToCompleted_Succeeds_SetsCompletionTimestamp()
    {
        // Arrange
        var task = CreateTask(TaskItemStatus.Blocked);
        var modifiedAtUtc = DateTime.UtcNow;

        // Act
        var (previousStatus, newStatus) = task.SetStatus(
            TaskItemStatus.Completed,
            "modifier",
            modifiedAtUtc);

        // Assert
        Assert.Equal(TaskItemStatus.Blocked, previousStatus);
        Assert.Equal(TaskItemStatus.Completed, newStatus);
        Assert.Equal(modifiedAtUtc, task.CompletedAtUtc);
    }

    #endregion

    #region Completed Terminal State

    [Fact]
    public void SetStatus_CompletedToAnything_ThrowsInvalidOperationException()
    {
        // Arrange
        var completedAtUtc = DateTime.UtcNow.AddHours(-1);
        var task = CreateTask(TaskItemStatus.Completed, completedAtUtc);

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(
            () => task.SetStatus(TaskItemStatus.ToDo, "modifier", DateTime.UtcNow));

        Assert.Contains("cannot transition to another status via SetStatus", ex.Message);
        Assert.Contains("use Reopen instead", ex.Message);
    }

    [Fact]
    public void SetStatus_CompletedToCompleted_ThrowsInvalidOperationException()
    {
        // Arrange
        var completedAtUtc = DateTime.UtcNow.AddHours(-1);
        var task = CreateTask(TaskItemStatus.Completed, completedAtUtc);

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(
            () => task.SetStatus(TaskItemStatus.Completed, "modifier", DateTime.UtcNow));

        Assert.Contains("cannot transition to another status via SetStatus", ex.Message);
    }

    #endregion

    #region Cancelled Terminal State

    [Fact]
    public void SetStatus_CancelledToAnything_ThrowsInvalidOperationException()
    {
        // Arrange
        var task = CreateTask(TaskItemStatus.Cancelled);

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(
            () => task.SetStatus(TaskItemStatus.ToDo, "modifier", DateTime.UtcNow));

        Assert.Contains("Cancelled Task cannot transition", ex.Message);
    }

    [Fact]
    public void SetStatus_CancelledToCompleted_ThrowsInvalidOperationException()
    {
        // Arrange
        var task = CreateTask(TaskItemStatus.Cancelled);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(
            () => task.SetStatus(TaskItemStatus.Completed, "modifier", DateTime.UtcNow));
    }

    #endregion

    #region Reopen Behavior

    [Fact]
    public void SetReopen_CompletedToToDo_Succeeds_ClearsCompletionTimestamp()
    {
        // Arrange
        var completedAtUtc = DateTime.UtcNow.AddHours(-2);
        var task = CreateTask(TaskItemStatus.Completed, completedAtUtc);
        Assert.NotNull(task.CompletedAtUtc);

        // Act
        var (previousStatus, newStatus) = task.SetReopen(
            TaskItemStatus.ToDo,
            "reopener",
            DateTime.UtcNow);

        // Assert
        Assert.Equal(TaskItemStatus.Completed, previousStatus);
        Assert.Equal(TaskItemStatus.ToDo, newStatus);
        Assert.Equal(TaskItemStatus.ToDo, task.Status);
        Assert.Null(task.CompletedAtUtc);
    }

    [Fact]
    public void SetReopen_CompletedToBacklog_Succeeds_ClearsCompletionTimestamp()
    {
        // Arrange
        var completedAtUtc = DateTime.UtcNow.AddHours(-1);
        var task = CreateTask(TaskItemStatus.Completed, completedAtUtc);

        // Act
        var (previousStatus, newStatus) = task.SetReopen(
            TaskItemStatus.Backlog,
            "reopener",
            DateTime.UtcNow);

        // Assert
        Assert.Equal(TaskItemStatus.Completed, previousStatus);
        Assert.Equal(TaskItemStatus.Backlog, newStatus);
        Assert.Null(task.CompletedAtUtc);
    }

    [Fact]
    public void SetReopen_CompletedToInProgress_Succeeds()
    {
        // Arrange
        var completedAtUtc = DateTime.UtcNow.AddHours(-1);
        var task = CreateTask(TaskItemStatus.Completed, completedAtUtc);

        // Act
        var (previousStatus, newStatus) = task.SetReopen(
            TaskItemStatus.InProgress,
            "reopener",
            DateTime.UtcNow);

        // Assert
        Assert.Equal(TaskItemStatus.Completed, previousStatus);
        Assert.Equal(TaskItemStatus.InProgress, newStatus);
        Assert.Null(task.CompletedAtUtc);
    }

    [Fact]
    public void SetReopen_CompletedToBlocked_Succeeds()
    {
        // Arrange
        var completedAtUtc = DateTime.UtcNow.AddHours(-1);
        var task = CreateTask(TaskItemStatus.Completed, completedAtUtc);

        // Act
        var (previousStatus, newStatus) = task.SetReopen(
            TaskItemStatus.Blocked,
            "reopener",
            DateTime.UtcNow);

        // Assert
        Assert.Equal(TaskItemStatus.Completed, previousStatus);
        Assert.Equal(TaskItemStatus.Blocked, newStatus);
        Assert.Null(task.CompletedAtUtc);
    }

    [Fact]
    public void SetReopen_NonCompletedTask_ThrowsInvalidOperationException()
    {
        // Arrange
        var task = CreateTask(TaskItemStatus.ToDo);

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(
            () => task.SetReopen(TaskItemStatus.Backlog, "reopener", DateTime.UtcNow));

        Assert.Contains("Only a Completed Task can be reopened", ex.Message);
    }

    [Fact]
    public void SetReopen_CompletedToCompleted_ThrowsInvalidOperationException()
    {
        // Arrange
        var completedAtUtc = DateTime.UtcNow.AddHours(-1);
        var task = CreateTask(TaskItemStatus.Completed, completedAtUtc);

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(
            () => task.SetReopen(TaskItemStatus.Completed, "reopener", DateTime.UtcNow));

        Assert.Contains("must transition to an open status", ex.Message);
    }

    [Fact]
    public void SetReopen_CompletedToCancelled_ThrowsInvalidOperationException()
    {
        // Arrange
        var completedAtUtc = DateTime.UtcNow.AddHours(-1);
        var task = CreateTask(TaskItemStatus.Completed, completedAtUtc);

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(
            () => task.SetReopen(TaskItemStatus.Cancelled, "reopener", DateTime.UtcNow));

        Assert.Contains("must transition to an open status", ex.Message);
    }

    #endregion

    #region LastModifiedBy/At Tracking

    [Fact]
    public void SetStatus_UpdatesLastModifiedByAndAt()
    {
        // Arrange
        var task = CreateTask(TaskItemStatus.Backlog);
        var originalModifiedBy = task.LastModifiedBy;
        var originalModifiedAt = task.LastModifiedAtUtc;

        var newModifiedBy = "new-modifier";
        var newModifiedAt = DateTime.UtcNow.AddSeconds(10);

        // Act
        task.SetStatus(TaskItemStatus.ToDo, newModifiedBy, newModifiedAt);

        // Assert
        Assert.NotEqual(originalModifiedBy, task.LastModifiedBy);
        Assert.NotEqual(originalModifiedAt, task.LastModifiedAtUtc);
        Assert.Equal(newModifiedBy, task.LastModifiedBy);
        Assert.Equal(newModifiedAt, task.LastModifiedAtUtc);
    }

    [Fact]
    public void SetReopen_UpdatesLastModifiedByAndAt()
    {
        // Arrange
        var completedAtUtc = DateTime.UtcNow.AddHours(-1);
        var task = CreateTask(TaskItemStatus.Completed, completedAtUtc);
        var originalModifiedBy = task.LastModifiedBy;

        var newModifiedBy = "reopener";
        var newModifiedAt = DateTime.UtcNow;

        // Act
        task.SetReopen(TaskItemStatus.ToDo, newModifiedBy, newModifiedAt);

        // Assert
        Assert.NotEqual(originalModifiedBy, task.LastModifiedBy);
        Assert.Equal(newModifiedBy, task.LastModifiedBy);
        Assert.Equal(newModifiedAt, task.LastModifiedAtUtc);
    }

    #endregion

    #region Validation

    [Fact]
    public void SetStatus_SameStatusAsCurrentThrowsInvalidOperationException()
    {
        // Arrange
        var task = CreateTask(TaskItemStatus.ToDo);

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(
            () => task.SetStatus(TaskItemStatus.ToDo, "modifier", DateTime.UtcNow));

        Assert.Contains("already set to the specified status", ex.Message);
    }

    [Fact]
    public void SetStatus_UndefinedStatusThrowsArgumentException()
    {
        // Arrange
        var task = CreateTask(TaskItemStatus.Backlog);

        // Act & Assert
        Assert.Throws<ArgumentException>(
            () => task.SetStatus((TaskItemStatus)999, "modifier", DateTime.UtcNow));
    }

    [Fact]
    public void SetStatus_NullModifiedByThrowsArgumentException()
    {
        // Arrange
        var task = CreateTask(TaskItemStatus.Backlog);

        // Act & Assert
        Assert.Throws<ArgumentException>(
            () => task.SetStatus(TaskItemStatus.ToDo, null!, DateTime.UtcNow));
    }

    [Fact]
    public void SetStatus_EmptyModifiedByThrowsArgumentException()
    {
        // Arrange
        var task = CreateTask(TaskItemStatus.Backlog);

        // Act & Assert
        Assert.Throws<ArgumentException>(
            () => task.SetStatus(TaskItemStatus.ToDo, string.Empty, DateTime.UtcNow));
    }

    [Fact]
    public void SetStatus_NonUtcModifiedAtThrowsArgumentException()
    {
        // Arrange
        var task = CreateTask(TaskItemStatus.Backlog);
        var localTime = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Local);

        // Act & Assert
        Assert.Throws<ArgumentException>(
            () => task.SetStatus(TaskItemStatus.ToDo, "modifier", localTime));
    }

    #endregion
}

using Moq;
using ProjectChicago.Contracts.Audit;
using ProjectChicago.Crm.Contracts.Common;
using ProjectChicago.Crm.Contracts.Tasks;
using ProjectChicago.Crm.Core.Business;
using ProjectChicago.Crm.Core.Data;
using ProjectChicago.Crm.Core.Models.DataModels.Entities;
using ProjectChicago.Shared.Correlation;
using Xunit;

namespace ProjectChicago.Crm.Core.Tests.Business;

public class TaskBusinessTests
{
    private readonly Mock<ITaskData> _mockTaskData;
    private readonly TaskBusiness _business;

    public TaskBusinessTests()
    {
        _mockTaskData = new Mock<ITaskData>();
        _business = new TaskBusiness(_mockTaskData.Object);
    }

    #region AssignAsync - Initial Assignment

    [Fact]
    public async Task AssignAsync_InitialAssignment_ReturnsUpdatedTaskWithAssignedUser()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var originalTask = TaskItem.Create(
            id: taskId,
            projectId: projectId,
            title: "Test Task",
            status: TaskItemStatus.ToDo,
            priority: TaskItemPriority.Normal,
            createdBy: "creator",
            createdAtUtc: DateTime.UtcNow);

        var request = new AssignTaskViewModel
        {
            TaskId = taskId,
            AssignedUserId = "user-123",
            ConcurrencyToken = Convert.ToBase64String([1, 2, 3]),
        };

        var actor = ActorContext.ForUser("actor-456");
        var requestContext = RequestContext.FromPropagated(
            traceId: "trace-123",
            correlationId: "corr-123",
            causationId: "cause-123",
            requestId: "req-123",
            actor: actor);

        _mockTaskData.Setup(d => d.GetByIdAsync(taskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(originalTask);

        // Act
        var result = await _business.AssignAsync(
            request, actor, requestContext, DateTime.UtcNow, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(taskId, result.Id);
        Assert.Equal("user-123", result.AssignedUserId);

        // Verify AssignAsync was called with the mutated task and audit fact
        _mockTaskData.Verify(
            d => d.AssignAsync(It.IsAny<TaskItem>(), It.IsAny<EntityMutationAudited>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AssignAsync_InitialAssignment_CreatesAuditFactWithAssignedAction()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var originalTask = TaskItem.Create(
            id: taskId,
            projectId: projectId,
            title: "Test Task",
            status: TaskItemStatus.Backlog,
            priority: TaskItemPriority.Normal,
            createdBy: "creator",
            createdAtUtc: DateTime.UtcNow);

        var request = new AssignTaskViewModel
        {
            TaskId = taskId,
            AssignedUserId = "user-789",
            ConcurrencyToken = Convert.ToBase64String([1, 2, 3]),
        };

        var actor = ActorContext.ForUser("actor-999");
        var requestContext = RequestContext.FromPropagated(
            traceId: "trace-456",
            correlationId: "corr-456",
            causationId: "cause-456",
            requestId: "req-456",
            actor: actor);

        EntityMutationAudited? capturedAuditFact = null;
        _mockTaskData.Setup(d => d.GetByIdAsync(taskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(originalTask);
        _mockTaskData.Setup(d => d.AssignAsync(It.IsAny<TaskItem>(), It.IsAny<EntityMutationAudited>(), It.IsAny<CancellationToken>()))
            .Callback<TaskItem, EntityMutationAudited, CancellationToken>((t, a, ct) => capturedAuditFact = a)
            .Returns(Task.CompletedTask);

        // Act
        await _business.AssignAsync(
            request, actor, requestContext, DateTime.UtcNow, CancellationToken.None);

        // Assert
        Assert.NotNull(capturedAuditFact);
        Assert.Equal(AuditActions.Assigned, capturedAuditFact.Action);
        Assert.Equal(taskId, capturedAuditFact.EntityId);
        Assert.Equal("actor-999", capturedAuditFact.ActorId);
        Assert.Contains(nameof(TaskItem.AssignedUserId), capturedAuditFact.ChangedFields);
        Assert.Null(capturedAuditFact.PreviousValues);
        Assert.NotNull(capturedAuditFact.NewValues);
        Assert.Equal("user-789", capturedAuditFact.NewValues[nameof(TaskItem.AssignedUserId)]);
    }

    #endregion

    #region AssignAsync - Reassignment

    [Fact]
    public async Task AssignAsync_Reassignment_ReturnsUpdatedTaskWithNewAssignedUser()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var originalTask = TaskItem.Create(
            id: taskId,
            projectId: projectId,
            title: "Test Task",
            status: TaskItemStatus.InProgress,
            priority: TaskItemPriority.High,
            createdBy: "creator",
            createdAtUtc: DateTime.UtcNow,
            assignedUserId: "old-user");

        var request = new AssignTaskViewModel
        {
            TaskId = taskId,
            AssignedUserId = "new-user",
            ConcurrencyToken = Convert.ToBase64String([1, 2, 3]),
        };

        var actor = ActorContext.ForUser("reassigner");
        var requestContext = RequestContext.FromPropagated(
            traceId: "trace-789",
            correlationId: "corr-789",
            causationId: "cause-789",
            requestId: "req-789",
            actor: actor);

        _mockTaskData.Setup(d => d.GetByIdAsync(taskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(originalTask);

        // Act
        var result = await _business.AssignAsync(
            request, actor, requestContext, DateTime.UtcNow, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("new-user", result.AssignedUserId);
    }

    [Fact]
    public async Task AssignAsync_Reassignment_CreatesAuditFactWithReassignedAction()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var originalTask = TaskItem.Create(
            id: taskId,
            projectId: projectId,
            title: "Test Task",
            status: TaskItemStatus.ToDo,
            priority: TaskItemPriority.Normal,
            createdBy: "creator",
            createdAtUtc: DateTime.UtcNow,
            assignedUserId: "previous-user");

        var request = new AssignTaskViewModel
        {
            TaskId = taskId,
            AssignedUserId = "new-assignee",
            ConcurrencyToken = Convert.ToBase64String([1, 2, 3]),
        };

        var actor = ActorContext.ForUser("reassigner");
        var requestContext = RequestContext.FromPropagated(
            traceId: "trace-abc",
            correlationId: "corr-abc",
            causationId: "cause-abc",
            requestId: "req-abc",
            actor: actor);

        EntityMutationAudited? capturedAuditFact = null;
        _mockTaskData.Setup(d => d.GetByIdAsync(taskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(originalTask);
        _mockTaskData.Setup(d => d.AssignAsync(It.IsAny<TaskItem>(), It.IsAny<EntityMutationAudited>(), It.IsAny<CancellationToken>()))
            .Callback<TaskItem, EntityMutationAudited, CancellationToken>((t, a, ct) => capturedAuditFact = a)
            .Returns(Task.CompletedTask);

        // Act
        await _business.AssignAsync(
            request, actor, requestContext, DateTime.UtcNow, CancellationToken.None);

        // Assert
        Assert.NotNull(capturedAuditFact);
        Assert.Equal(AuditActions.Reassigned, capturedAuditFact.Action);
        Assert.Equal(taskId, capturedAuditFact.EntityId);
        Assert.NotNull(capturedAuditFact.PreviousValues);
        Assert.Equal("previous-user", capturedAuditFact.PreviousValues[nameof(TaskItem.AssignedUserId)]);
        Assert.NotNull(capturedAuditFact.NewValues);
        Assert.Equal("new-assignee", capturedAuditFact.NewValues[nameof(TaskItem.AssignedUserId)]);
    }

    #endregion

    #region AssignAsync - Validation Errors

    [Fact]
    public async Task AssignAsync_TaskNotFound_ThrowsArgumentException()
    {
        // Arrange
        var taskId = Guid.NewGuid();

        var request = new AssignTaskViewModel
        {
            TaskId = taskId,
            AssignedUserId = "user-123",
            ConcurrencyToken = Convert.ToBase64String([1, 2, 3]),
        };

        var actor = ActorContext.ForUser("actor-456");
        var requestContext = RequestContext.FromPropagated(
            traceId: "trace-123",
            correlationId: "corr-123",
            causationId: "cause-123",
            requestId: "req-123",
            actor: actor);

        _mockTaskData.Setup(d => d.GetByIdAsync(taskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TaskItem?)null);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _business.AssignAsync(request, actor, requestContext, DateTime.UtcNow, CancellationToken.None));
    }

    [Fact]
    public async Task AssignAsync_ReassignToSameUser_ThrowsInvalidOperationException()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var originalTask = TaskItem.Create(
            id: taskId,
            projectId: projectId,
            title: "Test Task",
            status: TaskItemStatus.ToDo,
            priority: TaskItemPriority.Normal,
            createdBy: "creator",
            createdAtUtc: DateTime.UtcNow,
            assignedUserId: "current-user");

        var request = new AssignTaskViewModel
        {
            TaskId = taskId,
            AssignedUserId = "current-user",
            ConcurrencyToken = Convert.ToBase64String([1, 2, 3]),
        };

        var actor = ActorContext.ForUser("actor-999");
        var requestContext = RequestContext.FromPropagated(
            traceId: "trace-xyz",
            correlationId: "corr-xyz",
            causationId: "cause-xyz",
            requestId: "req-xyz",
            actor: actor);

        _mockTaskData.Setup(d => d.GetByIdAsync(taskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(originalTask);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _business.AssignAsync(request, actor, requestContext, DateTime.UtcNow, CancellationToken.None));
    }

    [Fact]
    public async Task AssignAsync_CompletedTask_ThrowsInvalidOperationException()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var completedAtUtc = DateTime.UtcNow;
        var originalTask = TaskItem.Create(
            id: taskId,
            projectId: projectId,
            title: "Test Task",
            status: TaskItemStatus.Completed,
            priority: TaskItemPriority.Normal,
            createdBy: "creator",
            createdAtUtc: DateTime.UtcNow,
            completedAtUtc: completedAtUtc);

        var request = new AssignTaskViewModel
        {
            TaskId = taskId,
            AssignedUserId = "new-user",
            ConcurrencyToken = Convert.ToBase64String([1, 2, 3]),
        };

        var actor = ActorContext.ForUser("actor-111");
        var requestContext = RequestContext.FromPropagated(
            traceId: "trace-111",
            correlationId: "corr-111",
            causationId: "cause-111",
            requestId: "req-111",
            actor: actor);

        _mockTaskData.Setup(d => d.GetByIdAsync(taskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(originalTask);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _business.AssignAsync(request, actor, requestContext, DateTime.UtcNow, CancellationToken.None));
    }

    #endregion

    #region ChangePriorityAsync

    [Fact]
    public async Task ChangePriorityAsync_ValidPriorityChange_ReturnsUpdatedTaskWithNewPriority()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var originalTask = TaskItem.Create(
            id: taskId,
            projectId: projectId,
            title: "Test Task",
            status: TaskItemStatus.ToDo,
            priority: TaskItemPriority.Normal,
            createdBy: "creator",
            createdAtUtc: DateTime.UtcNow);

        var request = new ChangeTaskPriorityViewModel
        {
            TaskId = taskId,
            Priority = TaskItemPriorityContract.High,
            ConcurrencyToken = Convert.ToBase64String([1, 2, 3]),
        };

        var actor = ActorContext.ForUser("actor-123");
        var requestContext = RequestContext.FromPropagated(
            traceId: "trace-123",
            correlationId: "corr-123",
            causationId: "cause-123",
            requestId: "req-123",
            actor: actor);

        _mockTaskData.Setup(d => d.GetByIdAsync(taskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(originalTask);

        // Act
        var result = await _business.ChangePriorityAsync(
            request, actor, requestContext, DateTime.UtcNow, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(taskId, result.Id);
        Assert.Equal(TaskItemPriorityContract.High, result.Priority);

        // Verify ChangePriorityAsync was called with the mutated task and audit fact
        _mockTaskData.Verify(
            d => d.ChangePriorityAsync(It.IsAny<TaskItem>(), It.IsAny<EntityMutationAudited>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ChangePriorityAsync_ValidPriorityChange_CreatesAuditFactWithPriorityChangedAction()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var originalTask = TaskItem.Create(
            id: taskId,
            projectId: projectId,
            title: "Test Task",
            status: TaskItemStatus.Backlog,
            priority: TaskItemPriority.Low,
            createdBy: "creator",
            createdAtUtc: DateTime.UtcNow);

        var request = new ChangeTaskPriorityViewModel
        {
            TaskId = taskId,
            Priority = TaskItemPriorityContract.Critical,
            ConcurrencyToken = Convert.ToBase64String([1, 2, 3]),
        };

        var actor = ActorContext.ForUser("actor-456");
        var requestContext = RequestContext.FromPropagated(
            traceId: "trace-456",
            correlationId: "corr-456",
            causationId: "cause-456",
            requestId: "req-456",
            actor: actor);

        EntityMutationAudited? capturedAuditFact = null;
        _mockTaskData.Setup(d => d.GetByIdAsync(taskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(originalTask);
        _mockTaskData.Setup(d => d.ChangePriorityAsync(It.IsAny<TaskItem>(), It.IsAny<EntityMutationAudited>(), It.IsAny<CancellationToken>()))
            .Callback<TaskItem, EntityMutationAudited, CancellationToken>((t, a, ct) => capturedAuditFact = a)
            .Returns(Task.CompletedTask);

        // Act
        await _business.ChangePriorityAsync(
            request, actor, requestContext, DateTime.UtcNow, CancellationToken.None);

        // Assert
        Assert.NotNull(capturedAuditFact);
        Assert.Equal(AuditActions.PriorityChanged, capturedAuditFact.Action);
        Assert.Equal(taskId, capturedAuditFact.EntityId);
        Assert.Equal("actor-456", capturedAuditFact.ActorId);
        Assert.Contains(nameof(TaskItem.Priority), capturedAuditFact.ChangedFields);
        Assert.NotNull(capturedAuditFact.PreviousValues);
        Assert.Equal(TaskItemPriority.Low.ToString(), capturedAuditFact.PreviousValues[nameof(TaskItem.Priority)]);
        Assert.NotNull(capturedAuditFact.NewValues);
        Assert.Equal(TaskItemPriority.Critical.ToString(), capturedAuditFact.NewValues[nameof(TaskItem.Priority)]);
    }

    [Fact]
    public async Task ChangePriorityAsync_TaskNotFound_ThrowsArgumentException()
    {
        // Arrange
        var taskId = Guid.NewGuid();

        var request = new ChangeTaskPriorityViewModel
        {
            TaskId = taskId,
            Priority = TaskItemPriorityContract.High,
            ConcurrencyToken = Convert.ToBase64String([1, 2, 3]),
        };

        var actor = ActorContext.ForUser("actor-789");
        var requestContext = RequestContext.FromPropagated(
            traceId: "trace-789",
            correlationId: "corr-789",
            causationId: "cause-789",
            requestId: "req-789",
            actor: actor);

        _mockTaskData.Setup(d => d.GetByIdAsync(taskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TaskItem?)null);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _business.ChangePriorityAsync(request, actor, requestContext, DateTime.UtcNow, CancellationToken.None));
    }

    [Fact]
    public async Task ChangePriorityAsync_ChangeToPriority_ThrowsInvalidOperationException()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var originalTask = TaskItem.Create(
            id: taskId,
            projectId: projectId,
            title: "Test Task",
            status: TaskItemStatus.ToDo,
            priority: TaskItemPriority.Normal,
            createdBy: "creator",
            createdAtUtc: DateTime.UtcNow);

        var request = new ChangeTaskPriorityViewModel
        {
            TaskId = taskId,
            Priority = TaskItemPriorityContract.Normal,
            ConcurrencyToken = Convert.ToBase64String([1, 2, 3]),
        };

        var actor = ActorContext.ForUser("actor-999");
        var requestContext = RequestContext.FromPropagated(
            traceId: "trace-999",
            correlationId: "corr-999",
            causationId: "cause-999",
            requestId: "req-999",
            actor: actor);

        _mockTaskData.Setup(d => d.GetByIdAsync(taskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(originalTask);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _business.ChangePriorityAsync(request, actor, requestContext, DateTime.UtcNow, CancellationToken.None));
    }

    #endregion

    #region ReopenAsync

    [Fact]
    public async Task ReopenAsync_CompletedToToDo_ReturnsReopenedTaskAndCreatesAuditFact()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var completedAtUtc = DateTime.UtcNow.AddHours(-2);
        var originalTask = TaskItem.Create(
            id: taskId,
            projectId: projectId,
            title: "Test Task",
            status: TaskItemStatus.Completed,
            priority: TaskItemPriority.Normal,
            createdBy: "creator",
            createdAtUtc: DateTime.UtcNow,
            completedAtUtc: completedAtUtc);

        var request = new ReopenTaskViewModel
        {
            TaskId = taskId,
            ReopenToStatus = TaskItemStatusContract.ToDo,
            ConcurrencyToken = Convert.ToBase64String([1, 2, 3]),
        };

        var actor = ActorContext.ForUser("reopener-123");
        var requestContext = RequestContext.FromPropagated(
            traceId: "trace-reopen-1",
            correlationId: "corr-reopen-1",
            causationId: "cause-reopen-1",
            requestId: "req-reopen-1",
            actor: actor);

        _mockTaskData.Setup(d => d.GetByIdAsync(taskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(originalTask);

        // Act
        var result = await _business.ReopenAsync(
            request, actor, requestContext, DateTime.UtcNow, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(taskId, result.Id);
        Assert.Equal(TaskItemStatusContract.ToDo, result.Status);
        Assert.Null(result.CompletedAtUtc);

        _mockTaskData.Verify(
            d => d.ReopenAsync(It.IsAny<TaskItem>(), It.IsAny<EntityMutationAudited>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ReopenAsync_CreatesAuditFactWithReopenedActionAndStatusChange()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var completedAtUtc = DateTime.UtcNow.AddHours(-1);
        var originalTask = TaskItem.Create(
            id: taskId,
            projectId: projectId,
            title: "Test Task",
            status: TaskItemStatus.Completed,
            priority: TaskItemPriority.High,
            createdBy: "creator",
            createdAtUtc: DateTime.UtcNow,
            completedAtUtc: completedAtUtc);

        var request = new ReopenTaskViewModel
        {
            TaskId = taskId,
            ReopenToStatus = TaskItemStatusContract.InProgress,
            ConcurrencyToken = Convert.ToBase64String([4, 5, 6]),
        };

        var actor = ActorContext.ForUser("reopener-456");
        var requestContext = RequestContext.FromPropagated(
            traceId: "trace-reopen-2",
            correlationId: "corr-reopen-2",
            causationId: "cause-reopen-2",
            requestId: "req-reopen-2",
            actor: actor);

        EntityMutationAudited? capturedAuditFact = null;
        _mockTaskData.Setup(d => d.GetByIdAsync(taskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(originalTask);
        _mockTaskData.Setup(d => d.ReopenAsync(It.IsAny<TaskItem>(), It.IsAny<EntityMutationAudited>(), It.IsAny<CancellationToken>()))
            .Callback<TaskItem, EntityMutationAudited, CancellationToken>((t, a, ct) => capturedAuditFact = a)
            .Returns(Task.CompletedTask);

        // Act
        await _business.ReopenAsync(
            request, actor, requestContext, DateTime.UtcNow, CancellationToken.None);

        // Assert
        Assert.NotNull(capturedAuditFact);
        Assert.Equal(AuditActions.Reopened, capturedAuditFact.Action);
        Assert.Equal(taskId, capturedAuditFact.EntityId);
        Assert.Equal("reopener-456", capturedAuditFact.ActorId);
        Assert.Contains(nameof(TaskItem.Status), capturedAuditFact.ChangedFields);
        Assert.NotNull(capturedAuditFact.PreviousValues);
        Assert.Equal(TaskItemStatus.Completed.ToString(), capturedAuditFact.PreviousValues[nameof(TaskItem.Status)]);
        Assert.NotNull(capturedAuditFact.NewValues);
        Assert.Equal(TaskItemStatus.InProgress.ToString(), capturedAuditFact.NewValues[nameof(TaskItem.Status)]);
    }

    [Fact]
    public async Task ReopenAsync_AuditFactIncludesCompletedAtUTCClearance()
    {
        // Arrange: verify that the audit fact documents the clearing of CompletedAtUtc when reopening
        var taskId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var completedAtUtc = DateTime.UtcNow.AddHours(-3);
        var originalTask = TaskItem.Create(
            id: taskId,
            projectId: projectId,
            title: "Test Task",
            status: TaskItemStatus.Completed,
            priority: TaskItemPriority.Low,
            createdBy: "creator",
            createdAtUtc: DateTime.UtcNow,
            completedAtUtc: completedAtUtc);

        var request = new ReopenTaskViewModel
        {
            TaskId = taskId,
            ReopenToStatus = TaskItemStatusContract.Backlog,
            ConcurrencyToken = Convert.ToBase64String([7, 8, 9]),
        };

        var actor = ActorContext.ForUser("reopener-789");
        var requestContext = RequestContext.FromPropagated(
            traceId: "trace-reopen-3",
            correlationId: "corr-reopen-3",
            causationId: "cause-reopen-3",
            requestId: "req-reopen-3",
            actor: actor);

        EntityMutationAudited? capturedAuditFact = null;
        _mockTaskData.Setup(d => d.GetByIdAsync(taskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(originalTask);
        _mockTaskData.Setup(d => d.ReopenAsync(It.IsAny<TaskItem>(), It.IsAny<EntityMutationAudited>(), It.IsAny<CancellationToken>()))
            .Callback<TaskItem, EntityMutationAudited, CancellationToken>((t, a, ct) => capturedAuditFact = a)
            .Returns(Task.CompletedTask);

        // Act
        await _business.ReopenAsync(
            request, actor, requestContext, DateTime.UtcNow, CancellationToken.None);

        // Assert
        Assert.NotNull(capturedAuditFact);
        Assert.NotNull(capturedAuditFact.PreviousValues);
        Assert.True(capturedAuditFact.PreviousValues.ContainsKey(nameof(TaskItem.CompletedAtUtc)));
        Assert.Equal("Cleared on reopen", capturedAuditFact.PreviousValues[nameof(TaskItem.CompletedAtUtc)]);
    }

    [Fact]
    public async Task ReopenAsync_TaskNotFound_ThrowsArgumentException()
    {
        // Arrange
        var taskId = Guid.NewGuid();

        var request = new ReopenTaskViewModel
        {
            TaskId = taskId,
            ReopenToStatus = TaskItemStatusContract.ToDo,
            ConcurrencyToken = Convert.ToBase64String([1, 2, 3]),
        };

        var actor = ActorContext.ForUser("reopener-999");
        var requestContext = RequestContext.FromPropagated(
            traceId: "trace-reopen-notfound",
            correlationId: "corr-reopen-notfound",
            causationId: "cause-reopen-notfound",
            requestId: "req-reopen-notfound",
            actor: actor);

        _mockTaskData.Setup(d => d.GetByIdAsync(taskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TaskItem?)null);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _business.ReopenAsync(request, actor, requestContext, DateTime.UtcNow, CancellationToken.None));

        Assert.Contains("does not exist", ex.Message);
    }

    [Fact]
    public async Task ReopenAsync_NonCompletedTask_ThrowsInvalidOperationException()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var originalTask = TaskItem.Create(
            id: taskId,
            projectId: projectId,
            title: "Test Task",
            status: TaskItemStatus.ToDo,
            priority: TaskItemPriority.Normal,
            createdBy: "creator",
            createdAtUtc: DateTime.UtcNow);

        var request = new ReopenTaskViewModel
        {
            TaskId = taskId,
            ReopenToStatus = TaskItemStatusContract.Backlog,
            ConcurrencyToken = Convert.ToBase64String([1, 2, 3]),
        };

        var actor = ActorContext.ForUser("reopener-not-completed");
        var requestContext = RequestContext.FromPropagated(
            traceId: "trace-reopen-not-completed",
            correlationId: "corr-reopen-not-completed",
            causationId: "cause-reopen-not-completed",
            requestId: "req-reopen-not-completed",
            actor: actor);

        _mockTaskData.Setup(d => d.GetByIdAsync(taskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(originalTask);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _business.ReopenAsync(request, actor, requestContext, DateTime.UtcNow, CancellationToken.None));

        Assert.Contains("Only a Completed Task can be reopened", ex.Message);
    }

    [Fact]
    public async Task ReopenAsync_ReopenToCompleted_ThrowsInvalidOperationException()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var completedAtUtc = DateTime.UtcNow.AddHours(-1);
        var originalTask = TaskItem.Create(
            id: taskId,
            projectId: projectId,
            title: "Test Task",
            status: TaskItemStatus.Completed,
            priority: TaskItemPriority.Normal,
            createdBy: "creator",
            createdAtUtc: DateTime.UtcNow,
            completedAtUtc: completedAtUtc);

        var request = new ReopenTaskViewModel
        {
            TaskId = taskId,
            ReopenToStatus = TaskItemStatusContract.Completed,
            ConcurrencyToken = Convert.ToBase64String([1, 2, 3]),
        };

        var actor = ActorContext.ForUser("reopener-to-completed");
        var requestContext = RequestContext.FromPropagated(
            traceId: "trace-reopen-to-completed",
            correlationId: "corr-reopen-to-completed",
            causationId: "cause-reopen-to-completed",
            requestId: "req-reopen-to-completed",
            actor: actor);

        _mockTaskData.Setup(d => d.GetByIdAsync(taskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(originalTask);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _business.ReopenAsync(request, actor, requestContext, DateTime.UtcNow, CancellationToken.None));

        Assert.Contains("must transition to an open status", ex.Message);
    }

    [Fact]
    public async Task ReopenAsync_ReopenToCancelled_ThrowsInvalidOperationException()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var completedAtUtc = DateTime.UtcNow.AddHours(-1);
        var originalTask = TaskItem.Create(
            id: taskId,
            projectId: projectId,
            title: "Test Task",
            status: TaskItemStatus.Completed,
            priority: TaskItemPriority.Normal,
            createdBy: "creator",
            createdAtUtc: DateTime.UtcNow,
            completedAtUtc: completedAtUtc);

        var request = new ReopenTaskViewModel
        {
            TaskId = taskId,
            ReopenToStatus = TaskItemStatusContract.Cancelled,
            ConcurrencyToken = Convert.ToBase64String([1, 2, 3]),
        };

        var actor = ActorContext.ForUser("reopener-to-cancelled");
        var requestContext = RequestContext.FromPropagated(
            traceId: "trace-reopen-to-cancelled",
            correlationId: "corr-reopen-to-cancelled",
            causationId: "cause-reopen-to-cancelled",
            requestId: "req-reopen-to-cancelled",
            actor: actor);

        _mockTaskData.Setup(d => d.GetByIdAsync(taskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(originalTask);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _business.ReopenAsync(request, actor, requestContext, DateTime.UtcNow, CancellationToken.None));

        Assert.Contains("must transition to an open status", ex.Message);
    }

    #endregion

    #region EditAsync

    [Fact]
    public async Task EditAsync_UpdateTitle_ReturnsUpdatedTaskWithNewTitle()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var originalTask = TaskItem.Create(
            id: taskId,
            projectId: projectId,
            title: "Old Title",
            status: TaskItemStatus.ToDo,
            priority: TaskItemPriority.Normal,
            createdBy: "creator",
            createdAtUtc: DateTime.UtcNow);

        var request = new EditTaskViewModel
        {
            TaskId = taskId,
            Title = "New Title",
            ConcurrencyToken = Convert.ToBase64String([1, 2, 3]),
        };

        var actor = ActorContext.ForUser("editor-123");
        var requestContext = RequestContext.FromPropagated(
            traceId: "trace-edit-title",
            correlationId: "corr-edit-title",
            causationId: "cause-edit-title",
            requestId: "req-edit-title",
            actor: actor);

        _mockTaskData.Setup(d => d.GetByIdAsync(taskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(originalTask);

        // Act
        var result = await _business.EditAsync(
            request, actor, requestContext, DateTime.UtcNow, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(taskId, result.Id);
        Assert.Equal("New Title", result.Title);

        // Verify EditAsync was called with the mutated task and audit fact
        _mockTaskData.Verify(
            d => d.EditAsync(It.IsAny<TaskItem>(), It.IsAny<EntityMutationAudited>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task EditAsync_UpdateMultipleFields_ReturnsTaskWithAllChanges()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var originalTask = TaskItem.Create(
            id: taskId,
            projectId: projectId,
            title: "Original Title",
            status: TaskItemStatus.Backlog,
            priority: TaskItemPriority.Normal,
            createdBy: "creator",
            createdAtUtc: DateTime.UtcNow,
            description: "Old description",
            startDateUtc: DateTime.UtcNow.AddDays(1),
            dueDateUtc: DateTime.UtcNow.AddDays(7));

        var newDueDate = DateTime.UtcNow.AddDays(14);

        var request = new EditTaskViewModel
        {
            TaskId = taskId,
            Title = "Updated Title",
            Description = "New description",
            DueDateUtc = newDueDate,
            Notes = "Added notes",
            ConcurrencyToken = Convert.ToBase64String([1, 2, 3]),
        };

        var actor = ActorContext.ForUser("editor-456");
        var requestContext = RequestContext.FromPropagated(
            traceId: "trace-edit-multi",
            correlationId: "corr-edit-multi",
            causationId: "cause-edit-multi",
            requestId: "req-edit-multi",
            actor: actor);

        _mockTaskData.Setup(d => d.GetByIdAsync(taskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(originalTask);

        // Act
        var result = await _business.EditAsync(
            request, actor, requestContext, DateTime.UtcNow, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Updated Title", result.Title);
        Assert.Equal("New description", result.Description);
        Assert.Equal(newDueDate, result.DueDateUtc);
        Assert.Equal("Added notes", result.Notes);

        _mockTaskData.Verify(
            d => d.EditAsync(It.IsAny<TaskItem>(), It.IsAny<EntityMutationAudited>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task EditAsync_ClearDescription_ReturnsTaskWithNullDescription()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var originalTask = TaskItem.Create(
            id: taskId,
            projectId: projectId,
            title: "Test Task",
            status: TaskItemStatus.ToDo,
            priority: TaskItemPriority.Normal,
            createdBy: "creator",
            createdAtUtc: DateTime.UtcNow,
            description: "Old description");

        var request = new EditTaskViewModel
        {
            TaskId = taskId,
            Description = "", // Empty string to clear
            ConcurrencyToken = Convert.ToBase64String([1, 2, 3]),
        };

        var actor = ActorContext.ForUser("editor-789");
        var requestContext = RequestContext.FromPropagated(
            traceId: "trace-edit-clear-desc",
            correlationId: "corr-edit-clear-desc",
            causationId: "cause-edit-clear-desc",
            requestId: "req-edit-clear-desc",
            actor: actor);

        _mockTaskData.Setup(d => d.GetByIdAsync(taskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(originalTask);

        // Act
        var result = await _business.EditAsync(
            request, actor, requestContext, DateTime.UtcNow, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Null(result.Description);

        _mockTaskData.Verify(
            d => d.EditAsync(It.IsAny<TaskItem>(), It.IsAny<EntityMutationAudited>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task EditAsync_NoFieldsChanged_ThrowsInvalidOperationException()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var originalTask = TaskItem.Create(
            id: taskId,
            projectId: projectId,
            title: "Test Task",
            status: TaskItemStatus.ToDo,
            priority: TaskItemPriority.Normal,
            createdBy: "creator",
            createdAtUtc: DateTime.UtcNow,
            description: "Original description");

        var request = new EditTaskViewModel
        {
            TaskId = taskId,
            Title = "Test Task", // Same as original
            Description = "Original description", // Same as original
            ConcurrencyToken = Convert.ToBase64String([1, 2, 3]),
        };

        var actor = ActorContext.ForUser("editor-nochange");
        var requestContext = RequestContext.FromPropagated(
            traceId: "trace-edit-nochange",
            correlationId: "corr-edit-nochange",
            causationId: "cause-edit-nochange",
            requestId: "req-edit-nochange",
            actor: actor);

        _mockTaskData.Setup(d => d.GetByIdAsync(taskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(originalTask);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _business.EditAsync(request, actor, requestContext, DateTime.UtcNow, CancellationToken.None));

        Assert.Contains("No fields were changed", ex.Message);

        _mockTaskData.Verify(
            d => d.EditAsync(It.IsAny<TaskItem>(), It.IsAny<EntityMutationAudited>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task EditAsync_TaskNotFound_ThrowsArgumentException()
    {
        // Arrange
        var taskId = Guid.NewGuid();

        var request = new EditTaskViewModel
        {
            TaskId = taskId,
            Title = "New Title",
            ConcurrencyToken = Convert.ToBase64String([1, 2, 3]),
        };

        var actor = ActorContext.ForUser("editor-notfound");
        var requestContext = RequestContext.FromPropagated(
            traceId: "trace-edit-notfound",
            correlationId: "corr-edit-notfound",
            causationId: "cause-edit-notfound",
            requestId: "req-edit-notfound",
            actor: actor);

        _mockTaskData.Setup(d => d.GetByIdAsync(taskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TaskItem?)null);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _business.EditAsync(request, actor, requestContext, DateTime.UtcNow, CancellationToken.None));

        Assert.Contains("does not exist", ex.Message);

        _mockTaskData.Verify(
            d => d.EditAsync(It.IsAny<TaskItem>(), It.IsAny<EntityMutationAudited>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task EditAsync_CreatesAuditFactWithUpdatedAction()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var originalTask = TaskItem.Create(
            id: taskId,
            projectId: projectId,
            title: "Original Title",
            status: TaskItemStatus.ToDo,
            priority: TaskItemPriority.Normal,
            createdBy: "creator",
            createdAtUtc: DateTime.UtcNow);

        var request = new EditTaskViewModel
        {
            TaskId = taskId,
            Title = "New Title",
            ConcurrencyToken = Convert.ToBase64String([1, 2, 3]),
        };

        var actor = ActorContext.ForUser("editor-audit");
        var requestContext = RequestContext.FromPropagated(
            traceId: "trace-edit-audit",
            correlationId: "corr-edit-audit",
            causationId: "cause-edit-audit",
            requestId: "req-edit-audit",
            actor: actor);

        _mockTaskData.Setup(d => d.GetByIdAsync(taskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(originalTask);

        EntityMutationAudited? capturedAudit = null;
        _mockTaskData.Setup(d => d.EditAsync(It.IsAny<TaskItem>(), It.IsAny<EntityMutationAudited>(), It.IsAny<CancellationToken>()))
            .Callback<TaskItem, EntityMutationAudited, CancellationToken>((_, audit, _) => capturedAudit = audit)
            .Returns(Task.CompletedTask);

        // Act
        await _business.EditAsync(request, actor, requestContext, DateTime.UtcNow, CancellationToken.None);

        // Assert
        Assert.NotNull(capturedAudit);
        Assert.Equal(AuditActions.Updated, capturedAudit.Action);
        Assert.Contains(nameof(TaskItem.Title), capturedAudit.ChangedFields);
        Assert.Equal("Original Title", capturedAudit.PreviousValues?[nameof(TaskItem.Title)]);
        Assert.Equal("New Title", capturedAudit.NewValues?[nameof(TaskItem.Title)]);
    }

    #endregion
}

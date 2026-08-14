using Moq;
using ProjectChicago.Contracts.Audit;
using ProjectChicago.Crm.Contracts.Common;
using ProjectChicago.Crm.Contracts.Tasks;
using ProjectChicago.Crm.Core.Business;
using ProjectChicago.Crm.Core.Data;
using ProjectChicago.Crm.Core.Models.DataModels.Entities;
using ProjectChicago.Shared.Correlation;

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

        var actor = new ActorContext { ActorId = "actor-456", ActorType = ActorType.User };
        var requestContext = new RequestContext
        {
            TraceId = "trace-123",
            CorrelationId = "corr-123",
            CausationId = "cause-123",
            Actor = actor,
        };

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

        var actor = new ActorContext { ActorId = "actor-999", ActorType = ActorType.User };
        var requestContext = new RequestContext
        {
            TraceId = "trace-456",
            CorrelationId = "corr-456",
            CausationId = "cause-456",
            Actor = actor,
        };

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

        var actor = new ActorContext { ActorId = "reassigner", ActorType = ActorType.User };
        var requestContext = new RequestContext
        {
            TraceId = "trace-789",
            CorrelationId = "corr-789",
            CausationId = "cause-789",
            Actor = actor,
        };

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

        var actor = new ActorContext { ActorId = "reassigner", ActorType = ActorType.User };
        var requestContext = new RequestContext
        {
            TraceId = "trace-abc",
            CorrelationId = "corr-abc",
            CausationId = "cause-abc",
            Actor = actor,
        };

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

        var actor = new ActorContext { ActorId = "actor-456", ActorType = ActorType.User };
        var requestContext = new RequestContext
        {
            TraceId = "trace-123",
            CorrelationId = "corr-123",
            CausationId = "cause-123",
            Actor = actor,
        };

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

        var actor = new ActorContext { ActorId = "actor-999", ActorType = ActorType.User };
        var requestContext = new RequestContext
        {
            TraceId = "trace-xyz",
            CorrelationId = "corr-xyz",
            CausationId = "cause-xyz",
            Actor = actor,
        };

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

        var actor = new ActorContext { ActorId = "actor-111", ActorType = ActorType.User };
        var requestContext = new RequestContext
        {
            TraceId = "trace-111",
            CorrelationId = "corr-111",
            CausationId = "cause-111",
            Actor = actor,
        };

        _mockTaskData.Setup(d => d.GetByIdAsync(taskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(originalTask);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _business.AssignAsync(request, actor, requestContext, DateTime.UtcNow, CancellationToken.None));
    }

    #endregion
}

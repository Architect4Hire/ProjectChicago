using ProjectChicago.Crm.Contracts.Tasks;
using ProjectChicago.Crm.Core.Models.DataModels.Entities;
using ProjectChicago.Crm.Core.Repositories;
using TaskItemStatusContractTasks = ProjectChicago.Crm.Contracts.Tasks.TaskItemStatusContract;
using TaskItemPriorityContractTasks = ProjectChicago.Crm.Contracts.Tasks.TaskItemPriorityContract;

namespace ProjectChicago.Crm.Core.Business;

// Wire-contract <-> domain-model translation for Task use cases (TASK-001..022; onion-boundaries.md:
// "Business owns ... translation between Facade and Data models"). Lives in Business, as extension
// methods, alongside TaskBusiness - the only caller. TaskFacade never touches these; it passes wire
// contracts straight through and returns whatever ITaskBusiness methods hand back, so TasksController
// stays transport-only.
public static class TaskContractMappingExtensions
{
    public static TaskItemStatus ToCoreStatus(this TaskItemStatusContractTasks status) => status switch
    {
        TaskItemStatusContractTasks.Backlog => TaskItemStatus.Backlog,
        TaskItemStatusContractTasks.ToDo => TaskItemStatus.ToDo,
        TaskItemStatusContractTasks.InProgress => TaskItemStatus.InProgress,
        TaskItemStatusContractTasks.Blocked => TaskItemStatus.Blocked,
        TaskItemStatusContractTasks.Completed => TaskItemStatus.Completed,
        TaskItemStatusContractTasks.Cancelled => TaskItemStatus.Cancelled,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unmapped TaskItemStatusContract value."),
    };

    public static TaskItemPriority ToCorePriority(this TaskItemPriorityContractTasks priority) => priority switch
    {
        TaskItemPriorityContractTasks.Low => TaskItemPriority.Low,
        TaskItemPriorityContractTasks.Normal => TaskItemPriority.Normal,
        TaskItemPriorityContractTasks.High => TaskItemPriority.High,
        TaskItemPriorityContractTasks.Critical => TaskItemPriority.Critical,
        _ => throw new ArgumentOutOfRangeException(nameof(priority), priority, "Unmapped TaskItemPriorityContract value."),
    };

    private static TaskItemStatusContractTasks ToContractStatus(this TaskItemStatus status) => status switch
    {
        TaskItemStatus.Backlog => TaskItemStatusContractTasks.Backlog,
        TaskItemStatus.ToDo => TaskItemStatusContractTasks.ToDo,
        TaskItemStatus.InProgress => TaskItemStatusContractTasks.InProgress,
        TaskItemStatus.Blocked => TaskItemStatusContractTasks.Blocked,
        TaskItemStatus.Completed => TaskItemStatusContractTasks.Completed,
        TaskItemStatus.Cancelled => TaskItemStatusContractTasks.Cancelled,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unmapped TaskItemStatus value."),
    };

    private static TaskItemPriorityContractTasks ToContractPriority(this TaskItemPriority priority) => priority switch
    {
        TaskItemPriority.Low => TaskItemPriorityContractTasks.Low,
        TaskItemPriority.Normal => TaskItemPriorityContractTasks.Normal,
        TaskItemPriority.High => TaskItemPriorityContractTasks.High,
        TaskItemPriority.Critical => TaskItemPriorityContractTasks.Critical,
        _ => throw new ArgumentOutOfRangeException(nameof(priority), priority, "Unmapped TaskItemPriority value."),
    };

    // The single exit-point mapping TaskBusiness.CreateAsync calls once the Task is persisted -
    // builds the public TaskServiceModel straight from the domain aggregate, so no other layer
    // ever reads TaskItem fields directly.
    public static TaskServiceModel ToServiceModel(this TaskItem task) => new()
    {
        Id = task.Id,
        ProjectId = task.ProjectId,
        Title = task.Title,
        Description = task.Description,
        Status = task.Status.ToContractStatus(),
        Priority = task.Priority.ToContractPriority(),
        AssignedUserId = task.AssignedUserId,
        StartDateUtc = task.StartDateUtc,
        DueDateUtc = task.DueDateUtc,
        CompletedAtUtc = task.CompletedAtUtc,
        Notes = task.Notes,
        CreatedAtUtc = task.CreatedAtUtc,
        CreatedBy = task.CreatedBy,
        LastModifiedAtUtc = task.LastModifiedAtUtc,
        LastModifiedBy = task.LastModifiedBy,
        ConcurrencyToken = Convert.ToBase64String(task.RowVersion),
    };

    public static TaskListSortField ToCoreListSortField(this TaskSortField field) => field switch
    {
        TaskSortField.DueDateUtc => TaskListSortField.DueDateUtc,
        TaskSortField.Priority => TaskListSortField.Priority,
        TaskSortField.CreatedAtUtc => TaskListSortField.CreatedAtUtc,
        TaskSortField.LastModifiedAtUtc => TaskListSortField.LastModifiedAtUtc,
        _ => throw new ArgumentOutOfRangeException(nameof(field), field, "Unmapped TaskSortField value."),
    };

    public static TaskListSortDirection ToCoreListSortDirection(this TaskSortDirection direction) => direction switch
    {
        TaskSortDirection.Ascending => TaskListSortDirection.Ascending,
        TaskSortDirection.Descending => TaskListSortDirection.Descending,
        _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, "Unmapped TaskSortDirection value."),
    };
}

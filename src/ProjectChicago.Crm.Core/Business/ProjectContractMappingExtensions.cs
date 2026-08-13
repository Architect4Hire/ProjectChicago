using ProjectChicago.Crm.Contracts.Projects;
using ProjectChicago.Crm.Core.Models.DataModels.Entities;
using ProjectChicago.Crm.Core.Repositories;
using ClientContracts = ProjectChicago.Crm.Contracts.Clients;
using TaskItemStatusContract = ProjectChicago.Crm.Contracts.Clients.TaskItemStatusContract;
using TaskItemPriorityContract = ProjectChicago.Crm.Contracts.Clients.TaskItemPriorityContract;

namespace ProjectChicago.Crm.Core.Business;

// Wire-contract <-> domain-model translation for Project use cases (PROJECT-001..002, PROJECT-020..023;
// onion-boundaries.md: "Business owns ... translation between Facade and Data models"). Lives in
// Business, as extension methods, alongside ProjectBusiness - the only caller. ProjectFacade never
// touches these; it passes wire contracts straight through and returns whatever IProjectBusiness
// methods hand back, so ProjectsController stays transport-only.
public static class ProjectContractMappingExtensions
{
    public static ProjectStatus ToCoreStatus(this ProjectStatusContract status) => status switch
    {
        ProjectStatusContract.Planned => ProjectStatus.Planned,
        ProjectStatusContract.Active => ProjectStatus.Active,
        ProjectStatusContract.OnHold => ProjectStatus.OnHold,
        ProjectStatusContract.Completed => ProjectStatus.Completed,
        ProjectStatusContract.Cancelled => ProjectStatus.Cancelled,
        ProjectStatusContract.Archived => ProjectStatus.Archived,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unmapped ProjectStatusContract value."),
    };

    public static ProjectPriority ToCorePriority(this ProjectPriorityContract priority) => priority switch
    {
        ProjectPriorityContract.Low => ProjectPriority.Low,
        ProjectPriorityContract.Normal => ProjectPriority.Normal,
        ProjectPriorityContract.High => ProjectPriority.High,
        ProjectPriorityContract.Critical => ProjectPriority.Critical,
        _ => throw new ArgumentOutOfRangeException(nameof(priority), priority, "Unmapped ProjectPriorityContract value."),
    };

    public static ProjectListSortField ToCoreListSortField(this ProjectSortField sortField) => sortField switch
    {
        ProjectSortField.Name => ProjectListSortField.Name,
        ProjectSortField.CreatedAtUtc => ProjectListSortField.CreatedAtUtc,
        ProjectSortField.LastModifiedAtUtc => ProjectListSortField.LastModifiedAtUtc,
        ProjectSortField.Status => ProjectListSortField.Status,
        ProjectSortField.Priority => ProjectListSortField.Priority,
        ProjectSortField.TargetCompletionDateUtc => ProjectListSortField.TargetCompletionDateUtc,
        _ => throw new ArgumentOutOfRangeException(nameof(sortField), sortField, "Unmapped ProjectSortField value."),
    };

    public static ProjectListSortDirection ToCoreListSortDirection(this ProjectSortDirection sortDirection) => sortDirection switch
    {
        ProjectSortDirection.Ascending => ProjectListSortDirection.Ascending,
        ProjectSortDirection.Descending => ProjectListSortDirection.Descending,
        _ => throw new ArgumentOutOfRangeException(nameof(sortDirection), sortDirection, "Unmapped ProjectSortDirection value."),
    };

    private static ProjectStatusContract ToContractStatus(this ProjectStatus status) => status switch
    {
        ProjectStatus.Planned => ProjectStatusContract.Planned,
        ProjectStatus.Active => ProjectStatusContract.Active,
        ProjectStatus.OnHold => ProjectStatusContract.OnHold,
        ProjectStatus.Completed => ProjectStatusContract.Completed,
        ProjectStatus.Cancelled => ProjectStatusContract.Cancelled,
        ProjectStatus.Archived => ProjectStatusContract.Archived,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unmapped ProjectStatus value."),
    };

    private static ProjectPriorityContract ToContractPriority(this ProjectPriority priority) => priority switch
    {
        ProjectPriority.Low => ProjectPriorityContract.Low,
        ProjectPriority.Normal => ProjectPriorityContract.Normal,
        ProjectPriority.High => ProjectPriorityContract.High,
        ProjectPriority.Critical => ProjectPriorityContract.Critical,
        _ => throw new ArgumentOutOfRangeException(nameof(priority), priority, "Unmapped ProjectPriority value."),
    };

    // The single exit-point mapping ProjectBusiness.CreateAsync calls once the Project is persisted -
    // builds the public ProjectServiceModel straight from the domain aggregate, so no other layer
    // ever reads Project fields directly.
    public static ProjectServiceModel ToServiceModel(this Project project) => new()
    {
        Id = project.Id,
        ClientId = project.ClientId,
        Name = project.Name,
        Description = project.Description,
        Status = project.Status.ToContractStatus(),
        Priority = project.Priority.ToContractPriority(),
        OwnerUserId = project.OwnerUserId,
        StartDateUtc = project.StartDateUtc,
        TargetCompletionDateUtc = project.TargetCompletionDateUtc,
        ActualCompletionDateUtc = project.ActualCompletionDateUtc,
        Notes = project.Notes,
        CreatedAtUtc = project.CreatedAtUtc,
        CreatedBy = project.CreatedBy,
        LastModifiedAtUtc = project.LastModifiedAtUtc,
        LastModifiedBy = project.LastModifiedBy,
        ConcurrencyToken = Convert.ToBase64String(project.RowVersion),
    };

    // The exit-point mapping ProjectBusiness.GetDetailAsync calls once the repository read is
    // complete - builds ProjectDetailServiceModel from ProjectDetailResult (PROJECT-030), so no
    // other layer ever reads Project/Client/TaskItem fields directly for this use case.
    public static ProjectDetailServiceModel ToDetailServiceModel(this ProjectDetailResult detail) => new()
    {
        Project = detail.Project.ToServiceModel(),
        Client = detail.Client.ToClientSummary(),
        OpenTasks = detail.OpenTasks.Select(ToTaskSummary).ToList(),
        CompletedTasks = detail.CompletedTasks.Select(ToTaskSummary).ToList(),
        RecentActivityCount = detail.RecentActivityCount,
    };

    private static ClientContracts.ClientSummary ToClientSummary(this Client client) => new()
    {
        Id = client.Id,
        Name = client.Name,
        LifecycleStatus = client.LifecycleStatus.ToContractLifecycleStatus(),
        OwnerUserId = client.OwnerUserId,
        PrimaryContactName = client.PrimaryContactName,
        PrimaryEmail = client.PrimaryEmail,
    };

    private static ClientContracts.ClientLifecycleStatusContract ToContractLifecycleStatus(this ClientLifecycleStatus status) => status switch
    {
        ClientLifecycleStatus.Lead => ClientContracts.ClientLifecycleStatusContract.Lead,
        ClientLifecycleStatus.Prospect => ClientContracts.ClientLifecycleStatusContract.Prospect,
        ClientLifecycleStatus.Active => ClientContracts.ClientLifecycleStatusContract.Active,
        ClientLifecycleStatus.OnHold => ClientContracts.ClientLifecycleStatusContract.OnHold,
        ClientLifecycleStatus.Inactive => ClientContracts.ClientLifecycleStatusContract.Inactive,
        ClientLifecycleStatus.Archived => ClientContracts.ClientLifecycleStatusContract.Archived,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unmapped ClientLifecycleStatus value."),
    };

    private static ProjectTaskSummary ToTaskSummary(TaskItem task) => new()
    {
        Id = task.Id,
        Title = task.Title,
        Status = task.Status.ToContractStatus(),
        Priority = task.Priority.ToContractPriority(),
        AssignedUserId = task.AssignedUserId,
        DueDateUtc = task.DueDateUtc,
        CompletedAtUtc = task.CompletedAtUtc,
    };

    private static TaskItemStatusContract ToContractStatus(this TaskItemStatus status) => status switch
    {
        TaskItemStatus.Backlog => TaskItemStatusContract.Backlog,
        TaskItemStatus.ToDo => TaskItemStatusContract.ToDo,
        TaskItemStatus.InProgress => TaskItemStatusContract.InProgress,
        TaskItemStatus.Blocked => TaskItemStatusContract.Blocked,
        TaskItemStatus.Completed => TaskItemStatusContract.Completed,
        TaskItemStatus.Cancelled => TaskItemStatusContract.Cancelled,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unmapped TaskItemStatus value."),
    };

    private static TaskItemPriorityContract ToContractPriority(this TaskItemPriority priority) => priority switch
    {
        TaskItemPriority.Low => TaskItemPriorityContract.Low,
        TaskItemPriority.Normal => TaskItemPriorityContract.Normal,
        TaskItemPriority.High => TaskItemPriorityContract.High,
        TaskItemPriority.Critical => TaskItemPriorityContract.Critical,
        _ => throw new ArgumentOutOfRangeException(nameof(priority), priority, "Unmapped TaskItemPriority value."),
    };
}

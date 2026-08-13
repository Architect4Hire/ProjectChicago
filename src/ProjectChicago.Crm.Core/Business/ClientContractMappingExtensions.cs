using ProjectChicago.Crm.Contracts.Clients;
using ProjectChicago.Crm.Core.Models.DataModels.Entities;
using ProjectChicago.Crm.Core.Models.ServiceModels;
using ProjectChicago.Crm.Core.Repositories;
using CoreDuplicateMatchField = ProjectChicago.Crm.Core.Models.ServiceModels.ClientDuplicateMatchField;
using ContractDuplicateMatchField = ProjectChicago.Crm.Contracts.Clients.ClientDuplicateMatchField;

namespace ProjectChicago.Crm.Core.Business;

// Wire-contract <-> domain-model translation for the Client creation use case (CLIENT-001..004;
// onion-boundaries.md: "Business owns ... translation between Facade and Data models"). Lives in
// Business, as extension methods, alongside ClientBusiness - the only caller. ClientFacade never
// touches these; it passes CreateClientViewModel straight through and returns whatever
// IClientBusiness.CreateAsync hands back, so ClientsController stays transport-only too.
public static class ClientContractMappingExtensions
{
    public static ClientLifecycleStatus ToCoreLifecycleStatus(this ClientLifecycleStatusContract status) => status switch
    {
        ClientLifecycleStatusContract.Lead => ClientLifecycleStatus.Lead,
        ClientLifecycleStatusContract.Prospect => ClientLifecycleStatus.Prospect,
        ClientLifecycleStatusContract.Active => ClientLifecycleStatus.Active,
        ClientLifecycleStatusContract.OnHold => ClientLifecycleStatus.OnHold,
        ClientLifecycleStatusContract.Inactive => ClientLifecycleStatus.Inactive,
        ClientLifecycleStatusContract.Archived => ClientLifecycleStatus.Archived,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unmapped ClientLifecycleStatusContract value."),
    };

    private static ClientLifecycleStatusContract ToContractLifecycleStatus(this ClientLifecycleStatus status) => status switch
    {
        ClientLifecycleStatus.Lead => ClientLifecycleStatusContract.Lead,
        ClientLifecycleStatus.Prospect => ClientLifecycleStatusContract.Prospect,
        ClientLifecycleStatus.Active => ClientLifecycleStatusContract.Active,
        ClientLifecycleStatus.OnHold => ClientLifecycleStatusContract.OnHold,
        ClientLifecycleStatus.Inactive => ClientLifecycleStatusContract.Inactive,
        ClientLifecycleStatus.Archived => ClientLifecycleStatusContract.Archived,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unmapped ClientLifecycleStatus value."),
    };

    // The single exit-point mapping ClientBusiness.CreateAsync calls once the Client is persisted -
    // builds the public ClientServiceModel straight from the domain aggregate plus the CLIENT-004
    // duplicate candidates, so no other layer ever reads Client fields directly.
    public static ClientServiceModel ToServiceModel(this Client client, IReadOnlyList<ClientDuplicateCandidate> possibleDuplicates) => new()
    {
        Id = client.Id,
        Name = client.Name,
        PrimaryContactName = client.PrimaryContactName,
        PrimaryEmail = client.PrimaryEmail,
        PrimaryPhone = client.PrimaryPhone,
        Website = client.Website,
        AddressLine = client.AddressLine,
        City = client.City,
        StateOrProvince = client.StateOrProvince,
        PostalCode = client.PostalCode,
        Country = client.Country,
        LifecycleStatus = client.LifecycleStatus.ToContractLifecycleStatus(),
        Description = client.Description,
        OwnerUserId = client.OwnerUserId,
        CreatedAtUtc = client.CreatedAtUtc,
        CreatedBy = client.CreatedBy,
        LastModifiedAtUtc = client.LastModifiedAtUtc,
        LastModifiedBy = client.LastModifiedBy,
        ConcurrencyToken = Convert.ToBase64String(client.RowVersion),
        PossibleDuplicates = possibleDuplicates.Select(ToDuplicateWarning).ToList(),
    };

    private static ClientDuplicateWarning ToDuplicateWarning(this ClientDuplicateCandidate candidate) => new()
    {
        ClientId = candidate.ClientId,
        Name = candidate.Name,
        MatchedOn = candidate.MatchedOn.Select(ToContractMatchField).ToList(),
    };

    private static ContractDuplicateMatchField ToContractMatchField(CoreDuplicateMatchField matchField) => matchField switch
    {
        CoreDuplicateMatchField.Name => ContractDuplicateMatchField.Name,
        CoreDuplicateMatchField.PrimaryEmail => ContractDuplicateMatchField.PrimaryEmail,
        CoreDuplicateMatchField.PrimaryPhone => ContractDuplicateMatchField.PrimaryPhone,
        _ => throw new ArgumentOutOfRangeException(nameof(matchField), matchField, "Unmapped ClientDuplicateMatchField value."),
    };

    // Wire-contract <-> repository-level sort mirrors for the Client list use case (CLIENT-023).
    // ClientListSortField/ClientListSortDirection exist so IClientRepository never depends on the
    // public wire contract (data.md; onion-boundaries.md) - Business is the only place that
    // crosses between the two.
    public static ClientListSortField ToCoreListSortField(this ClientSortField sortField) => sortField switch
    {
        ClientSortField.Name => ClientListSortField.Name,
        ClientSortField.CreatedAtUtc => ClientListSortField.CreatedAtUtc,
        ClientSortField.LastModifiedAtUtc => ClientListSortField.LastModifiedAtUtc,
        ClientSortField.LifecycleStatus => ClientListSortField.LifecycleStatus,
        _ => throw new ArgumentOutOfRangeException(nameof(sortField), sortField, "Unmapped ClientSortField value."),
    };

    public static ClientListSortDirection ToCoreListSortDirection(this ClientSortDirection sortDirection) => sortDirection switch
    {
        ClientSortDirection.Ascending => ClientListSortDirection.Ascending,
        ClientSortDirection.Descending => ClientListSortDirection.Descending,
        _ => throw new ArgumentOutOfRangeException(nameof(sortDirection), sortDirection, "Unmapped ClientSortDirection value."),
    };

    // The single exit-point mapping ClientBusiness.GetDetailAsync calls once the repository read
    // is complete - builds the public ClientDetailServiceModel straight from
    // ClientDetailQueryResult (CLIENT-030..032), so no other layer ever reads Client/Project/
    // TaskItem fields directly for this use case.
    public static ClientDetailServiceModel ToClientDetailServiceModel(this ClientDetailQueryResult detail) => new()
    {
        Client = detail.Client.ToServiceModel([]),
        ActiveProjects = detail.ActiveProjects.Select(ToProjectSummary).ToList(),
        HistoricalProjects = detail.HistoricalProjects.Select(ToProjectSummary).ToList(),
        OpenTasks = detail.OpenTasks.Select(ToTaskSummary).ToList(),
        RecentlyCompletedTasks = detail.RecentlyCompletedTasks.Select(ToTaskSummary).ToList(),
    };

    private static ClientProjectSummary ToProjectSummary(Project project) => new()
    {
        Id = project.Id,
        Name = project.Name,
        Status = project.Status.ToContractStatus(),
        Priority = project.Priority.ToContractPriority(),
        OwnerUserId = project.OwnerUserId,
        StartDateUtc = project.StartDateUtc,
        TargetCompletionDateUtc = project.TargetCompletionDateUtc,
        ActualCompletionDateUtc = project.ActualCompletionDateUtc,
        LastModifiedAtUtc = project.LastModifiedAtUtc,
    };

    private static ClientTaskSummary ToTaskSummary(TaskItem task) => new()
    {
        Id = task.Id,
        ProjectId = task.ProjectId,
        Title = task.Title,
        Status = task.Status.ToContractStatus(),
        Priority = task.Priority.ToContractPriority(),
        AssignedUserId = task.AssignedUserId,
        DueDateUtc = task.DueDateUtc,
        CompletedAtUtc = task.CompletedAtUtc,
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

using ProjectChicago.Contracts.Audit;
using ProjectChicago.Crm.Contracts.Common;
using ProjectChicago.Crm.Contracts.Projects;
using ProjectChicago.Crm.Core.Business;
using ProjectChicago.Crm.Core.Data;
using ProjectChicago.Crm.Core.Models.DataModels.Entities;
using ProjectChicago.Crm.Core.Repositories;
using ProjectChicago.Shared.Correlation;
using Xunit;

namespace ProjectChicago.Crm.Core.Tests.Business;

// Pure unit tests for ProjectBusiness (PROJECT-001..002, AUDIT-001..003; backend.md Tests: "Unit-
// test Facade/Business/Data behavior at the layer that owns the rule"). IProjectData is faked
// rather than backed by SQL Server - proving Business's own rules/translation does not require a
// database, matching the RESTRICTION that Business itself never touches EF. CreateAsync takes the
// wire CreateProjectViewModel and returns the wire ProjectServiceModel directly (Business owns that
// mapping - ProjectContractMappingExtensions), so these tests assert against ProjectServiceModel's
// fields rather than an internal Project-entity wrapper.
public class ProjectBusinessTests
{
    private sealed class FakeProjectData : IProjectData
    {
        public Project? CreatedProject { get; private set; }

        public EntityMutationAudited? CreatedAuditFact { get; private set; }

        public Task CreateAsync(Project project, EntityMutationAudited auditFact, CancellationToken cancellationToken)
        {
            CreatedProject = project;
            CreatedAuditFact = auditFact;
            return Task.CompletedTask;
        }

        public Task<ProjectListResult> ListAsync(ProjectListFilter filter, CancellationToken cancellationToken) =>
            Task.FromResult(new ProjectListResult { Items = [], TotalCount = 0 });

        public Task<ProjectDetailResult?> GetDetailAsync(Guid projectId, CancellationToken cancellationToken) =>
            Task.FromResult<ProjectDetailResult?>(null);

        public Task<Project?> GetAsync(Guid projectId, CancellationToken cancellationToken) =>
            Task.FromResult<Project?>(null);

        public Task TransitionStatusAsync(
            Project project,
            ProjectStatus newStatus,
            string modifiedBy,
            DateTime modifiedAtUtc,
            DateTime? completionTimestampUtc,
            string expectedConcurrencyToken,
            EntityMutationAudited auditFact,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task ArchiveAsync(
            Project project,
            string modifiedBy,
            DateTime modifiedAtUtc,
            string expectedConcurrencyToken,
            EntityMutationAudited auditFact,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private static readonly DateTime CreatedAtUtc = new(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);

    private static CreateProjectViewModel CreateViewModel(
        Guid? clientId = null,
        string name = "Website Redesign",
        ProjectStatusContract? status = null,
        ProjectPriorityContract? priority = null,
        string ownerUserId = "owner-1") => new()
    {
        ClientId = clientId ?? Guid.NewGuid(),
        Name = name,
        Status = status,
        Priority = priority,
        OwnerUserId = ownerUserId,
    };

    private static Task<ProjectServiceModel> CreateAsync(
        ProjectBusiness business,
        CreateProjectViewModel request,
        ActorContext? actor = null,
        RequestContext? requestContext = null) =>
        business.CreateAsync(
            request,
            actor ?? ActorContext.ForUser("user-1"),
            requestContext ?? RequestContext.CreateNew(),
            CreatedAtUtc,
            CancellationToken.None);

    // --- Initial state (PROJECT-010) ---

    [Fact]
    public async Task CreateAsync_WithNoStatusSupplied_DefaultsToPlanned()
    {
        var business = new ProjectBusiness(new FakeProjectData());

        var result = await CreateAsync(business, CreateViewModel());

        Assert.Equal(ProjectStatusContract.Planned, result.Status);
    }

    [Fact]
    public async Task CreateAsync_WithAnExplicitStatus_UsesIt()
    {
        var business = new ProjectBusiness(new FakeProjectData());

        var result = await CreateAsync(business, CreateViewModel(status: ProjectStatusContract.Active));

        Assert.Equal(ProjectStatusContract.Active, result.Status);
    }

    [Fact]
    public async Task CreateAsync_WithNoPrioritySupplied_DefaultsToNormal()
    {
        var business = new ProjectBusiness(new FakeProjectData());

        var result = await CreateAsync(business, CreateViewModel());

        Assert.Equal(ProjectPriorityContract.Normal, result.Priority);
    }

    [Fact]
    public async Task CreateAsync_WithAnExplicitPriority_UsesIt()
    {
        var business = new ProjectBusiness(new FakeProjectData());

        var result = await CreateAsync(business, CreateViewModel(priority: ProjectPriorityContract.High));

        Assert.Equal(ProjectPriorityContract.High, result.Priority);
    }

    [Fact]
    public async Task CreateAsync_WithAnUndefinedStatus_Throws()
    {
        var business = new ProjectBusiness(new FakeProjectData());
        var request = CreateViewModel() with { Status = (ProjectStatusContract)999 };

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => CreateAsync(business, request));
    }

    [Fact]
    public async Task CreateAsync_WithAnUndefinedPriority_Throws()
    {
        var business = new ProjectBusiness(new FakeProjectData());
        var request = CreateViewModel() with { Priority = (ProjectPriorityContract)999 };

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => CreateAsync(business, request));
    }

    // --- Model translation ---

    [Fact]
    public async Task CreateAsync_TrimsNameAndOwnerUserId()
    {
        var business = new ProjectBusiness(new FakeProjectData());

        var result = await CreateAsync(
            business, CreateViewModel(name: "  Website Redesign  ", ownerUserId: "  owner-1  "));

        Assert.Equal("Website Redesign", result.Name);
        Assert.Equal("owner-1", result.OwnerUserId);
    }

    [Fact]
    public async Task CreateAsync_ConvertsBlankOptionalFieldsToNull()
    {
        var business = new ProjectBusiness(new FakeProjectData());
        var request = CreateViewModel() with { Description = "   ", Notes = "" };

        var result = await CreateAsync(business, request);

        Assert.Null(result.Description);
        Assert.Null(result.Notes);
    }

    [Fact]
    public async Task CreateAsync_AssignsAFreshApplicationGeneratedId()
    {
        var business = new ProjectBusiness(new FakeProjectData());

        var result = await CreateAsync(business, CreateViewModel());

        Assert.NotEqual(Guid.Empty, result.Id);
    }

    [Fact]
    public async Task CreateAsync_PreservesTheClientId()
    {
        var business = new ProjectBusiness(new FakeProjectData());
        var clientId = Guid.NewGuid();

        var result = await CreateAsync(business, CreateViewModel(clientId: clientId));

        Assert.Equal(clientId, result.ClientId);
    }

    [Fact]
    public async Task CreateAsync_UsesTheActorIdAsCreatedByAndLastModifiedBy()
    {
        var business = new ProjectBusiness(new FakeProjectData());

        var result = await CreateAsync(business, CreateViewModel(), actor: ActorContext.ForUser("actor-42"));

        Assert.Equal("actor-42", result.CreatedBy);
        Assert.Equal("actor-42", result.LastModifiedBy);
    }

    [Fact]
    public async Task CreateAsync_RecordsTheSuppliedTimestamps()
    {
        var business = new ProjectBusiness(new FakeProjectData());

        var result = await CreateAsync(business, CreateViewModel());

        Assert.Equal(CreatedAtUtc, result.CreatedAtUtc);
        Assert.Equal(CreatedAtUtc, result.LastModifiedAtUtc);
    }

    // --- Audit (AUDIT-001..003) ---

    [Fact]
    public async Task CreateAsync_GeneratesAnAuditFactWithCreatedAction()
    {
        var data = new FakeProjectData();
        var business = new ProjectBusiness(data);

        await CreateAsync(business, CreateViewModel());

        Assert.NotNull(data.CreatedAuditFact);
        Assert.Equal(AuditActions.Created, data.CreatedAuditFact.Action);
    }

    [Fact]
    public async Task CreateAsync_AuditFactIdentifiesTheProject()
    {
        var data = new FakeProjectData();
        var business = new ProjectBusiness(data);

        await CreateAsync(business, CreateViewModel());

        Assert.NotNull(data.CreatedAuditFact);
        Assert.Equal(AuditEntityTypes.Project, data.CreatedAuditFact.EntityType);
        Assert.Equal(data.CreatedProject!.Id, data.CreatedAuditFact.EntityId);
    }

    [Fact]
    public async Task CreateAsync_AuditFactIncludesCreatedFieldsList()
    {
        var data = new FakeProjectData();
        var business = new ProjectBusiness(data);

        await CreateAsync(business, CreateViewModel());

        Assert.NotNull(data.CreatedAuditFact);
        Assert.Contains(nameof(Project.Name), data.CreatedAuditFact.ChangedFields);
        Assert.Contains(nameof(Project.Status), data.CreatedAuditFact.ChangedFields);
        Assert.Contains(nameof(Project.Priority), data.CreatedAuditFact.ChangedFields);
        Assert.Contains(nameof(Project.OwnerUserId), data.CreatedAuditFact.ChangedFields);
        Assert.Contains(nameof(Project.ClientId), data.CreatedAuditFact.ChangedFields);
    }

    [Fact]
    public async Task CreateAsync_AuditFactPreservesRequestContext()
    {
        var data = new FakeProjectData();
        var business = new ProjectBusiness(data);
        var requestContext = RequestContext.CreateNew();

        await CreateAsync(business, CreateViewModel(), requestContext: requestContext);

        Assert.NotNull(data.CreatedAuditFact);
        Assert.Equal(requestContext.TraceId, data.CreatedAuditFact.TraceId);
        Assert.Equal(requestContext.CorrelationId, data.CreatedAuditFact.CorrelationId);
        Assert.Equal(requestContext.CausationId, data.CreatedAuditFact.CausationId);
    }

    [Fact]
    public async Task CreateAsync_PersistsViaIProjectData()
    {
        var data = new FakeProjectData();
        var business = new ProjectBusiness(data);
        var request = CreateViewModel();

        await CreateAsync(business, request);

        Assert.NotNull(data.CreatedProject);
        Assert.Equal(request.ClientId, data.CreatedProject.ClientId);
        Assert.Equal(request.Name, data.CreatedProject.Name);
    }

    [Fact]
    public async Task CreateAsync_RequiresAnIdentifiedActorForCreatedBy()
    {
        var business = new ProjectBusiness(new FakeProjectData());

        await Assert.ThrowsAsync<ArgumentException>(
            () => CreateAsync(business, CreateViewModel(), actor: ActorContext.ForAnonymous()));
    }

    // --- Archive (PROJECT-014, DATA-020) ---

    [Fact]
    public async Task ArchiveAsync_WithValidCompletedProject_ReturnsArchivedProject()
    {
        // PROJECT-014: Projects can be archived from Completed status
        var data = new FakeProjectData();
        var business = new ProjectBusiness(data);
        var clientId = Guid.NewGuid();

        var project = Project.Create(
            id: Guid.NewGuid(),
            clientId: clientId,
            name: "Archived Project",
            status: ProjectStatus.Completed,
            priority: ProjectPriority.Normal,
            ownerUserId: "owner-1",
            createdBy: "user-1",
            createdAtUtc: CreatedAtUtc,
            actualCompletionDateUtc: CreatedAtUtc.AddDays(30));

        var fakeData = new FakeProjectDataWithProject(project);
        var businessWithProject = new ProjectBusiness(fakeData);
        var archivedAtUtc = CreatedAtUtc.AddDays(31);

        // Act
        var result = await businessWithProject.ArchiveAsync(
            project.Id,
            Convert.ToBase64String(project.RowVersion),
            ActorContext.ForUser("user-1"),
            RequestContext.CreateNew(),
            archivedAtUtc,
            CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(ProjectStatusContract.Archived, result.Status);
    }

    [Fact]
    public async Task ArchiveAsync_WithValidCancelledProject_ReturnsArchivedProject()
    {
        // PROJECT-014: Projects can be archived from Cancelled status
        var clientId = Guid.NewGuid();

        var project = Project.Create(
            id: Guid.NewGuid(),
            clientId: clientId,
            name: "Cancelled Project",
            status: ProjectStatus.Cancelled,
            priority: ProjectPriority.Normal,
            ownerUserId: "owner-1",
            createdBy: "user-1",
            createdAtUtc: CreatedAtUtc);

        var fakeData = new FakeProjectDataWithProject(project);
        var business = new ProjectBusiness(fakeData);
        var archivedAtUtc = CreatedAtUtc.AddDays(1);

        // Act
        var result = await business.ArchiveAsync(
            project.Id,
            Convert.ToBase64String(project.RowVersion),
            ActorContext.ForUser("user-1"),
            RequestContext.CreateNew(),
            archivedAtUtc,
            CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(ProjectStatusContract.Archived, result.Status);
    }

    [Fact]
    public async Task ArchiveAsync_WithNonexistentProject_ReturnsNull()
    {
        var business = new ProjectBusiness(new FakeProjectData());

        var result = await business.ArchiveAsync(
            Guid.NewGuid(),
            Convert.ToBase64String(new byte[8]),
            ActorContext.ForUser("user-1"),
            RequestContext.CreateNew(),
            CreatedAtUtc.AddDays(1),
            CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task ArchiveAsync_RequiresProjectId()
    {
        var business = new ProjectBusiness(new FakeProjectData());

        await Assert.ThrowsAsync<ArgumentException>(
            () => business.ArchiveAsync(
                Guid.Empty,
                Convert.ToBase64String(new byte[8]),
                ActorContext.ForUser("user-1"),
                RequestContext.CreateNew(),
                CreatedAtUtc.AddDays(1),
                CancellationToken.None));
    }

    // Helper for tests that need to retrieve a project
    private sealed class FakeProjectDataWithProject : IProjectData
    {
        private readonly Project _project;

        public FakeProjectDataWithProject(Project project)
        {
            _project = project;
        }

        public Task CreateAsync(Project project, EntityMutationAudited auditFact, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<ProjectListResult> ListAsync(ProjectListFilter filter, CancellationToken cancellationToken) =>
            Task.FromResult(new ProjectListResult { Items = [], TotalCount = 0 });

        public Task<ProjectDetailResult?> GetDetailAsync(Guid projectId, CancellationToken cancellationToken) =>
            Task.FromResult<ProjectDetailResult?>(null);

        public Task<Project?> GetAsync(Guid projectId, CancellationToken cancellationToken) =>
            Task.FromResult(_project.Id == projectId ? _project : null);

        public Task TransitionStatusAsync(
            Project project,
            ProjectStatus newStatus,
            string modifiedBy,
            DateTime modifiedAtUtc,
            DateTime? completionTimestampUtc,
            string expectedConcurrencyToken,
            EntityMutationAudited auditFact,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task ArchiveAsync(
            Project project,
            string modifiedBy,
            DateTime modifiedAtUtc,
            string expectedConcurrencyToken,
            EntityMutationAudited auditFact,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}

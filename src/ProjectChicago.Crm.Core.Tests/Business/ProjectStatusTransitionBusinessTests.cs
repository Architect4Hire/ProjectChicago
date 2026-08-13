using ProjectChicago.Contracts.Audit;
using ProjectChicago.Crm.Contracts.Projects;
using ProjectChicago.Crm.Core.Business;
using ProjectChicago.Crm.Core.Data;
using ProjectChicago.Crm.Core.Models.DataModels.Entities;
using ProjectChicago.Crm.Core.Repositories;
using ProjectChicago.Shared.Correlation;
using Xunit;

namespace ProjectChicago.Crm.Core.Tests.Business;

// Business layer tests for Project status transitions (PROJECT-010..014, AUDIT-001..008;
// backend.md Tests: "Unit-test Facade/Business/Data behavior at the layer that owns the rule").
// IProjectData is faked - Business layer validates transition rules and builds audit facts;
// Data layer persistence is tested separately.
public class ProjectStatusTransitionBusinessTests
{
    private sealed class FakeProjectData : IProjectData
    {
        public Project? FetchedProject { get; set; }

        public Project? TransitionedProject { get; private set; }

        public EntityMutationAudited? TransitionAuditFact { get; private set; }

        public Project? ArchivedProject { get; private set; }

        public EntityMutationAudited? ArchiveAuditFact { get; private set; }

        public Task CreateAsync(Project project, EntityMutationAudited auditFact, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<ProjectListResult> ListAsync(ProjectListFilter filter, CancellationToken cancellationToken) =>
            Task.FromResult(new ProjectListResult { Items = [], TotalCount = 0 });

        public Task<ProjectDetailResult?> GetDetailAsync(Guid projectId, CancellationToken cancellationToken) =>
            Task.FromResult<ProjectDetailResult?>(null);

        public Task<Project?> GetAsync(Guid projectId, CancellationToken cancellationToken)
        {
            return Task.FromResult(FetchedProject);
        }

        public Task TransitionStatusAsync(
            Project project,
            ProjectStatus newStatus,
            string modifiedBy,
            DateTime modifiedAtUtc,
            DateTime? completionTimestampUtc,
            string expectedConcurrencyToken,
            EntityMutationAudited auditFact,
            CancellationToken cancellationToken)
        {
            TransitionedProject = project;
            TransitionAuditFact = auditFact;
            return Task.CompletedTask;
        }

        public Task ArchiveAsync(
            Project project,
            string modifiedBy,
            DateTime modifiedAtUtc,
            string expectedConcurrencyToken,
            EntityMutationAudited auditFact,
            CancellationToken cancellationToken)
        {
            ArchivedProject = project;
            ArchiveAuditFact = auditFact;
            return Task.CompletedTask;
        }
    }

    private static readonly DateTime Now = new(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task TransitionStatusAsync_FromPlanedToActive_Succeeds()
    {
        var fakeData = new FakeProjectData
        {
            FetchedProject = CreateProject(status: ProjectStatus.Planned),
        };
        var business = new ProjectBusiness(fakeData);
        var concurrencyToken = Convert.ToBase64String(new byte[] { 1, 2, 3 });

        var result = await business.TransitionStatusAsync(
            projectId: Guid.NewGuid(),
            targetStatus: ProjectStatusContract.Active,
            expectedConcurrencyToken: concurrencyToken,
            actor: ActorContext.ForUser("user-1"),
            requestContext: RequestContext.CreateNew(),
            transitionedAtUtc: Now);

        Assert.NotNull(result);
        Assert.Equal(ProjectStatusContract.Active, result.Status);
        Assert.NotNull(fakeData.TransitionAuditFact);
        Assert.Equal(AuditActions.StatusChanged, fakeData.TransitionAuditFact.Action);
    }

    [Fact]
    public async Task TransitionStatusAsync_ToCompleted_WithoutAcknowledgement_Throws()
    {
        var fakeData = new FakeProjectData
        {
            FetchedProject = CreateProject(status: ProjectStatus.Active),
        };
        var business = new ProjectBusiness(fakeData);
        var concurrencyToken = Convert.ToBase64String(new byte[] { 1, 2, 3 });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            business.TransitionStatusAsync(
                projectId: Guid.NewGuid(),
                targetStatus: ProjectStatusContract.Completed,
                expectedConcurrencyToken: concurrencyToken,
                actor: ActorContext.ForUser("user-1"),
                requestContext: RequestContext.CreateNew(),
                transitionedAtUtc: Now,
                acknowledgeOpenTasks: false));

        Assert.Contains("acknowledgement", ex.Message);
    }

    [Fact]
    public async Task TransitionStatusAsync_ToCompleted_WithAcknowledgement_Succeeds()
    {
        var fakeData = new FakeProjectData
        {
            FetchedProject = CreateProject(status: ProjectStatus.Active),
        };
        var business = new ProjectBusiness(fakeData);
        var concurrencyToken = Convert.ToBase64String(new byte[] { 1, 2, 3 });

        var result = await business.TransitionStatusAsync(
            projectId: Guid.NewGuid(),
            targetStatus: ProjectStatusContract.Completed,
            expectedConcurrencyToken: concurrencyToken,
            actor: ActorContext.ForUser("user-1"),
            requestContext: RequestContext.CreateNew(),
            transitionedAtUtc: Now,
            acknowledgeOpenTasks: true);

        Assert.NotNull(result);
        Assert.Equal(ProjectStatusContract.Completed, result.Status);
        Assert.NotNull(result.ActualCompletionDateUtc);
        Assert.Equal(Now, result.ActualCompletionDateUtc);
    }

    [Fact]
    public async Task TransitionStatusAsync_WhenProjectNotFound_ReturnsNull()
    {
        var fakeData = new FakeProjectData { FetchedProject = null };
        var business = new ProjectBusiness(fakeData);
        var concurrencyToken = Convert.ToBase64String(new byte[] { 1, 2, 3 });

        var result = await business.TransitionStatusAsync(
            projectId: Guid.NewGuid(),
            targetStatus: ProjectStatusContract.Active,
            expectedConcurrencyToken: concurrencyToken,
            actor: ActorContext.ForUser("user-1"),
            requestContext: RequestContext.CreateNew(),
            transitionedAtUtc: Now);

        Assert.Null(result);
    }

    [Fact]
    public async Task ArchiveAsync_FromCompleted_Succeeds()
    {
        var completionTime = new DateTime(2026, 1, 10, 12, 0, 0, DateTimeKind.Utc);
        var fakeData = new FakeProjectData
        {
            FetchedProject = CreateProject(status: ProjectStatus.Completed, actualCompletionDateUtc: completionTime),
        };
        var business = new ProjectBusiness(fakeData);
        var concurrencyToken = Convert.ToBase64String(new byte[] { 1, 2, 3 });

        var result = await business.ArchiveAsync(
            projectId: Guid.NewGuid(),
            expectedConcurrencyToken: concurrencyToken,
            actor: ActorContext.ForUser("user-1"),
            requestContext: RequestContext.CreateNew(),
            archivedAtUtc: Now,
            cancellationToken: CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(ProjectStatusContract.Archived, result.Status);
        Assert.Equal(completionTime, result.ActualCompletionDateUtc);
        Assert.NotNull(fakeData.ArchiveAuditFact);
        Assert.Equal(AuditActions.Archived, fakeData.ArchiveAuditFact.Action);
    }

    [Fact]
    public async Task ArchiveAsync_FromCancelled_Succeeds()
    {
        var fakeData = new FakeProjectData
        {
            FetchedProject = CreateProject(status: ProjectStatus.Cancelled),
        };
        var business = new ProjectBusiness(fakeData);
        var concurrencyToken = Convert.ToBase64String(new byte[] { 1, 2, 3 });

        var result = await business.ArchiveAsync(
            projectId: Guid.NewGuid(),
            expectedConcurrencyToken: concurrencyToken,
            actor: ActorContext.ForUser("user-1"),
            requestContext: RequestContext.CreateNew(),
            archivedAtUtc: Now,
            cancellationToken: CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(ProjectStatusContract.Archived, result.Status);
    }

    [Theory]
    [InlineData(ProjectStatus.Planned)]
    [InlineData(ProjectStatus.Active)]
    [InlineData(ProjectStatus.OnHold)]
    public async Task ArchiveAsync_FromNonTerminalStatus_Throws(ProjectStatus status)
    {
        var fakeData = new FakeProjectData
        {
            FetchedProject = CreateProject(status: status),
        };
        var business = new ProjectBusiness(fakeData);
        var concurrencyToken = Convert.ToBase64String(new byte[] { 1, 2, 3 });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            business.ArchiveAsync(
                projectId: Guid.NewGuid(),
                expectedConcurrencyToken: concurrencyToken,
                actor: ActorContext.ForUser("user-1"),
                requestContext: RequestContext.CreateNew(),
                archivedAtUtc: Now,
                cancellationToken: CancellationToken.None));

        Assert.Contains("only Completed or Cancelled", ex.Message);
    }

    [Fact]
    public async Task ArchiveAsync_WhenProjectNotFound_ReturnsNull()
    {
        var fakeData = new FakeProjectData { FetchedProject = null };
        var business = new ProjectBusiness(fakeData);
        var concurrencyToken = Convert.ToBase64String(new byte[] { 1, 2, 3 });

        var result = await business.ArchiveAsync(
            projectId: Guid.NewGuid(),
            expectedConcurrencyToken: concurrencyToken,
            actor: ActorContext.ForUser("user-1"),
            requestContext: RequestContext.CreateNew(),
            archivedAtUtc: Now,
            cancellationToken: CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task TransitionStatusAsync_BuildsAuditFactWithCorrectMetadata()
    {
        var projectId = Guid.NewGuid();
        var requestContext = RequestContext.CreateNew();

        var fakeData = new FakeProjectData
        {
            FetchedProject = CreateProject(id: projectId, status: ProjectStatus.Planned),
        };
        var business = new ProjectBusiness(fakeData);
        var concurrencyToken = Convert.ToBase64String(new byte[] { 1, 2, 3 });

        await business.TransitionStatusAsync(
            projectId: projectId,
            targetStatus: ProjectStatusContract.Active,
            expectedConcurrencyToken: concurrencyToken,
            actor: ActorContext.ForUser("user-1"),
            requestContext: requestContext,
            transitionedAtUtc: Now);

        var auditFact = fakeData.TransitionAuditFact;
        Assert.NotNull(auditFact);
        Assert.Equal(AuditEntityTypes.Project, auditFact.EntityType);
        Assert.Equal(projectId, auditFact.EntityId);
        Assert.Equal(requestContext.TraceId, auditFact.TraceId);
        Assert.Equal(requestContext.CorrelationId, auditFact.CorrelationId);
    }

    private static Project CreateProject(
        Guid? id = null,
        ProjectStatus status = ProjectStatus.Planned,
        DateTime? actualCompletionDateUtc = null)
    {
        var now = DateTime.UtcNow;
        return Project.Create(
            id: id ?? Guid.NewGuid(),
            clientId: Guid.NewGuid(),
            name: "Test Project",
            status: status,
            priority: ProjectPriority.Normal,
            ownerUserId: "owner-1",
            createdBy: "user-1",
            createdAtUtc: now,
            actualCompletionDateUtc: actualCompletionDateUtc ?? (status == ProjectStatus.Completed ? now : null));
    }
}

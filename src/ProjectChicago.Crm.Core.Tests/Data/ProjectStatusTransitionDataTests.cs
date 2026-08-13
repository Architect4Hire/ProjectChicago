using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ProjectChicago.Contracts.Audit;
using ProjectChicago.Crm.Core.Data;
using ProjectChicago.Crm.Core.Models.DataModels.Entities;
using ProjectChicago.Crm.Core.Persistence;
using ProjectChicago.Crm.Core.Repositories;
using ProjectChicago.Crm.Core.Tests.Persistence;
using ProjectChicago.Shared.Outbox;
using Xunit;

namespace ProjectChicago.Crm.Core.Tests.Data;

// Real SQL Server integration tests for ProjectData's status transition and archive operations
// (PROJECT-010..014, DATA-008, AUDIT-001..008, OUTBOX-001/002; messaging.md publish-side test
// matrix: "state + outbox commit together" / "concurrency token enforcement" / "completion
// timestamp recording").
public class ProjectStatusTransitionDataTests : IClassFixture<MsSqlContainerFixture>
{
    private readonly MsSqlContainerFixture _fixture;

    public ProjectStatusTransitionDataTests(MsSqlContainerFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<CrmDbContext> CreateContextAsync(string databaseName)
    {
        var builder = new SqlConnectionStringBuilder(_fixture.ConnectionString)
        {
            InitialCatalog = databaseName,
        };

        var options = new DbContextOptionsBuilder<CrmDbContext>()
            .UseSqlServer(builder.ConnectionString)
            .Options;

        var context = new CrmDbContext(options);
        await context.Database.EnsureCreatedAsync();
        return context;
    }

    private static readonly DateTime CreatedAtUtc = new(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);

    private static Client CreateClient(Guid id, string name = "Acme Corporation") =>
        Client.Create(
            id: id,
            name: name,
            lifecycleStatus: ClientLifecycleStatus.Lead,
            ownerUserId: "owner-1",
            createdBy: "creator-1",
            createdAtUtc: CreatedAtUtc,
            primaryEmail: "jane@acme.example",
            primaryPhone: "+1-555-0100");

    private static Project CreateProject(Guid id, Guid clientId, string name = "Website Redesign") =>
        Project.Create(
            id: id,
            clientId: clientId,
            name: name,
            status: ProjectStatus.Planned,
            priority: ProjectPriority.Normal,
            ownerUserId: "owner-1",
            createdBy: "creator-1",
            createdAtUtc: CreatedAtUtc,
            description: "Redesign the client website",
            startDateUtc: CreatedAtUtc.AddDays(1),
            targetCompletionDateUtc: CreatedAtUtc.AddDays(30));

    private static EntityMutationAudited CreateAuditFact(
        Guid projectId,
        string action = AuditActions.StatusChanged,
        Guid? eventId = null) => new()
        {
            EventId = (eventId ?? Guid.NewGuid()).ToString(),
            OccurredAtUtc = new DateTimeOffset(CreatedAtUtc),
            SourceService = AuditSourceServices.Crm,
            EntityType = AuditEntityTypes.Project,
            EntityId = projectId,
            Action = action,
            ActorId = "user-1",
            ActorType = AuditActorTypes.User,
            TraceId = Guid.NewGuid().ToString("N"),
            CorrelationId = Guid.NewGuid().ToString(),
            CausationId = Guid.NewGuid().ToString(),
            ChangedFields = [nameof(Project.Status)],
        };

    [Fact]
    public async Task TransitionStatusAsync_PersistsProjectStateAndAuditFactAtomically()
    {
        var db = nameof(TransitionStatusAsync_PersistsProjectStateAndAuditFactAtomically);
        await using var setupContext = await CreateContextAsync(db);
        var setupData = new ClientData(setupContext, new ClientRepository(setupContext));
        var clientId = Guid.NewGuid();
        var client = CreateClient(clientId);
        await setupData.CreateAsync(client, CreateAuditFact(clientId, AuditActions.Created), CancellationToken.None);

        var projectId = Guid.NewGuid();
        var project = CreateProject(projectId, clientId);
        var setupProjectData = new ProjectData(setupContext, new ProjectRepository(setupContext));
        await setupProjectData.CreateAsync(project, CreateAuditFact(projectId), CancellationToken.None);

        await using var transitionContext = await CreateContextAsync(db);
        var transitionRepository = new ProjectRepository(transitionContext);
        var transitionData = new ProjectData(transitionContext, transitionRepository);
        var fetchedProject = await transitionRepository.GetAsync(projectId, CancellationToken.None);
        var concurrencyToken = Convert.ToBase64String(fetchedProject!.RowVersion);

        var transitionTime = CreatedAtUtc.AddHours(1);
        var auditFact = CreateAuditFact(projectId);

        await transitionData.TransitionStatusAsync(
            fetchedProject,
            ProjectStatus.Active,
            "user-2",
            transitionTime,
            completionTimestampUtc: null,
            expectedConcurrencyToken: concurrencyToken,
            auditFact: auditFact,
            cancellationToken: CancellationToken.None);

        await using var verifyContext = await CreateContextAsync(db);
        var updatedProject = await verifyContext.Projects.FirstAsync(p => p.Id == projectId);
        var outboxMessages = await verifyContext.OutboxMessages
            .Where(m => m.CorrelationId == auditFact.CorrelationId)
            .ToListAsync();

        Assert.Equal(ProjectStatus.Active, updatedProject.Status);
        Assert.Equal("user-2", updatedProject.LastModifiedBy);
        Assert.Equal(transitionTime, updatedProject.LastModifiedAtUtc);
        Assert.Single(outboxMessages);
        Assert.Equal(AuditSourceServices.Crm, outboxMessages[0].CorrelationId);
    }

    [Fact]
    public async Task TransitionStatusAsync_RecordsCompletionTimestampWhenTransitioningToCompleted()
    {
        var db = nameof(TransitionStatusAsync_RecordsCompletionTimestampWhenTransitioningToCompleted);
        await using var setupContext = await CreateContextAsync(db);
        var setupData = new ClientData(setupContext, new ClientRepository(setupContext));
        var clientId = Guid.NewGuid();
        var client = CreateClient(clientId);
        await setupData.CreateAsync(client, CreateAuditFact(clientId, AuditActions.Created), CancellationToken.None);

        var projectId = Guid.NewGuid();
        var project = CreateProject(projectId, clientId);
        project.TransitionStatus(ProjectStatus.Active, "user-1", CreatedAtUtc.AddHours(1), completionTimestampUtc: null);
        var setupProjectData = new ProjectData(setupContext, new ProjectRepository(setupContext));
        await setupProjectData.CreateAsync(project, CreateAuditFact(projectId), CancellationToken.None);

        await using var transitionContext = await CreateContextAsync(db);
        var transitionRepository = new ProjectRepository(transitionContext);
        var transitionData = new ProjectData(transitionContext, transitionRepository);
        var fetchedProject = await transitionRepository.GetAsync(projectId, CancellationToken.None);
        var concurrencyToken = Convert.ToBase64String(fetchedProject!.RowVersion);

        var completionTime = CreatedAtUtc.AddDays(30);
        var auditFact = CreateAuditFact(projectId);

        await transitionData.TransitionStatusAsync(
            fetchedProject,
            ProjectStatus.Completed,
            "user-2",
            completionTime,
            completionTimestampUtc: completionTime,
            expectedConcurrencyToken: concurrencyToken,
            auditFact: auditFact,
            cancellationToken: CancellationToken.None);

        await using var verifyContext = await CreateContextAsync(db);
        var completedProject = await verifyContext.Projects.FirstAsync(p => p.Id == projectId);

        Assert.Equal(ProjectStatus.Completed, completedProject.Status);
        Assert.Equal(completionTime, completedProject.ActualCompletionDateUtc);
    }

    [Fact]
    public async Task TransitionStatusAsync_WithMismatchedConcurrencyToken_ThrowsDbUpdateConcurrencyException()
    {
        var db = nameof(TransitionStatusAsync_WithMismatchedConcurrencyToken_ThrowsDbUpdateConcurrencyException);
        await using var setupContext = await CreateContextAsync(db);
        var setupData = new ClientData(setupContext, new ClientRepository(setupContext));
        var clientId = Guid.NewGuid();
        var client = CreateClient(clientId);
        await setupData.CreateAsync(client, CreateAuditFact(clientId, AuditActions.Created), CancellationToken.None);

        var projectId = Guid.NewGuid();
        var project = CreateProject(projectId, clientId);
        var setupProjectData = new ProjectData(setupContext, new ProjectRepository(setupContext));
        await setupProjectData.CreateAsync(project, CreateAuditFact(projectId), CancellationToken.None);

        // Modify the project to change its rowversion by updating through raw SQL
        // (EF property setters are private, so we bypass them for test purposes)
        await using var modifyContext = await CreateContextAsync(db);
        await modifyContext.Database.ExecuteSqlInterpolatedAsync(
            $@"UPDATE Projects SET LastModifiedBy = 'test-modifier' WHERE Id = {projectId}");

        // Now try to transition with the old token
        await using var transitionContext = await CreateContextAsync(db);
        var transitionRepository = new ProjectRepository(transitionContext);
        var transitionData = new ProjectData(transitionContext, transitionRepository);
        var fetchedProject = await transitionRepository.GetAsync(projectId, CancellationToken.None);
        var staleToken = Convert.ToBase64String(new byte[] { 1, 2, 3 }); // Wrong token
        var auditFact = CreateAuditFact(projectId);

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
            transitionData.TransitionStatusAsync(
                fetchedProject!,
                ProjectStatus.Active,
                "user-2",
                CreatedAtUtc.AddHours(1),
                completionTimestampUtc: null,
                expectedConcurrencyToken: staleToken,
                auditFact: auditFact,
                cancellationToken: CancellationToken.None));
    }

    [Fact]
    public async Task ArchiveAsync_PersistsProjectStateAndAuditFactAtomically()
    {
        var db = nameof(ArchiveAsync_PersistsProjectStateAndAuditFactAtomically);
        await using var setupContext = await CreateContextAsync(db);
        var setupData = new ClientData(setupContext, new ClientRepository(setupContext));
        var clientId = Guid.NewGuid();
        var client = CreateClient(clientId);
        await setupData.CreateAsync(client, CreateAuditFact(clientId, AuditActions.Created), CancellationToken.None);

        var projectId = Guid.NewGuid();
        var project = CreateProject(projectId, clientId);
        project.TransitionStatus(ProjectStatus.Active, "user-1", CreatedAtUtc.AddHours(1), completionTimestampUtc: null);
        var completionTime = CreatedAtUtc.AddDays(30);
        project.TransitionStatus(ProjectStatus.Completed, "user-1", completionTime, completionTimestampUtc: completionTime);
        var setupProjectData = new ProjectData(setupContext, new ProjectRepository(setupContext));
        await setupProjectData.CreateAsync(project, CreateAuditFact(projectId), CancellationToken.None);

        await using var archiveContext = await CreateContextAsync(db);
        var archiveRepository = new ProjectRepository(archiveContext);
        var archiveData = new ProjectData(archiveContext, archiveRepository);
        var fetchedProject = await archiveRepository.GetAsync(projectId, CancellationToken.None);
        var concurrencyToken = Convert.ToBase64String(fetchedProject!.RowVersion);
        var archiveTime = completionTime.AddDays(1);
        var auditFact = CreateAuditFact(projectId, AuditActions.Archived);

        await archiveData.ArchiveAsync(
            fetchedProject,
            "user-2",
            archiveTime,
            expectedConcurrencyToken: concurrencyToken,
            auditFact: auditFact,
            cancellationToken: CancellationToken.None);

        await using var verifyContext = await CreateContextAsync(db);
        var archivedProject = await verifyContext.Projects.FirstAsync(p => p.Id == projectId);
        var outboxMessages = await verifyContext.OutboxMessages
            .Where(m => m.CorrelationId == auditFact.CorrelationId)
            .ToListAsync();

        Assert.Equal(ProjectStatus.Archived, archivedProject.Status);
        Assert.Equal(completionTime, archivedProject.ActualCompletionDateUtc); // Preserved
        Assert.Equal("user-2", archivedProject.LastModifiedBy);
        Assert.Equal(archiveTime, archivedProject.LastModifiedAtUtc);
        Assert.Single(outboxMessages);
    }

    [Fact]
    public async Task ArchiveAsync_WithMismatchedConcurrencyToken_ThrowsDbUpdateConcurrencyException()
    {
        var db = nameof(ArchiveAsync_WithMismatchedConcurrencyToken_ThrowsDbUpdateConcurrencyException);
        await using var setupContext = await CreateContextAsync(db);
        var setupData = new ClientData(setupContext, new ClientRepository(setupContext));
        var clientId = Guid.NewGuid();
        var client = CreateClient(clientId);
        await setupData.CreateAsync(client, CreateAuditFact(clientId, AuditActions.Created), CancellationToken.None);

        var projectId = Guid.NewGuid();
        var project = CreateProject(projectId, clientId);
        var completionTime = CreatedAtUtc.AddDays(30);
        project.TransitionStatus(ProjectStatus.Completed, "user-1", completionTime, completionTimestampUtc: completionTime);
        var setupProjectData = new ProjectData(setupContext, new ProjectRepository(setupContext));
        await setupProjectData.CreateAsync(project, CreateAuditFact(projectId), CancellationToken.None);

        var staleToken = Convert.ToBase64String(new byte[] { 1, 2, 3 }); // Wrong token
        await using var archiveContext = await CreateContextAsync(db);
        var archiveRepository = new ProjectRepository(archiveContext);
        var archiveData = new ProjectData(archiveContext, archiveRepository);
        var fetchedProject = await archiveRepository.GetAsync(projectId, CancellationToken.None);
        var auditFact = CreateAuditFact(projectId, AuditActions.Archived);

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
            archiveData.ArchiveAsync(
                fetchedProject!,
                "user-2",
                CreatedAtUtc.AddDays(31),
                expectedConcurrencyToken: staleToken,
                auditFact: auditFact,
                cancellationToken: CancellationToken.None));
    }
}

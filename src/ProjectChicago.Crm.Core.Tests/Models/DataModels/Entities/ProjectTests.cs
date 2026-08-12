using ProjectChicago.Crm.Core.Models.DataModels.Entities;
using Xunit;

namespace ProjectChicago.Crm.Core.Tests.Models.DataModels.Entities;

// Entity-level invariant tests only (PROJECT-001..014, DATA-001..008). No EF/persistence
// involvement - these assert what Project.Create enforces regardless of how it is later stored.
public class ProjectTests
{
    private static readonly DateTime CreatedAtUtc = new(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);

    private static Project CreateValidProject(
        ProjectStatus status = ProjectStatus.Planned,
        ProjectPriority priority = ProjectPriority.Normal,
        DateTime? createdAtUtc = null,
        DateTime? actualCompletionDateUtc = null) =>
        Project.Create(
            id: Guid.NewGuid(),
            clientId: Guid.NewGuid(),
            name: "Website Redesign",
            status: status,
            priority: priority,
            ownerUserId: "owner-1",
            createdBy: "creator-1",
            createdAtUtc: createdAtUtc ?? CreatedAtUtc,
            description: "Redesign the marketing site.",
            startDateUtc: CreatedAtUtc,
            targetCompletionDateUtc: CreatedAtUtc.AddMonths(3),
            actualCompletionDateUtc: actualCompletionDateUtc,
            notes: "Kickoff scheduled.");

    [Fact]
    public void Create_WithValidArguments_SetsAllProvidedValues()
    {
        var id = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var targetCompletionDateUtc = CreatedAtUtc.AddMonths(3);

        var project = Project.Create(
            id: id,
            clientId: clientId,
            name: "Website Redesign",
            status: ProjectStatus.Active,
            priority: ProjectPriority.High,
            ownerUserId: "owner-1",
            createdBy: "creator-1",
            createdAtUtc: CreatedAtUtc,
            description: "Redesign the marketing site.",
            startDateUtc: CreatedAtUtc,
            targetCompletionDateUtc: targetCompletionDateUtc,
            notes: "Kickoff scheduled.");

        Assert.Equal(id, project.Id);
        Assert.Equal(clientId, project.ClientId);
        Assert.Equal("Website Redesign", project.Name);
        Assert.Equal(ProjectStatus.Active, project.Status);
        Assert.Equal(ProjectPriority.High, project.Priority);
        Assert.Equal("owner-1", project.OwnerUserId);
        Assert.Equal("Redesign the marketing site.", project.Description);
        Assert.Equal(CreatedAtUtc, project.StartDateUtc);
        Assert.Equal(targetCompletionDateUtc, project.TargetCompletionDateUtc);
        Assert.Null(project.ActualCompletionDateUtc);
        Assert.Equal("Kickoff scheduled.", project.Notes);
    }

    [Fact]
    public void Create_WithoutOptionalFields_LeavesThemNull()
    {
        var project = Project.Create(
            id: Guid.NewGuid(),
            clientId: Guid.NewGuid(),
            name: "Website Redesign",
            status: ProjectStatus.Planned,
            priority: ProjectPriority.Normal,
            ownerUserId: "owner-1",
            createdBy: "creator-1",
            createdAtUtc: CreatedAtUtc);

        Assert.Null(project.Description);
        Assert.Null(project.StartDateUtc);
        Assert.Null(project.TargetCompletionDateUtc);
        Assert.Null(project.ActualCompletionDateUtc);
        Assert.Null(project.Notes);
    }

    [Fact]
    public void Create_SetsLastModifiedMetadataEqualToCreatedMetadata()
    {
        var project = CreateValidProject();

        Assert.Equal(project.CreatedAtUtc, project.LastModifiedAtUtc);
        Assert.Equal(project.CreatedBy, project.LastModifiedBy);
    }

    [Fact]
    public void Create_AssignsAnEmptyRowVersion_UntilPersistence()
    {
        var project = CreateValidProject();

        Assert.Empty(project.RowVersion);
    }

    [Fact]
    public void Create_WithEmptyId_Throws()
    {
        var exception = Assert.Throws<ArgumentException>(() => Project.Create(
            id: Guid.Empty,
            clientId: Guid.NewGuid(),
            name: "Website Redesign",
            status: ProjectStatus.Planned,
            priority: ProjectPriority.Normal,
            ownerUserId: "owner-1",
            createdBy: "creator-1",
            createdAtUtc: CreatedAtUtc));

        Assert.Equal("id", exception.ParamName);
    }

    [Fact]
    public void Create_WithEmptyClientId_Throws_FromData002()
    {
        var exception = Assert.Throws<ArgumentException>(() => Project.Create(
            id: Guid.NewGuid(),
            clientId: Guid.Empty,
            name: "Website Redesign",
            status: ProjectStatus.Planned,
            priority: ProjectPriority.Normal,
            ownerUserId: "owner-1",
            createdBy: "creator-1",
            createdAtUtc: CreatedAtUtc));

        Assert.Equal("clientId", exception.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithNullOrWhitespaceName_Throws(string? name)
    {
        var exception = Assert.Throws<ArgumentException>(() => Project.Create(
            id: Guid.NewGuid(),
            clientId: Guid.NewGuid(),
            name: name!,
            status: ProjectStatus.Planned,
            priority: ProjectPriority.Normal,
            ownerUserId: "owner-1",
            createdBy: "creator-1",
            createdAtUtc: CreatedAtUtc));

        Assert.Equal("name", exception.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithNullOrWhitespaceOwnerUserId_Throws(string? ownerUserId)
    {
        var exception = Assert.Throws<ArgumentException>(() => Project.Create(
            id: Guid.NewGuid(),
            clientId: Guid.NewGuid(),
            name: "Website Redesign",
            status: ProjectStatus.Planned,
            priority: ProjectPriority.Normal,
            ownerUserId: ownerUserId!,
            createdBy: "creator-1",
            createdAtUtc: CreatedAtUtc));

        Assert.Equal("ownerUserId", exception.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithNullOrWhitespaceCreatedBy_Throws(string? createdBy)
    {
        var exception = Assert.Throws<ArgumentException>(() => Project.Create(
            id: Guid.NewGuid(),
            clientId: Guid.NewGuid(),
            name: "Website Redesign",
            status: ProjectStatus.Planned,
            priority: ProjectPriority.Normal,
            ownerUserId: "owner-1",
            createdBy: createdBy!,
            createdAtUtc: CreatedAtUtc));

        Assert.Equal("createdBy", exception.ParamName);
    }

    [Fact]
    public void Create_WithUndefinedStatus_Throws()
    {
        var exception = Assert.Throws<ArgumentException>(() => Project.Create(
            id: Guid.NewGuid(),
            clientId: Guid.NewGuid(),
            name: "Website Redesign",
            status: (ProjectStatus)999,
            priority: ProjectPriority.Normal,
            ownerUserId: "owner-1",
            createdBy: "creator-1",
            createdAtUtc: CreatedAtUtc));

        Assert.Equal("status", exception.ParamName);
    }

    [Fact]
    public void Create_WithUndefinedPriority_Throws()
    {
        var exception = Assert.Throws<ArgumentException>(() => Project.Create(
            id: Guid.NewGuid(),
            clientId: Guid.NewGuid(),
            name: "Website Redesign",
            status: ProjectStatus.Planned,
            priority: (ProjectPriority)999,
            ownerUserId: "owner-1",
            createdBy: "creator-1",
            createdAtUtc: CreatedAtUtc));

        Assert.Equal("priority", exception.ParamName);
    }

    [Theory]
    [InlineData(ProjectStatus.Planned)]
    [InlineData(ProjectStatus.Active)]
    [InlineData(ProjectStatus.OnHold)]
    [InlineData(ProjectStatus.Cancelled)]
    [InlineData(ProjectStatus.Archived)]
    public void Create_AllowsEveryNonCompletedInitialStatus_FromProject010(ProjectStatus status)
    {
        var project = CreateValidProject(status: status);

        Assert.Equal(status, project.Status);
    }

    [Fact]
    public void Create_WithCompletedStatusAndActualCompletionDate_Succeeds_FromProject012()
    {
        var project = CreateValidProject(
            status: ProjectStatus.Completed,
            actualCompletionDateUtc: CreatedAtUtc.AddMonths(2));

        Assert.Equal(ProjectStatus.Completed, project.Status);
        Assert.Equal(CreatedAtUtc.AddMonths(2), project.ActualCompletionDateUtc);
    }

    [Fact]
    public void Create_WithCompletedStatusAndNoActualCompletionDate_Throws_FromProject012()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => CreateValidProject(status: ProjectStatus.Completed, actualCompletionDateUtc: null));

        Assert.Equal("actualCompletionDateUtc", exception.ParamName);
    }

    [Fact]
    public void Create_WithLocalCreatedAtUtc_Throws()
    {
        var localTime = DateTime.SpecifyKind(CreatedAtUtc, DateTimeKind.Local);

        var exception = Assert.Throws<ArgumentException>(() => CreateValidProject(createdAtUtc: localTime));

        Assert.Equal("createdAtUtc", exception.ParamName);
    }

    [Fact]
    public void Create_WithUnspecifiedCreatedAtUtcKind_Throws()
    {
        var unspecifiedTime = DateTime.SpecifyKind(CreatedAtUtc, DateTimeKind.Unspecified);

        var exception = Assert.Throws<ArgumentException>(() => CreateValidProject(createdAtUtc: unspecifiedTime));

        Assert.Equal("createdAtUtc", exception.ParamName);
    }

    [Fact]
    public void Create_WithLocalStartDateUtc_Throws()
    {
        var localTime = DateTime.SpecifyKind(CreatedAtUtc, DateTimeKind.Local);

        var exception = Assert.Throws<ArgumentException>(() => Project.Create(
            id: Guid.NewGuid(),
            clientId: Guid.NewGuid(),
            name: "Website Redesign",
            status: ProjectStatus.Planned,
            priority: ProjectPriority.Normal,
            ownerUserId: "owner-1",
            createdBy: "creator-1",
            createdAtUtc: CreatedAtUtc,
            startDateUtc: localTime));

        Assert.Equal("startDateUtc", exception.ParamName);
    }

    [Fact]
    public void Create_WithLocalTargetCompletionDateUtc_Throws()
    {
        var localTime = DateTime.SpecifyKind(CreatedAtUtc, DateTimeKind.Local);

        var exception = Assert.Throws<ArgumentException>(() => Project.Create(
            id: Guid.NewGuid(),
            clientId: Guid.NewGuid(),
            name: "Website Redesign",
            status: ProjectStatus.Planned,
            priority: ProjectPriority.Normal,
            ownerUserId: "owner-1",
            createdBy: "creator-1",
            createdAtUtc: CreatedAtUtc,
            targetCompletionDateUtc: localTime));

        Assert.Equal("targetCompletionDateUtc", exception.ParamName);
    }

    [Fact]
    public void Create_WithLocalActualCompletionDateUtc_Throws()
    {
        var localTime = DateTime.SpecifyKind(CreatedAtUtc, DateTimeKind.Local);

        var exception = Assert.Throws<ArgumentException>(() => Project.Create(
            id: Guid.NewGuid(),
            clientId: Guid.NewGuid(),
            name: "Website Redesign",
            status: ProjectStatus.Completed,
            priority: ProjectPriority.Normal,
            ownerUserId: "owner-1",
            createdBy: "creator-1",
            createdAtUtc: CreatedAtUtc,
            actualCompletionDateUtc: localTime));

        Assert.Equal("actualCompletionDateUtc", exception.ParamName);
    }
}

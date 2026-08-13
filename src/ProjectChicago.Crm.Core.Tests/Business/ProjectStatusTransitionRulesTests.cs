using ProjectChicago.Crm.Core.Models.DataModels.Entities;
using Xunit;

namespace ProjectChicago.Crm.Core.Tests.Business;

// Pure unit tests for PROJECT-010..014 status transition rules, isolated from Business
// orchestration tests. Tests the state machine rules that govern allowed Project transitions.
public class ProjectStatusTransitionRulesTests
{
    [Theory]
    [InlineData(ProjectStatus.Planned, ProjectStatus.Active)]
    [InlineData(ProjectStatus.Planned, ProjectStatus.Cancelled)]
    [InlineData(ProjectStatus.Active, ProjectStatus.OnHold)]
    [InlineData(ProjectStatus.Active, ProjectStatus.Completed)]
    [InlineData(ProjectStatus.OnHold, ProjectStatus.Active)]
    [InlineData(ProjectStatus.OnHold, ProjectStatus.Cancelled)]
    [InlineData(ProjectStatus.Completed, ProjectStatus.Archived)]
    [InlineData(ProjectStatus.Cancelled, ProjectStatus.Archived)]
    public void IsValidTransition_ForAllowedTransitions_ReturnsTrue(ProjectStatus from, ProjectStatus to)
    {
        var project = CreateProject(status: from);

        var ex = Record.Exception(() =>
        {
            project.TransitionStatus(to, "user-1", DateTime.UtcNow, completionTimestampUtc: null);
        });

        Assert.Null(ex);
        Assert.Equal(to, project.Status);
    }

    [Theory]
    [InlineData(ProjectStatus.Planned, ProjectStatus.OnHold)]
    [InlineData(ProjectStatus.Planned, ProjectStatus.Archived)]
    [InlineData(ProjectStatus.Planned, ProjectStatus.Completed)]
    [InlineData(ProjectStatus.Active, ProjectStatus.Cancelled)]
    [InlineData(ProjectStatus.Active, ProjectStatus.Archived)]
    [InlineData(ProjectStatus.OnHold, ProjectStatus.Completed)]
    [InlineData(ProjectStatus.OnHold, ProjectStatus.Archived)]
    [InlineData(ProjectStatus.Cancelled, ProjectStatus.Active)]
    [InlineData(ProjectStatus.Archived, ProjectStatus.Active)]
    public void IsValidTransition_ForDisallowedTransitions_Throws(ProjectStatus from, ProjectStatus to)
    {
        var project = CreateProject(status: from);

        var ex = Assert.Throws<InvalidOperationException>(() =>
        {
            project.TransitionStatus(to, "user-1", DateTime.UtcNow, completionTimestampUtc: null);
        });

        Assert.Contains("Cannot transition", ex.Message);
    }

    [Fact]
    public void Archive_FromCompleted_Succeeds()
    {
        var completionTime = new DateTime(2026, 1, 10, 12, 0, 0, DateTimeKind.Utc);
        var project = CreateProject(status: ProjectStatus.Completed, actualCompletionDateUtc: completionTime);

        project.Archive("user-1", DateTime.UtcNow);

        Assert.Equal(ProjectStatus.Archived, project.Status);
        // Completion timestamp is preserved when archiving
        Assert.Equal(completionTime, project.ActualCompletionDateUtc);
    }

    [Fact]
    public void Archive_FromCancelled_Succeeds()
    {
        var project = CreateProject(status: ProjectStatus.Cancelled);

        project.Archive("user-1", DateTime.UtcNow);

        Assert.Equal(ProjectStatus.Archived, project.Status);
    }

    [Theory]
    [InlineData(ProjectStatus.Planned)]
    [InlineData(ProjectStatus.Active)]
    [InlineData(ProjectStatus.OnHold)]
    public void Archive_FromNonTerminalStatus_Throws(ProjectStatus status)
    {
        var project = CreateProject(status: status);

        var ex = Assert.Throws<InvalidOperationException>(() =>
        {
            project.Archive("user-1", DateTime.UtcNow);
        });

        Assert.Contains("only Completed or Cancelled", ex.Message);
    }

    [Fact]
    public void TransitionToCompleted_WithoutCompletionTimestamp_Throws()
    {
        var project = CreateProject(status: ProjectStatus.Active);

        var ex = Assert.Throws<ArgumentException>(() =>
        {
            project.TransitionStatus(ProjectStatus.Completed, "user-1", DateTime.UtcNow, completionTimestampUtc: null);
        });

        Assert.Contains("completion timestamp", ex.Message);
    }

    [Fact]
    public void TransitionToCompleted_WithCompletionTimestamp_RecordsIt()
    {
        var project = CreateProject(status: ProjectStatus.Active);
        var completionTime = new DateTime(2026, 1, 10, 15, 30, 0, DateTimeKind.Utc);

        project.TransitionStatus(ProjectStatus.Completed, "user-1", DateTime.UtcNow, completionTimestampUtc: completionTime);

        Assert.Equal(ProjectStatus.Completed, project.Status);
        Assert.Equal(completionTime, project.ActualCompletionDateUtc);
    }

    [Fact]
    public void TransitionFromCompleted_ToNonArchived_Throws()
    {
        var completionTime = new DateTime(2026, 1, 10, 12, 0, 0, DateTimeKind.Utc);
        var project = CreateProject(status: ProjectStatus.Completed, actualCompletionDateUtc: completionTime);

        var ex = Assert.Throws<InvalidOperationException>(() =>
        {
            project.TransitionStatus(ProjectStatus.Cancelled, "user-1", DateTime.UtcNow, completionTimestampUtc: null);
        });

        Assert.Contains("can only transition to Archived", ex.Message);
    }

    [Fact]
    public void TransitionUpdatesLastModifiedMetadata()
    {
        var project = CreateProject(status: ProjectStatus.Planned);
        var originalLastModified = project.LastModifiedAtUtc;
        var newModifiedTime = originalLastModified.AddHours(1);

        project.TransitionStatus(ProjectStatus.Active, "user-2", newModifiedTime, completionTimestampUtc: null);

        Assert.Equal("user-2", project.LastModifiedBy);
        Assert.Equal(newModifiedTime, project.LastModifiedAtUtc);
    }

    private static Project CreateProject(
        ProjectStatus status = ProjectStatus.Planned,
        DateTime? actualCompletionDateUtc = null)
    {
        var now = DateTime.UtcNow;
        return Project.Create(
            id: Guid.NewGuid(),
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

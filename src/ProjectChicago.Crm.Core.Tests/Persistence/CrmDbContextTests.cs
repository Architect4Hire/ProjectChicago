using Microsoft.EntityFrameworkCore;
using ProjectChicago.Crm.Core.Models.DataModels.Entities;
using ProjectChicago.Crm.Core.Persistence;
using ProjectChicago.Shared.Inbox;
using ProjectChicago.Shared.Outbox;
using Xunit;

namespace ProjectChicago.Crm.Core.Tests.Persistence;

// Confirms CrmDbContext maps the Shared outbox/inbox mechanism plus Client, Project, and TaskItem
// (DbSet + ClientConfiguration/ProjectConfiguration/TaskItemConfiguration for all three - no
// repository/migration for any of them yet). Uses the SQL Server provider to build the model
// (matching the provider CrmDbContext is built for) without opening a connection - no query/save
// executes against a real database.
public class CrmDbContextTests
{
    private static CrmDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CrmDbContext>()
            .UseSqlServer("Server=.;Database=CrmDbContextTests;")
            .Options;

        return new CrmDbContext(options);
    }

    [Fact]
    public void Model_MapsOnlyOutboxInboxClientProjectAndTaskItemEntityTypes()
    {
        using var context = CreateContext();

        var mappedClrTypes = context.Model.GetEntityTypes()
            .Select(e => e.ClrType)
            .ToArray();

        Assert.Equal(
            [typeof(Client), typeof(InboxMessage), typeof(OutboxMessage), typeof(Project), typeof(TaskItem)],
            mappedClrTypes.OrderBy(t => t.Name));
    }

    [Fact]
    public void OutboxMessages_DbSet_MapsToOutboxMessagesTable()
    {
        using var context = CreateContext();

        var entityType = context.Model.FindEntityType(typeof(OutboxMessage));

        Assert.NotNull(entityType);
        Assert.Equal("OutboxMessages", entityType!.GetTableName());
    }

    [Fact]
    public void InboxMessages_DbSet_MapsToInboxMessagesTable()
    {
        using var context = CreateContext();

        var entityType = context.Model.FindEntityType(typeof(InboxMessage));

        Assert.NotNull(entityType);
        Assert.Equal("InboxMessages", entityType!.GetTableName());
    }

    [Fact]
    public void Clients_DbSet_MapsToClientsTable()
    {
        using var context = CreateContext();

        var entityType = context.Model.FindEntityType(typeof(Client));

        Assert.NotNull(entityType);
        Assert.Equal("Clients", entityType!.GetTableName());
    }

    [Fact]
    public void Projects_DbSet_MapsToProjectsTable()
    {
        using var context = CreateContext();

        var entityType = context.Model.FindEntityType(typeof(Project));

        Assert.NotNull(entityType);
        Assert.Equal("Projects", entityType!.GetTableName());
    }

    // DATA-002: a Project cannot exist without a Client. Confirmed here through the Projects DbSet
    // (ProjectConfigurationTests covers the same invariant at the ProjectConfiguration level).
    [Fact]
    public void Projects_RequireClient_ExactlyOneRequiredNonCascadingForeignKey()
    {
        using var context = CreateContext();

        var entityType = context.Model.FindEntityType(typeof(Project));

        Assert.NotNull(entityType);
        var foreignKey = Assert.Single(entityType!.GetForeignKeys());

        Assert.Equal(nameof(Client), foreignKey.PrincipalEntityType.ClrType.Name);
        Assert.True(foreignKey.IsRequired);
        Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior);
    }

    [Fact]
    public void Tasks_DbSet_MapsToTasksTable()
    {
        using var context = CreateContext();

        var entityType = context.Model.FindEntityType(typeof(TaskItem));

        Assert.NotNull(entityType);
        Assert.Equal("Tasks", entityType!.GetTableName());
    }

    // DATA-003: a Task cannot exist without a Project. Confirmed here at the CrmDbContext level
    // (TaskItemConfigurationTests covers the same invariant at the TaskItemConfiguration level).
    [Fact]
    public void Tasks_RequireProject_ExactlyOneRequiredNonCascadingForeignKey()
    {
        using var context = CreateContext();

        var entityType = context.Model.FindEntityType(typeof(TaskItem));

        Assert.NotNull(entityType);
        var foreignKey = Assert.Single(entityType!.GetForeignKeys());

        Assert.Equal(nameof(Project), foreignKey.PrincipalEntityType.ClrType.Name);
        Assert.True(foreignKey.IsRequired);
        Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior);
    }

    // DATA-002 + DATA-003: proves the full Task -> Project -> Client required-relationship chain
    // exists in the model, not just each link in isolation.
    [Fact]
    public void TaskProjectClient_RequiredRelationshipChain_ExistsInModel()
    {
        using var context = CreateContext();

        var taskEntityType = context.Model.FindEntityType(typeof(TaskItem));
        var projectEntityType = context.Model.FindEntityType(typeof(Project));

        Assert.NotNull(taskEntityType);
        Assert.NotNull(projectEntityType);

        var taskToProject = Assert.Single(taskEntityType!.GetForeignKeys());
        Assert.Equal(nameof(Project), taskToProject.PrincipalEntityType.ClrType.Name);
        Assert.True(taskToProject.IsRequired);

        var projectToClient = Assert.Single(projectEntityType!.GetForeignKeys());
        Assert.Equal(nameof(Client), projectToClient.PrincipalEntityType.ClrType.Name);
        Assert.True(projectToClient.IsRequired);
    }
}

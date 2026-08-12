using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using ProjectChicago.Crm.Core.Models.DataModels.Entities;
using ProjectChicago.Crm.Core.Persistence;
using Xunit;

namespace ProjectChicago.Crm.Core.Tests.Persistence;

// Model-metadata tests for TaskItemConfiguration (TASK-001..022, DATA-003..005; database.md).
// Builds the SQL Server model without opening a connection - no query/save executes against a
// real database. Looks up the entity type via the model directly (rather than a DbSet, since none
// exists for TaskItem yet) since these tests target mapping metadata, not query/save behavior.
public class TaskItemConfigurationTests
{
    private static IEntityType GetTaskItemEntityType()
    {
        var options = new DbContextOptionsBuilder<CrmDbContext>()
            .UseSqlServer("Server=.;Database=TaskItemConfigurationTests;")
            .Options;

        using var context = new CrmDbContext(options);

        var entityType = context.Model.FindEntityType(typeof(TaskItem));
        Assert.NotNull(entityType);

        return entityType!;
    }

    [Fact]
    public void TaskItem_MapsToTasksTable()
    {
        var entityType = GetTaskItemEntityType();

        Assert.Equal("Tasks", entityType.GetTableName());
    }

    [Fact]
    public void TaskItem_PrimaryKeyIsId()
    {
        var entityType = GetTaskItemEntityType();

        var primaryKey = entityType.FindPrimaryKey();

        Assert.NotNull(primaryKey);
        Assert.Equal([nameof(TaskItem.Id)], primaryKey!.Properties.Select(p => p.Name));
    }

    [Fact]
    public void TaskItem_Id_IsNeverDatabaseGenerated()
    {
        var entityType = GetTaskItemEntityType();

        var idProperty = entityType.FindProperty(nameof(TaskItem.Id));

        Assert.NotNull(idProperty);
        Assert.Equal(ValueGenerated.Never, idProperty!.ValueGenerated);
        Assert.Equal("uniqueidentifier", idProperty.GetColumnType());
    }

    [Theory]
    [InlineData(nameof(TaskItem.Title), 200)]
    [InlineData(nameof(TaskItem.Description), 2000)]
    [InlineData(nameof(TaskItem.AssignedUserId), 128)]
    [InlineData(nameof(TaskItem.Notes), 2000)]
    [InlineData(nameof(TaskItem.CreatedBy), 128)]
    [InlineData(nameof(TaskItem.LastModifiedBy), 128)]
    public void TaskItem_BoundedStringProperty_HasExpectedMaxLength(string propertyName, int expectedMaxLength)
    {
        var entityType = GetTaskItemEntityType();

        var property = entityType.FindProperty(propertyName);

        Assert.NotNull(property);
        Assert.Equal(expectedMaxLength, property!.GetMaxLength());
    }

    [Theory]
    [InlineData(nameof(TaskItem.ProjectId))]
    [InlineData(nameof(TaskItem.Title))]
    [InlineData(nameof(TaskItem.Status))]
    [InlineData(nameof(TaskItem.Priority))]
    [InlineData(nameof(TaskItem.CreatedAtUtc))]
    [InlineData(nameof(TaskItem.CreatedBy))]
    [InlineData(nameof(TaskItem.LastModifiedAtUtc))]
    [InlineData(nameof(TaskItem.LastModifiedBy))]
    public void TaskItem_RequiredProperty_IsNotNullable(string propertyName)
    {
        var entityType = GetTaskItemEntityType();

        var property = entityType.FindProperty(propertyName);

        Assert.NotNull(property);
        Assert.False(property!.IsNullable);
    }

    // TASK-013: assignment happens after creation, so AssignedUserId must not be required - unlike
    // Client/Project's OwnerUserId.
    [Theory]
    [InlineData(nameof(TaskItem.Description))]
    [InlineData(nameof(TaskItem.AssignedUserId))]
    [InlineData(nameof(TaskItem.StartDateUtc))]
    [InlineData(nameof(TaskItem.DueDateUtc))]
    [InlineData(nameof(TaskItem.CompletedAtUtc))]
    [InlineData(nameof(TaskItem.Notes))]
    public void TaskItem_OptionalProperty_IsNullable(string propertyName)
    {
        var entityType = GetTaskItemEntityType();

        var property = entityType.FindProperty(propertyName);

        Assert.NotNull(property);
        Assert.True(property!.IsNullable);
    }

    [Theory]
    [InlineData(nameof(TaskItem.Status))]
    [InlineData(nameof(TaskItem.Priority))]
    public void TaskItem_EnumProperty_IsStoredAsBoundedString(string propertyName)
    {
        var entityType = GetTaskItemEntityType();

        var property = entityType.FindProperty(propertyName);

        Assert.NotNull(property);
        Assert.Equal(typeof(string), property!.GetProviderClrType());
        Assert.Equal(20, property.GetMaxLength());
    }

    [Theory]
    [InlineData(nameof(TaskItem.StartDateUtc))]
    [InlineData(nameof(TaskItem.DueDateUtc))]
    [InlineData(nameof(TaskItem.CompletedAtUtc))]
    [InlineData(nameof(TaskItem.CreatedAtUtc))]
    [InlineData(nameof(TaskItem.LastModifiedAtUtc))]
    public void TaskItem_UtcTimestampProperty_UsesDateTime2WithMillisecondPrecision(string propertyName)
    {
        var entityType = GetTaskItemEntityType();

        var property = entityType.FindProperty(propertyName);

        Assert.NotNull(property);
        Assert.Equal("datetime2(3)", property!.GetColumnType());
    }

    [Fact]
    public void TaskItem_RowVersion_IsConfiguredAsConcurrencyToken()
    {
        var entityType = GetTaskItemEntityType();

        var property = entityType.FindProperty(nameof(TaskItem.RowVersion));

        Assert.NotNull(property);
        Assert.True(property!.IsConcurrencyToken);
        Assert.Equal(ValueGenerated.OnAddOrUpdate, property.ValueGenerated);
    }

    // DATA-003/DATA-004: a Task cannot exist without a Project, enforced at the database layer via
    // a single required foreign key with no cascade delete (non-destructive; DATA-020).
    [Fact]
    public void TaskItem_HasExactlyOneForeignKey_ToProject()
    {
        var entityType = GetTaskItemEntityType();

        var foreignKey = Assert.Single(entityType.GetForeignKeys());

        Assert.Equal(nameof(Project), foreignKey.PrincipalEntityType.ClrType.Name);
        Assert.Equal([nameof(TaskItem.ProjectId)], foreignKey.Properties.Select(p => p.Name));
    }

    [Fact]
    public void TaskItem_ProjectForeignKey_IsRequired()
    {
        var entityType = GetTaskItemEntityType();

        var foreignKey = Assert.Single(entityType.GetForeignKeys());

        Assert.True(foreignKey.IsRequired);
    }

    // CONSTRAINT: protect historical Task data from cascading physical deletion when a Project is
    // deleted.
    [Fact]
    public void TaskItem_ProjectForeignKey_DoesNotCascadeDelete()
    {
        var entityType = GetTaskItemEntityType();

        var foreignKey = Assert.Single(entityType.GetForeignKeys());

        Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior);
    }

    [Fact]
    public void TaskItem_ProjectForeignKey_HasNoNavigationProperties()
    {
        var entityType = GetTaskItemEntityType();

        var foreignKey = Assert.Single(entityType.GetForeignKeys());

        Assert.Null(foreignKey.DependentToPrincipal);
        Assert.Null(foreignKey.PrincipalToDependent);
    }

    [Theory]
    [InlineData("IX_Tasks_ProjectId", nameof(TaskItem.ProjectId))]
    [InlineData("IX_Tasks_Status", nameof(TaskItem.Status))]
    [InlineData("IX_Tasks_Priority", nameof(TaskItem.Priority))]
    [InlineData("IX_Tasks_AssignedUserId", nameof(TaskItem.AssignedUserId))]
    [InlineData("IX_Tasks_DueDateUtc", nameof(TaskItem.DueDateUtc))]
    public void TaskItem_HasExpectedSingleColumnIndex(string indexName, string propertyName)
    {
        var entityType = GetTaskItemEntityType();

        var index = entityType.GetIndexes().SingleOrDefault(i => i.GetDatabaseName() == indexName);

        Assert.NotNull(index);
        Assert.Equal([propertyName], index!.Properties.Select(p => p.Name));
    }

    [Fact]
    public void TaskItem_HasNoIndexOnCompletedAtUtc()
    {
        var entityType = GetTaskItemEntityType();

        var index = entityType.GetIndexes()
            .SingleOrDefault(i => i.Properties.Select(p => p.Name).SequenceEqual([nameof(TaskItem.CompletedAtUtc)]));

        Assert.Null(index);
    }

    [Fact]
    public void CrmDbContext_HasNoDbSet_ForTaskItem()
    {
        var taskItemProperty = typeof(CrmDbContext).GetProperties()
            .SingleOrDefault(p => p.PropertyType == typeof(DbSet<TaskItem>));

        Assert.Null(taskItemProperty);
    }
}

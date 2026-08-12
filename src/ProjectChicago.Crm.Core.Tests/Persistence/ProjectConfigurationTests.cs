using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using ProjectChicago.Crm.Core.Models.DataModels.Entities;
using ProjectChicago.Crm.Core.Persistence;
using Xunit;

namespace ProjectChicago.Crm.Core.Tests.Persistence;

// Model-metadata tests for ProjectConfiguration (PROJECT-001..023, DATA-002..005; database.md).
// Builds the SQL Server model without opening a connection - no query/save executes against a
// real database. Looks up the entity type via the model directly (rather than the Projects DbSet)
// since these tests target mapping metadata, not query/save behavior.
public class ProjectConfigurationTests
{
    private static IEntityType GetProjectEntityType()
    {
        var options = new DbContextOptionsBuilder<CrmDbContext>()
            .UseSqlServer("Server=.;Database=ProjectConfigurationTests;")
            .Options;

        using var context = new CrmDbContext(options);

        var entityType = context.Model.FindEntityType(typeof(Project));
        Assert.NotNull(entityType);

        return entityType!;
    }

    [Fact]
    public void Project_MapsToProjectsTable()
    {
        var entityType = GetProjectEntityType();

        Assert.Equal("Projects", entityType.GetTableName());
    }

    [Fact]
    public void Project_PrimaryKeyIsId()
    {
        var entityType = GetProjectEntityType();

        var primaryKey = entityType.FindPrimaryKey();

        Assert.NotNull(primaryKey);
        Assert.Equal([nameof(Project.Id)], primaryKey!.Properties.Select(p => p.Name));
    }

    [Fact]
    public void Project_Id_IsNeverDatabaseGenerated()
    {
        var entityType = GetProjectEntityType();

        var idProperty = entityType.FindProperty(nameof(Project.Id));

        Assert.NotNull(idProperty);
        Assert.Equal(ValueGenerated.Never, idProperty!.ValueGenerated);
        Assert.Equal("uniqueidentifier", idProperty.GetColumnType());
    }

    [Theory]
    [InlineData(nameof(Project.Name), 200)]
    [InlineData(nameof(Project.Description), 2000)]
    [InlineData(nameof(Project.OwnerUserId), 128)]
    [InlineData(nameof(Project.Notes), 2000)]
    [InlineData(nameof(Project.CreatedBy), 128)]
    [InlineData(nameof(Project.LastModifiedBy), 128)]
    public void Project_BoundedStringProperty_HasExpectedMaxLength(string propertyName, int expectedMaxLength)
    {
        var entityType = GetProjectEntityType();

        var property = entityType.FindProperty(propertyName);

        Assert.NotNull(property);
        Assert.Equal(expectedMaxLength, property!.GetMaxLength());
    }

    [Theory]
    [InlineData(nameof(Project.ClientId))]
    [InlineData(nameof(Project.Name))]
    [InlineData(nameof(Project.Status))]
    [InlineData(nameof(Project.Priority))]
    [InlineData(nameof(Project.OwnerUserId))]
    [InlineData(nameof(Project.CreatedAtUtc))]
    [InlineData(nameof(Project.CreatedBy))]
    [InlineData(nameof(Project.LastModifiedAtUtc))]
    [InlineData(nameof(Project.LastModifiedBy))]
    public void Project_RequiredProperty_IsNotNullable(string propertyName)
    {
        var entityType = GetProjectEntityType();

        var property = entityType.FindProperty(propertyName);

        Assert.NotNull(property);
        Assert.False(property!.IsNullable);
    }

    [Theory]
    [InlineData(nameof(Project.Description))]
    [InlineData(nameof(Project.StartDateUtc))]
    [InlineData(nameof(Project.TargetCompletionDateUtc))]
    [InlineData(nameof(Project.ActualCompletionDateUtc))]
    [InlineData(nameof(Project.Notes))]
    public void Project_OptionalProperty_IsNullable(string propertyName)
    {
        var entityType = GetProjectEntityType();

        var property = entityType.FindProperty(propertyName);

        Assert.NotNull(property);
        Assert.True(property!.IsNullable);
    }

    [Theory]
    [InlineData(nameof(Project.Status))]
    [InlineData(nameof(Project.Priority))]
    public void Project_EnumProperty_IsStoredAsBoundedString(string propertyName)
    {
        var entityType = GetProjectEntityType();

        var property = entityType.FindProperty(propertyName);

        Assert.NotNull(property);
        Assert.Equal(typeof(string), property!.GetProviderClrType());
        Assert.Equal(20, property.GetMaxLength());
    }

    [Theory]
    [InlineData(nameof(Project.StartDateUtc))]
    [InlineData(nameof(Project.TargetCompletionDateUtc))]
    [InlineData(nameof(Project.ActualCompletionDateUtc))]
    [InlineData(nameof(Project.CreatedAtUtc))]
    [InlineData(nameof(Project.LastModifiedAtUtc))]
    public void Project_UtcTimestampProperty_UsesDateTime2WithMillisecondPrecision(string propertyName)
    {
        var entityType = GetProjectEntityType();

        var property = entityType.FindProperty(propertyName);

        Assert.NotNull(property);
        Assert.Equal("datetime2(3)", property!.GetColumnType());
    }

    [Fact]
    public void Project_RowVersion_IsConfiguredAsConcurrencyToken()
    {
        var entityType = GetProjectEntityType();

        var property = entityType.FindProperty(nameof(Project.RowVersion));

        Assert.NotNull(property);
        Assert.True(property!.IsConcurrencyToken);
        Assert.Equal(ValueGenerated.OnAddOrUpdate, property.ValueGenerated);
    }

    // DATA-002/DATA-004: a Project cannot exist without a Client, enforced at the database layer
    // via a single required foreign key with no cascade delete (non-destructive; PROJECT-014).
    [Fact]
    public void Project_HasExactlyOneForeignKey_ToClient()
    {
        var entityType = GetProjectEntityType();

        var foreignKey = Assert.Single(entityType.GetForeignKeys());

        Assert.Equal(nameof(Client), foreignKey.PrincipalEntityType.ClrType.Name);
        Assert.Equal([nameof(Project.ClientId)], foreignKey.Properties.Select(p => p.Name));
    }

    [Fact]
    public void Project_ClientForeignKey_IsRequired()
    {
        var entityType = GetProjectEntityType();

        var foreignKey = Assert.Single(entityType.GetForeignKeys());

        Assert.True(foreignKey.IsRequired);
    }

    [Fact]
    public void Project_ClientForeignKey_DoesNotCascadeDelete()
    {
        var entityType = GetProjectEntityType();

        var foreignKey = Assert.Single(entityType.GetForeignKeys());

        Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior);
    }

    [Fact]
    public void Project_ClientForeignKey_HasNoNavigationProperties()
    {
        var entityType = GetProjectEntityType();

        var foreignKey = Assert.Single(entityType.GetForeignKeys());

        Assert.Null(foreignKey.DependentToPrincipal);
        Assert.Null(foreignKey.PrincipalToDependent);
    }

    [Theory]
    [InlineData("IX_Projects_ClientId", nameof(Project.ClientId))]
    [InlineData("IX_Projects_Status", nameof(Project.Status))]
    [InlineData("IX_Projects_OwnerUserId", nameof(Project.OwnerUserId))]
    [InlineData("IX_Projects_Priority", nameof(Project.Priority))]
    [InlineData("IX_Projects_StartDateUtc", nameof(Project.StartDateUtc))]
    [InlineData("IX_Projects_TargetCompletionDateUtc", nameof(Project.TargetCompletionDateUtc))]
    public void Project_HasExpectedSingleColumnIndex(string indexName, string propertyName)
    {
        var entityType = GetProjectEntityType();

        var index = entityType.GetIndexes().SingleOrDefault(i => i.GetDatabaseName() == indexName);

        Assert.NotNull(index);
        Assert.Equal([propertyName], index!.Properties.Select(p => p.Name));
    }

    [Fact]
    public void Project_HasNoIndexOnActualCompletionDate_FromProject021()
    {
        var entityType = GetProjectEntityType();

        var index = entityType.GetIndexes()
            .SingleOrDefault(i => i.Properties.Select(p => p.Name).SequenceEqual([nameof(Project.ActualCompletionDateUtc)]));

        Assert.Null(index);
    }
}

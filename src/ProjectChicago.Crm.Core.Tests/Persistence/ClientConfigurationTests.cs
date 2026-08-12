using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using ProjectChicago.Crm.Core.Models.DataModels.Entities;
using ProjectChicago.Crm.Core.Persistence;
using Xunit;

namespace ProjectChicago.Crm.Core.Tests.Persistence;

// Model-metadata tests for ClientConfiguration (CLIENT-002..004, DATA-004..008; database.md).
// Builds the SQL Server model without opening a connection - no query/save executes against a
// real database. Client has no DbSet yet, so entity-type lookup goes through the model directly.
public class ClientConfigurationTests
{
    private static IEntityType GetClientEntityType()
    {
        var options = new DbContextOptionsBuilder<CrmDbContext>()
            .UseSqlServer("Server=.;Database=ClientConfigurationTests;")
            .Options;

        using var context = new CrmDbContext(options);

        var entityType = context.Model.FindEntityType(typeof(Client));
        Assert.NotNull(entityType);

        return entityType!;
    }

    [Fact]
    public void Client_MapsToClientsTable()
    {
        var entityType = GetClientEntityType();

        Assert.Equal("Clients", entityType.GetTableName());
    }

    [Fact]
    public void Client_PrimaryKeyIsId()
    {
        var entityType = GetClientEntityType();

        var primaryKey = entityType.FindPrimaryKey();

        Assert.NotNull(primaryKey);
        Assert.Equal([nameof(Client.Id)], primaryKey!.Properties.Select(p => p.Name));
    }

    [Fact]
    public void Client_Id_IsNeverDatabaseGenerated()
    {
        var entityType = GetClientEntityType();

        var idProperty = entityType.FindProperty(nameof(Client.Id));

        Assert.NotNull(idProperty);
        Assert.Equal(ValueGenerated.Never, idProperty!.ValueGenerated);
        Assert.Equal("uniqueidentifier", idProperty.GetColumnType());
    }

    [Theory]
    [InlineData(nameof(Client.Name), 200)]
    [InlineData(nameof(Client.PrimaryContactName), 200)]
    [InlineData(nameof(Client.PrimaryEmail), 320)]
    [InlineData(nameof(Client.PrimaryPhone), 32)]
    [InlineData(nameof(Client.Website), 2048)]
    [InlineData(nameof(Client.AddressLine), 300)]
    [InlineData(nameof(Client.City), 150)]
    [InlineData(nameof(Client.StateOrProvince), 150)]
    [InlineData(nameof(Client.PostalCode), 20)]
    [InlineData(nameof(Client.Country), 100)]
    [InlineData(nameof(Client.Description), 2000)]
    [InlineData(nameof(Client.OwnerUserId), 128)]
    [InlineData(nameof(Client.CreatedBy), 128)]
    [InlineData(nameof(Client.LastModifiedBy), 128)]
    public void Client_BoundedStringProperty_HasExpectedMaxLength(string propertyName, int expectedMaxLength)
    {
        var entityType = GetClientEntityType();

        var property = entityType.FindProperty(propertyName);

        Assert.NotNull(property);
        Assert.Equal(expectedMaxLength, property!.GetMaxLength());
    }

    [Theory]
    [InlineData(nameof(Client.Name))]
    [InlineData(nameof(Client.LifecycleStatus))]
    [InlineData(nameof(Client.OwnerUserId))]
    [InlineData(nameof(Client.CreatedAtUtc))]
    [InlineData(nameof(Client.CreatedBy))]
    [InlineData(nameof(Client.LastModifiedAtUtc))]
    [InlineData(nameof(Client.LastModifiedBy))]
    public void Client_RequiredProperty_IsNotNullable(string propertyName)
    {
        var entityType = GetClientEntityType();

        var property = entityType.FindProperty(propertyName);

        Assert.NotNull(property);
        Assert.False(property!.IsNullable);
    }

    [Theory]
    [InlineData(nameof(Client.PrimaryContactName))]
    [InlineData(nameof(Client.PrimaryEmail))]
    [InlineData(nameof(Client.PrimaryPhone))]
    [InlineData(nameof(Client.Website))]
    [InlineData(nameof(Client.AddressLine))]
    [InlineData(nameof(Client.City))]
    [InlineData(nameof(Client.StateOrProvince))]
    [InlineData(nameof(Client.PostalCode))]
    [InlineData(nameof(Client.Country))]
    [InlineData(nameof(Client.Description))]
    public void Client_OptionalProperty_IsNullable(string propertyName)
    {
        var entityType = GetClientEntityType();

        var property = entityType.FindProperty(propertyName);

        Assert.NotNull(property);
        Assert.True(property!.IsNullable);
    }

    [Fact]
    public void Client_LifecycleStatus_IsStoredAsBoundedString()
    {
        var entityType = GetClientEntityType();

        var property = entityType.FindProperty(nameof(Client.LifecycleStatus));

        Assert.NotNull(property);
        Assert.Equal(typeof(string), property!.GetProviderClrType());
        Assert.Equal(20, property.GetMaxLength());
    }

    [Theory]
    [InlineData(nameof(Client.CreatedAtUtc))]
    [InlineData(nameof(Client.LastModifiedAtUtc))]
    public void Client_UtcTimestampProperty_UsesDateTime2WithMillisecondPrecision(string propertyName)
    {
        var entityType = GetClientEntityType();

        var property = entityType.FindProperty(propertyName);

        Assert.NotNull(property);
        Assert.Equal("datetime2(3)", property!.GetColumnType());
    }

    [Fact]
    public void Client_RowVersion_IsConfiguredAsConcurrencyToken()
    {
        var entityType = GetClientEntityType();

        var property = entityType.FindProperty(nameof(Client.RowVersion));

        Assert.NotNull(property);
        Assert.True(property!.IsConcurrencyToken);
        Assert.Equal(ValueGenerated.OnAddOrUpdate, property.ValueGenerated);
    }

    [Fact]
    public void Client_HasNoForeignKeys()
    {
        var entityType = GetClientEntityType();

        Assert.Empty(entityType.GetForeignKeys());
    }

    [Theory]
    [InlineData("IX_Clients_Name", nameof(Client.Name))]
    [InlineData("IX_Clients_PrimaryEmail", nameof(Client.PrimaryEmail))]
    [InlineData("IX_Clients_PrimaryPhone", nameof(Client.PrimaryPhone))]
    public void Client_HasExpectedSingleColumnIndex(string indexName, string propertyName)
    {
        var entityType = GetClientEntityType();

        var index = entityType.GetIndexes().SingleOrDefault(i => i.GetDatabaseName() == indexName);

        Assert.NotNull(index);
        Assert.Equal([propertyName], index!.Properties.Select(p => p.Name));
    }

    [Fact]
    public void Client_HasFilteredIndex_ExcludingArchivedLifecycleStatus()
    {
        var entityType = GetClientEntityType();

        var index = entityType.GetIndexes()
            .SingleOrDefault(i => i.GetDatabaseName() == "IX_Clients_LifecycleStatus_ExcludingArchived");

        Assert.NotNull(index);
        Assert.Equal([nameof(Client.LifecycleStatus)], index!.Properties.Select(p => p.Name));
        Assert.Equal("[LifecycleStatus] <> N'Archived'", index.GetFilter());
    }
}

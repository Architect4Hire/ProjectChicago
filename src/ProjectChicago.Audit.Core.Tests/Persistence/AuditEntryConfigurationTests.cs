using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using ProjectChicago.Audit.Core.Persistence;
using Xunit;

namespace ProjectChicago.Audit.Core.Tests.Persistence;

/// <summary>
/// Model-metadata tests for AuditEntryConfiguration (ADR-0016, AUDIT-001..008, database.md).
/// Verifies SQL Server mapping, column types, indexes, uniqueness constraints, and append-only shape
/// without executing queries against a real database.
/// </summary>
public class AuditEntryConfigurationTests
{
    private static IEntityType GetAuditEntryEntityType()
    {
        var options = new DbContextOptionsBuilder<AuditDbContext>()
            .UseSqlServer("Server=.;Database=AuditEntryConfigurationTests;")
            .Options;

        using var context = new AuditDbContext(options);

        var entityType = context.Model.FindEntityType(typeof(AuditEntry));
        Assert.NotNull(entityType);

        return entityType!;
    }

    // Table mapping and schema

    [Fact]
    public void AuditEntry_MapsToAuditEntriesTableInAuditSchema()
    {
        var entityType = GetAuditEntryEntityType();

        Assert.Equal("AuditEntries", entityType.GetTableName());
        Assert.Equal("audit", entityType.GetSchema());
    }

    // Primary key

    [Fact]
    public void AuditEntry_PrimaryKeyIsAuditEntryId()
    {
        var entityType = GetAuditEntryEntityType();

        var primaryKey = entityType.FindPrimaryKey();

        Assert.NotNull(primaryKey);
        Assert.Equal([nameof(AuditEntry.AuditEntryId)], primaryKey!.Properties.Select(p => p.Name));
    }

    [Fact]
    public void AuditEntry_AuditEntryId_IsUniqueIdentifier()
    {
        var entityType = GetAuditEntryEntityType();

        var property = entityType.FindProperty(nameof(AuditEntry.AuditEntryId));

        Assert.NotNull(property);
        Assert.Equal("uniqueidentifier", property!.GetColumnType());
    }

    [Fact]
    public void AuditEntry_AuditEntryId_HasDefaultValueSql()
    {
        var entityType = GetAuditEntryEntityType();

        var property = entityType.FindProperty(nameof(AuditEntry.AuditEntryId));

        Assert.NotNull(property);
        Assert.Equal("newid()", property!.GetDefaultValueSql());
    }

    // Unique constraint on EventId for idempotency (ASYNC-005, AUDIT-004)

    [Fact]
    public void AuditEntry_EventId_HasUniqueAlternateKey()
    {
        var entityType = GetAuditEntryEntityType();

        var alternateKeys = entityType.GetKeys().Where(k => !k.IsPrimaryKey()).ToList();

        Assert.Single(alternateKeys);
        var eventIdKey = alternateKeys.First();
        Assert.Equal([nameof(AuditEntry.EventId)], eventIdKey.Properties.Select(p => p.Name));
        Assert.Equal("AK_AuditEntries_EventId", eventIdKey.GetName());
    }

    // SQL Server column types for required string fields

    [Theory]
    [InlineData(nameof(AuditEntry.EventId), "nvarchar(256)")]
    [InlineData(nameof(AuditEntry.EntityType), "nvarchar(64)")]
    [InlineData(nameof(AuditEntry.Action), "nvarchar(64)")]
    [InlineData(nameof(AuditEntry.ActionCategory), "nvarchar(32)")]
    [InlineData(nameof(AuditEntry.ActorType), "nvarchar(32)")]
    [InlineData(nameof(AuditEntry.SourceService), "nvarchar(64)")]
    [InlineData(nameof(AuditEntry.SourceEventType), "nvarchar(128)")]
    [InlineData(nameof(AuditEntry.TraceId), "nvarchar(64)")]
    [InlineData(nameof(AuditEntry.CorrelationId), "nvarchar(256)")]
    public void AuditEntry_RequiredStringProperty_HasCorrectColumnType(string propertyName, string expectedColumnType)
    {
        var entityType = GetAuditEntryEntityType();

        var property = entityType.FindProperty(propertyName);

        Assert.NotNull(property);
        Assert.Equal(expectedColumnType, property!.GetColumnType());
    }

    // Nullable string fields

    [Theory]
    [InlineData(nameof(AuditEntry.ActorDisplayName), "nvarchar(256)")]
    [InlineData(nameof(AuditEntry.CausationId), "nvarchar(256)")]
    public void AuditEntry_OptionalStringProperty_IsNullable(string propertyName, string expectedColumnType)
    {
        var entityType = GetAuditEntryEntityType();

        var property = entityType.FindProperty(propertyName);

        Assert.NotNull(property);
        Assert.True(property!.IsNullable);
        Assert.Equal(expectedColumnType, property.GetColumnType());
    }

    // JSON/max-length fields

    [Theory]
    [InlineData(nameof(AuditEntry.ChangedFields))]
    [InlineData(nameof(AuditEntry.RawEventPayload))]
    public void AuditEntry_RequiredJsonField_UsesNvarcharMax(string propertyName)
    {
        var entityType = GetAuditEntryEntityType();

        var property = entityType.FindProperty(propertyName);

        Assert.NotNull(property);
        Assert.Equal("nvarchar(max)", property!.GetColumnType());
        Assert.False(property.IsNullable);
    }

    [Theory]
    [InlineData(nameof(AuditEntry.PreviousValues))]
    [InlineData(nameof(AuditEntry.NewValues))]
    [InlineData(nameof(AuditEntry.SummaryDescription))]
    public void AuditEntry_OptionalJsonField_UsesNvarcharMaxAndNullable(string propertyName)
    {
        var entityType = GetAuditEntryEntityType();

        var property = entityType.FindProperty(propertyName);

        Assert.NotNull(property);
        Assert.Equal("nvarchar(max)", property!.GetColumnType());
        Assert.True(property.IsNullable);
    }

    // GUID and DateTime columns

    [Fact]
    public void AuditEntry_EntityId_IsUniqueIdentifier()
    {
        var entityType = GetAuditEntryEntityType();

        var property = entityType.FindProperty(nameof(AuditEntry.EntityId));

        Assert.NotNull(property);
        Assert.Equal("uniqueidentifier", property!.GetColumnType());
        Assert.False(property.IsNullable);
    }

    [Fact]
    public void AuditEntry_ActorUserId_IsUniqueIdentifierAndNullable()
    {
        var entityType = GetAuditEntryEntityType();

        var property = entityType.FindProperty(nameof(AuditEntry.ActorUserId));

        Assert.NotNull(property);
        Assert.Equal("uniqueidentifier", property!.GetColumnType());
        Assert.True(property.IsNullable);
    }

    [Theory]
    [InlineData(nameof(AuditEntry.OccurredAtUtc))]
    [InlineData(nameof(AuditEntry.AuditedAtUtc))]
    public void AuditEntry_UtcTimestampProperty_UsesDateTime2(string propertyName)
    {
        var entityType = GetAuditEntryEntityType();

        var property = entityType.FindProperty(propertyName);

        Assert.NotNull(property);
        Assert.Equal("datetime2", property!.GetColumnType());
        Assert.False(property.IsNullable);
    }

    // Optimistic concurrency (rowversion)

    [Fact]
    public void AuditEntry_RowVersion_IsConfiguredAsConcurrencyToken()
    {
        var entityType = GetAuditEntryEntityType();

        var property = entityType.FindProperty(nameof(AuditEntry.RowVersion));

        Assert.NotNull(property);
        Assert.Equal("rowversion", property!.GetColumnType());
        Assert.True(property.IsNullable);
        Assert.Equal(ValueGenerated.OnAddOrUpdate, property.ValueGenerated);
    }

    // Indexes for query performance

    [Fact]
    public void AuditEntry_HasIndexForEntityTypeAndId()
    {
        var entityType = GetAuditEntryEntityType();

        var indexes = entityType.GetIndexes().ToList();
        var index = indexes.FirstOrDefault(i =>
            i.Properties.Select(p => p.Name).SequenceEqual([
                nameof(AuditEntry.EntityType),
                nameof(AuditEntry.EntityId),
                nameof(AuditEntry.OccurredAtUtc)
            ]));

        Assert.NotNull(index);
        Assert.Equal("IX_AuditEntries_EntityTypeId_OccurredAt", index!.GetDatabaseName());
    }

    [Fact]
    public void AuditEntry_HasIndexForOccurredAt()
    {
        var entityType = GetAuditEntryEntityType();

        var indexes = entityType.GetIndexes().ToList();
        var index = indexes.FirstOrDefault(i =>
            i.Properties.Select(p => p.Name).SequenceEqual([nameof(AuditEntry.OccurredAtUtc)]));

        Assert.NotNull(index);
        Assert.Equal("IX_AuditEntries_OccurredAt", index!.GetDatabaseName());
    }

    [Fact]
    public void AuditEntry_HasIndexForTraceId()
    {
        var entityType = GetAuditEntryEntityType();

        var indexes = entityType.GetIndexes().ToList();
        var index = indexes.FirstOrDefault(i =>
            i.Properties.Select(p => p.Name).SequenceEqual([nameof(AuditEntry.TraceId)]));

        Assert.NotNull(index);
        Assert.Equal("IX_AuditEntries_TraceId", index!.GetDatabaseName());
    }

    [Fact]
    public void AuditEntry_HasIndexForCorrelationId()
    {
        var entityType = GetAuditEntryEntityType();

        var indexes = entityType.GetIndexes().ToList();
        var index = indexes.FirstOrDefault(i =>
            i.Properties.Select(p => p.Name).SequenceEqual([nameof(AuditEntry.CorrelationId)]));

        Assert.NotNull(index);
        Assert.Equal("IX_AuditEntries_CorrelationId", index!.GetDatabaseName());
    }

    [Fact]
    public void AuditEntry_HasIndexForActorAndAuditedAt()
    {
        var entityType = GetAuditEntryEntityType();

        var indexes = entityType.GetIndexes().ToList();
        var index = indexes.FirstOrDefault(i =>
            i.Properties.Select(p => p.Name).SequenceEqual([
                nameof(AuditEntry.ActorUserId),
                nameof(AuditEntry.AuditedAtUtc)
            ]));

        Assert.NotNull(index);
        Assert.Equal("IX_AuditEntries_Actor_AuditedAt", index!.GetDatabaseName());
    }

    [Fact]
    public void AuditEntry_HasIndexForServiceActionAndAuditedAt()
    {
        var entityType = GetAuditEntryEntityType();

        var indexes = entityType.GetIndexes().ToList();
        var index = indexes.FirstOrDefault(i =>
            i.Properties.Select(p => p.Name).SequenceEqual([
                nameof(AuditEntry.SourceService),
                nameof(AuditEntry.Action),
                nameof(AuditEntry.AuditedAtUtc)
            ]));

        Assert.NotNull(index);
        Assert.Equal("IX_AuditEntries_Service_Action_AuditedAt", index!.GetDatabaseName());
    }

    // Required fields

    [Theory]
    [InlineData(nameof(AuditEntry.EventId))]
    [InlineData(nameof(AuditEntry.EntityType))]
    [InlineData(nameof(AuditEntry.EntityId))]
    [InlineData(nameof(AuditEntry.Action))]
    [InlineData(nameof(AuditEntry.ActionCategory))]
    [InlineData(nameof(AuditEntry.ActorType))]
    [InlineData(nameof(AuditEntry.SourceService))]
    [InlineData(nameof(AuditEntry.SourceEventType))]
    [InlineData(nameof(AuditEntry.OccurredAtUtc))]
    [InlineData(nameof(AuditEntry.AuditedAtUtc))]
    [InlineData(nameof(AuditEntry.TraceId))]
    [InlineData(nameof(AuditEntry.CorrelationId))]
    [InlineData(nameof(AuditEntry.ChangedFields))]
    [InlineData(nameof(AuditEntry.RawEventPayload))]
    public void AuditEntry_RequiredProperty_IsNotNullable(string propertyName)
    {
        var entityType = GetAuditEntryEntityType();

        var property = entityType.FindProperty(propertyName);

        Assert.NotNull(property);
        Assert.False(property!.IsNullable, $"{propertyName} should not be nullable");
    }

    // Append-only verification: no navigation properties or relationships that would enable direct updates/deletes

    [Fact]
    public void AuditEntry_HasNoNavigationPropertiesForRelationships()
    {
        var entityType = GetAuditEntryEntityType();

        var navigations = entityType.GetNavigations().ToList();

        Assert.Empty(navigations);
    }

    [Fact]
    public void AuditEntry_HasNoForeignKeys()
    {
        var entityType = GetAuditEntryEntityType();

        var foreignKeys = entityType.GetForeignKeys().ToList();

        Assert.Empty(foreignKeys);
    }
}

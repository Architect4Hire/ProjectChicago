using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using ProjectChicago.Shared.Outbox;
using Xunit;

namespace ProjectChicago.Shared.Tests;

// Verifies EF model metadata only, via a bare ModelBuilder - no DbContext, no database connection,
// no provider package. Confirms the mapping is SQL Server-compatible (no PostgreSQL-specific types).
public class OutboxMessageConfigurationTests
{
    // Reads the mutable (pre-finalization) model directly rather than calling FinalizeModel(): column
    // type/converter/default-value lookups on IReadOnlyProperty resolve from the annotations this
    // configuration sets explicitly, with no EF provider (SqlServer/InMemory/etc.) required. Finalizing
    // would require a provider's type-mapping source purely to satisfy the API, which is unrelated to
    // what these tests assert.
    private static IMutableEntityType BuildEntityType()
    {
        var modelBuilder = new ModelBuilder();
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());
        return modelBuilder.Model.FindEntityType(typeof(OutboxMessage))
            ?? throw new InvalidOperationException("OutboxMessage entity type was not registered.");
    }

    [Fact]
    public void MapsToOutboxMessagesTable()
    {
        var entityType = BuildEntityType();

        Assert.Equal("OutboxMessages", entityType.GetTableName());
    }

    [Fact]
    public void PrimaryKey_IsId_WithUniqueidentifierColumnType_AndNoDatabaseGeneration()
    {
        var entityType = BuildEntityType();

        var key = entityType.FindPrimaryKey();
        Assert.NotNull(key);
        Assert.Equal(["Id"], key!.Properties.Select(p => p.Name));

        var id = entityType.FindProperty(nameof(OutboxMessage.Id))!;
        Assert.Equal("uniqueidentifier", id.GetColumnType());
        Assert.Equal(ValueGenerated.Never, id.ValueGenerated);
    }

    [Fact]
    public void Payload_IsRequired_AndUnbounded()
    {
        var payload = BuildEntityType().FindProperty(nameof(OutboxMessage.Payload))!;

        Assert.False(payload.IsNullable);
        Assert.Equal("nvarchar(max)", payload.GetColumnType());
    }

    [Fact]
    public void CorrelationId_IsRequired_CausationId_IsOptional()
    {
        var entityType = BuildEntityType();

        Assert.False(entityType.FindProperty(nameof(OutboxMessage.CorrelationId))!.IsNullable);
        Assert.True(entityType.FindProperty(nameof(OutboxMessage.CausationId))!.IsNullable);
    }

    [Fact]
    public void Status_IsStoredAsString_AndDefaultsToPending()
    {
        var status = BuildEntityType().FindProperty(nameof(OutboxMessage.Status))!;

        Assert.False(status.IsNullable);
        Assert.Equal(typeof(string), status.GetProviderClrType());
    }

    [Fact]
    public void AttemptCount_DefaultsToZero()
    {
        var attemptCount = BuildEntityType().FindProperty(nameof(OutboxMessage.AttemptCount))!;

        Assert.Equal(0, attemptCount.GetDefaultValue());
    }

    [Fact]
    public void RowVersion_IsConcurrencyTokenGeneratedOnAddOrUpdate()
    {
        var rowVersion = BuildEntityType().FindProperty(nameof(OutboxMessage.RowVersion))!;

        Assert.True(rowVersion.IsConcurrencyToken);
        Assert.Equal(ValueGenerated.OnAddOrUpdate, rowVersion.ValueGenerated);
    }

    [Fact]
    public void Index_OnStatusAndCreatedAtUtc_ExistsForRelayBatchSelection()
    {
        var entityType = BuildEntityType();

        var index = entityType.FindIndex(
        [
            entityType.FindProperty(nameof(OutboxMessage.Status))!,
            entityType.FindProperty(nameof(OutboxMessage.CreatedAtUtc))!,
        ]);

        Assert.NotNull(index);
        Assert.Equal("IX_OutboxMessages_Status_CreatedAtUtc", index!.GetDatabaseName());
    }

    [Theory]
    [InlineData(nameof(OutboxMessage.OccurredAtUtc))]
    [InlineData(nameof(OutboxMessage.CreatedAtUtc))]
    [InlineData(nameof(OutboxMessage.DispatchedAtUtc))]
    [InlineData(nameof(OutboxMessage.LastAttemptAtUtc))]
    [InlineData(nameof(OutboxMessage.LeasedUntilUtc))]
    public void TimestampColumns_UseDatetime2WithConsistentPrecision(string propertyName)
    {
        var property = BuildEntityType().FindProperty(propertyName)!;

        Assert.Equal("datetime2(3)", property.GetColumnType());
    }
}

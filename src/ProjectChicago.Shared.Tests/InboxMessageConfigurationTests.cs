using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using ProjectChicago.Shared.Inbox;
using Xunit;

namespace ProjectChicago.Shared.Tests;

// Verifies EF model metadata only, via a bare ModelBuilder - no DbContext, no database connection,
// no provider package. Confirms the mapping is SQL Server-compatible (no PostgreSQL-specific types).
public class InboxMessageConfigurationTests
{
    // Reads the mutable (pre-finalization) model directly rather than calling FinalizeModel(): column
    // type/converter/default-value lookups on IReadOnlyProperty resolve from the annotations this
    // configuration sets explicitly, with no EF provider (SqlServer/InMemory/etc.) required.
    private static IMutableEntityType BuildEntityType()
    {
        var modelBuilder = new ModelBuilder();
        modelBuilder.ApplyConfiguration(new InboxMessageConfiguration());
        return modelBuilder.Model.FindEntityType(typeof(InboxMessage))
            ?? throw new InvalidOperationException("InboxMessage entity type was not registered.");
    }

    [Fact]
    public void MapsToInboxMessagesTable()
    {
        var entityType = BuildEntityType();

        Assert.Equal("InboxMessages", entityType.GetTableName());
    }

    [Fact]
    public void MessageId_IsTheExplicitIdempotencyKey_AsThePrimaryKey_WithNoDatabaseGeneration()
    {
        var entityType = BuildEntityType();

        var key = entityType.FindPrimaryKey();
        Assert.NotNull(key);
        Assert.Equal([nameof(InboxMessage.MessageId)], key!.Properties.Select(p => p.Name));

        var messageId = entityType.FindProperty(nameof(InboxMessage.MessageId))!;
        Assert.False(messageId.IsNullable);
        Assert.Equal(ValueGenerated.Never, messageId.ValueGenerated);
    }

    [Fact]
    public void NoSeparateUniqueIndexOnMessageId_BecauseThePrimaryKeyAlreadyEnforcesUniqueness()
    {
        var entityType = BuildEntityType();
        var messageId = entityType.FindProperty(nameof(InboxMessage.MessageId))!;

        var indexes = entityType.GetIndexes().Where(i => i.Properties.Contains(messageId));

        Assert.Empty(indexes);
    }

    [Fact]
    public void CorrelationId_IsRequired_CausationId_IsOptional()
    {
        var entityType = BuildEntityType();

        Assert.False(entityType.FindProperty(nameof(InboxMessage.CorrelationId))!.IsNullable);
        Assert.True(entityType.FindProperty(nameof(InboxMessage.CausationId))!.IsNullable);
    }

    [Fact]
    public void Status_IsStoredAsString_AndDefaultsToReceived()
    {
        var entityType = BuildEntityType();
        var status = entityType.FindProperty(nameof(InboxMessage.Status))!;

        Assert.False(status.IsNullable);
        Assert.Equal(typeof(string), status.GetProviderClrType());

        var clrDefault = new InboxMessage
        {
            MessageId = "test",
            ContractType = "Test",
            ContractVersion = 1,
            CorrelationId = "corr",
            TraceId = "trace",
            ReceivedAtUtc = default,
        };
        Assert.Equal(InboxMessageStatus.Received, clrDefault.Status);
    }

    [Fact]
    public void AttemptCount_DefaultsToZero()
    {
        var attemptCount = BuildEntityType().FindProperty(nameof(InboxMessage.AttemptCount))!;

        Assert.Equal(0, attemptCount.GetDefaultValue());
    }

    [Fact]
    public void RowVersion_IsConcurrencyTokenGeneratedOnAddOrUpdate()
    {
        var rowVersion = BuildEntityType().FindProperty(nameof(InboxMessage.RowVersion))!;

        Assert.True(rowVersion.IsConcurrencyToken);
        Assert.Equal(ValueGenerated.OnAddOrUpdate, rowVersion.ValueGenerated);
    }

    [Fact]
    public void Index_OnStatusAndLeasedUntilUtc_ExistsForStaleRecoveryAndDeadLetterVisibility()
    {
        var entityType = BuildEntityType();

        var index = entityType.FindIndex(
        [
            entityType.FindProperty(nameof(InboxMessage.Status))!,
            entityType.FindProperty(nameof(InboxMessage.LeasedUntilUtc))!,
        ]);

        Assert.NotNull(index);
        Assert.Equal("IX_InboxMessages_Status_LeasedUntilUtc", index!.GetDatabaseName());
    }

    [Theory]
    [InlineData(nameof(InboxMessage.ReceivedAtUtc))]
    [InlineData(nameof(InboxMessage.ProcessingStartedAtUtc))]
    [InlineData(nameof(InboxMessage.ProcessingCompletedAtUtc))]
    [InlineData(nameof(InboxMessage.LastAttemptAtUtc))]
    [InlineData(nameof(InboxMessage.LeasedUntilUtc))]
    public void TimestampColumns_UseDatetime2WithConsistentPrecision(string propertyName)
    {
        var property = BuildEntityType().FindProperty(propertyName)!;

        Assert.Equal("datetime2(3)", property.GetColumnType());
    }
}

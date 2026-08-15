using Microsoft.EntityFrameworkCore;
using ProjectChicago.Audit.Core.Persistence;
using ProjectChicago.Shared.Inbox;
using Xunit;

namespace ProjectChicago.Audit.Core.Tests;

/// <summary>
/// Tests for AuditDbContext model structure, mapping, and append-only shape (ADR-0016, AUDIT-001..008, ASYNC-005).
/// Verifies that AuditEntry and InboxMessage are properly mapped, with no unwanted foreign key
/// relationships that would enable cross-service access or permit cascading behavior.
/// </summary>
public class AuditDbContextTests
{
    private static AuditDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AuditDbContext>()
            .UseSqlServer("Server=.;Database=AuditDbContextTests;")
            .Options;

        return new AuditDbContext(options);
    }

    // AuditEntry tests

    [Fact]
    public void DbContext_HasAuditEntriesDbSet()
    {
        using var context = CreateContext();

        // DbSet should exist and be queryable (though we don't execute the query).
        var auditEntriesSet = context.AuditEntries;

        Assert.NotNull(auditEntriesSet);
    }

    [Fact]
    public void Model_MapsAuditEntryType()
    {
        using var context = CreateContext();

        var entityType = context.Model.FindEntityType(typeof(AuditEntry));

        Assert.NotNull(entityType);
    }

    [Fact]
    public void AuditEntries_DbSet_MapsToAuditEntriesTable()
    {
        using var context = CreateContext();

        var entityType = context.Model.FindEntityType(typeof(AuditEntry));

        Assert.NotNull(entityType);
        Assert.Equal("AuditEntries", entityType!.GetTableName());
        Assert.Equal("audit", entityType.GetSchema());
    }

    [Fact]
    public void AuditEntry_PrimaryKey_IsAuditEntryId()
    {
        using var context = CreateContext();

        var entityType = context.Model.FindEntityType(typeof(AuditEntry));

        Assert.NotNull(entityType);
        var primaryKey = entityType!.FindPrimaryKey();
        Assert.NotNull(primaryKey);
        Assert.Equal([nameof(AuditEntry.AuditEntryId)], primaryKey!.Properties.Select(p => p.Name));
    }

    [Fact]
    public void AuditEntry_HasAlternateKeyOnEventId()
    {
        using var context = CreateContext();

        var entityType = context.Model.FindEntityType(typeof(AuditEntry));
        Assert.NotNull(entityType);

        var alternateKeys = entityType!.GetKeys().Where(k => !k.IsPrimaryKey()).ToList();
        Assert.Single(alternateKeys);
        Assert.Equal([nameof(AuditEntry.EventId)], alternateKeys[0].Properties.Select(p => p.Name));
    }

    [Fact]
    public void AuditEntry_EventId_IsUniqueAndRequired()
    {
        using var context = CreateContext();

        var entityType = context.Model.FindEntityType(typeof(AuditEntry));
        Assert.NotNull(entityType);

        var eventIdProperty = entityType!.FindProperty(nameof(AuditEntry.EventId));
        Assert.NotNull(eventIdProperty);
        Assert.False(eventIdProperty!.IsNullable);
        Assert.Equal("nvarchar(256)", eventIdProperty.GetColumnType());
    }

    // InboxMessage tests

    [Fact]
    public void DbContext_HasInboxMessagesDbSet()
    {
        using var context = CreateContext();

        var inboxSet = context.InboxMessages;

        Assert.NotNull(inboxSet);
    }

    [Fact]
    public void Model_MapsInboxMessageType()
    {
        using var context = CreateContext();

        var entityType = context.Model.FindEntityType(typeof(InboxMessage));

        Assert.NotNull(entityType);
    }

    [Fact]
    public void InboxMessages_DbSet_MapsToInboxMessagesTable()
    {
        using var context = CreateContext();

        var entityType = context.Model.FindEntityType(typeof(InboxMessage));

        Assert.NotNull(entityType);
        Assert.Equal("InboxMessages", entityType!.GetTableName());
        // InboxMessage uses default schema (dbo)
        Assert.Null(entityType.GetSchema());
    }

    [Fact]
    public void InboxMessage_PrimaryKey_IsMessageId()
    {
        using var context = CreateContext();

        var entityType = context.Model.FindEntityType(typeof(InboxMessage));
        Assert.NotNull(entityType);

        var primaryKey = entityType!.FindPrimaryKey();
        Assert.NotNull(primaryKey);
        Assert.Equal([nameof(InboxMessage.MessageId)], primaryKey!.Properties.Select(p => p.Name));
    }

    [Fact]
    public void InboxMessage_MessageId_IsRequired()
    {
        using var context = CreateContext();

        var entityType = context.Model.FindEntityType(typeof(InboxMessage));
        Assert.NotNull(entityType);

        var messageIdProperty = entityType!.FindProperty(nameof(InboxMessage.MessageId));
        Assert.NotNull(messageIdProperty);
        Assert.False(messageIdProperty!.IsNullable);
    }

    // Schema integrity tests

    [Fact]
    public void DbContext_ContainsOnlyAuditEntryAndInboxMessageTypes()
    {
        using var context = CreateContext();

        var entityTypes = context.Model.GetEntityTypes().ToList();

        // Should only have AuditEntry and InboxMessage; no cross-service references.
        var typeNames = entityTypes.Select(t => t.ClrType.Name).OrderBy(n => n).ToList();
        Assert.Equal(["AuditEntry", "InboxMessage"], typeNames);
    }

    [Fact]
    public void DbContext_HasNoForeignKeyRelationships()
    {
        using var context = CreateContext();

        var allForeignKeys = context.Model.GetEntityTypes()
            .SelectMany(t => t.GetForeignKeys())
            .ToList();

        // Audit is a consumer-only; no outbound foreign keys to other services.
        Assert.Empty(allForeignKeys);
    }
}

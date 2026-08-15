using Microsoft.EntityFrameworkCore;
using ProjectChicago.Identity.Core.Models.DataModels.Entities;
using ProjectChicago.Identity.Core.Persistence;
using ProjectChicago.Shared.Inbox;
using ProjectChicago.Shared.Outbox;
using Xunit;

namespace ProjectChicago.Identity.Core.Tests.Persistence;

// OUTBOX-001..006/ASYNC-005/SEC-005: proves IdentityDbContext correctly maps Identity's own tables
// plus shared Outbox/Inbox infrastructure, while excluding CRM/Audit domain entities. Authentication
// events use the transactional outbox pattern for auditable, atomic coordination with asynchronous
// consumers (messaging.md). Preserved ASP.NET Core Identity schema ownership ensures framework-managed
// tables remain unmodified by application code.
public class IdentityDbContextMappingTests
{
    [Fact]
    public void IdentityDbContext_IncludesOutboxMessages()
    {
        var context = CreateInMemoryContext();
        var outboxSet = context.OutboxMessages;

        Assert.NotNull(outboxSet);
        // The DbSet exists and is typed for OutboxMessage
        Assert.IsAssignableFrom<DbSet<OutboxMessage>>(outboxSet);
    }

    [Fact]
    public void IdentityDbContext_IncludesInboxMessages()
    {
        var context = CreateInMemoryContext();
        var inboxSet = context.InboxMessages;

        Assert.NotNull(inboxSet);
        // The DbSet exists and is typed for InboxMessage
        Assert.IsAssignableFrom<DbSet<InboxMessage>>(inboxSet);
    }

    [Fact]
    public void IdentityDbContext_MapsPersistenceModel()
    {
        var context = CreateInMemoryContext();

        // Get all entity types registered in the model.
        var entityTypes = context.Model.GetEntityTypes().Select(e => e.Name).OrderBy(n => n).ToList();

        // OUTBOX-001..006: OutboxMessage and InboxMessage are mapped
        Assert.Contains("ProjectChicago.Shared.Outbox.OutboxMessage", entityTypes);
        Assert.Contains("ProjectChicago.Shared.Inbox.InboxMessage", entityTypes);

        // ASP.NET Core Identity tables (framework-managed schema ownership preserved)
        // These are automatically registered by IdentityDbContext<TUser, TRole, TKey>
        Assert.Contains("Microsoft.AspNetCore.Identity.IdentityUser<System.Guid>", entityTypes);
        Assert.Contains("Microsoft.AspNetCore.Identity.IdentityRole<System.Guid>", entityTypes);
        Assert.Contains("Microsoft.AspNetCore.Identity.IdentityUserClaim<System.Guid>", entityTypes);
        Assert.Contains("Microsoft.AspNetCore.Identity.IdentityRoleClaim<System.Guid>", entityTypes);
        Assert.Contains("Microsoft.AspNetCore.Identity.IdentityUserLogin<System.Guid>", entityTypes);
        Assert.Contains("Microsoft.AspNetCore.Identity.IdentityUserRole<System.Guid>", entityTypes);
        Assert.Contains("Microsoft.AspNetCore.Identity.IdentityUserToken<System.Guid>", entityTypes);
    }

    [Fact]
    public void IdentityDbContext_DoesNotContainCrmDomainEntities()
    {
        var context = CreateInMemoryContext();
        var entityTypes = context.Model.GetEntityTypes().Select(e => e.Name).ToList();

        // CRM domain entities must not appear (one database per service, cross-service boundary).
        Assert.DoesNotContain(entityTypes, t => t.Contains("Client"));
        Assert.DoesNotContain(entityTypes, t => t.Contains("Project"));
        Assert.DoesNotContain(entityTypes, t => t.Contains("TaskItem"));
    }

    [Fact]
    public void IdentityDbContext_DoesNotContainAuditDomainEntities()
    {
        var context = CreateInMemoryContext();
        var entityTypes = context.Model.GetEntityTypes().Select(e => e.Name).ToList();

        // Audit domain entities must not appear (audit.md: separate bounded service/database).
        Assert.DoesNotContain(entityTypes, t => t.Contains("AuditEvent"));
    }

    [Fact]
    public void OutboxMessageConfiguration_IsApplied()
    {
        var context = CreateInMemoryContext();
        var outboxEntityType = context.Model.FindEntityType(typeof(OutboxMessage));

        Assert.NotNull(outboxEntityType);

        // Table name should be "OutboxMessages" (SQL Server table name mapping).
        Assert.Equal("OutboxMessages", outboxEntityType.GetTableName());

        // Key should be the Id property
        var keyProperties = outboxEntityType.FindPrimaryKey()?.Properties.Select(p => p.Name).ToList();
        Assert.NotNull(keyProperties);
        Assert.Single(keyProperties);
        Assert.Contains("Id", keyProperties);
    }

    [Fact]
    public void InboxMessageConfiguration_IsApplied()
    {
        var context = CreateInMemoryContext();
        var inboxEntityType = context.Model.FindEntityType(typeof(InboxMessage));

        Assert.NotNull(inboxEntityType);

        // Table name should be "InboxMessages"
        Assert.Equal("InboxMessages", inboxEntityType.GetTableName());

        // Key should be the MessageId property (ASYNC-005: duplicate tolerance through unique constraint)
        var keyProperties = inboxEntityType.FindPrimaryKey()?.Properties.Select(p => p.Name).ToList();
        Assert.NotNull(keyProperties);
        Assert.Single(keyProperties);
        Assert.Contains("MessageId", keyProperties);
    }

    [Fact]
    public void IdentityDbContext_IdentityTablesUseGuidKey()
    {
        var context = CreateInMemoryContext();

        // ApplicationUser (IdentityUser<Guid>)
        var userEntityType = context.Model.FindEntityType(typeof(ApplicationUser));
        Assert.NotNull(userEntityType);
        var userKeyProperty = userEntityType.FindPrimaryKey()?.Properties.FirstOrDefault();
        Assert.NotNull(userKeyProperty);
        Assert.Equal(typeof(Guid), userKeyProperty.ClrType);

        // IdentityRole<Guid>
        var roleEntityType = context.Model.FindEntityType(typeof(Microsoft.AspNetCore.Identity.IdentityRole<Guid>));
        Assert.NotNull(roleEntityType);
        var roleKeyProperty = roleEntityType.FindPrimaryKey()?.Properties.FirstOrDefault();
        Assert.NotNull(roleKeyProperty);
        Assert.Equal(typeof(Guid), roleKeyProperty.ClrType);
    }

    private static IdentityDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(databaseName: $"IdentityDbMappingTest_{Guid.NewGuid()}")
            .Options;

        return new IdentityDbContext(options);
    }
}

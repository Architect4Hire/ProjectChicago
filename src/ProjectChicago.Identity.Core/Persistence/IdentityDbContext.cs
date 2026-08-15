using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ProjectChicago.Identity.Core.Models.DataModels.Entities;
using ProjectChicago.Shared.Inbox;
using ProjectChicago.Shared.Outbox;

namespace ProjectChicago.Identity.Core.Persistence;

// Identity's service-owned SQL Server database context (database.md/backend.md: one database per
// bounded service, service-specific schemas, migrations belong to the owning .Core project).
// IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid> is the ASP.NET Core Identity
// base DbContext, pre-configured with all required Identity tables (AspNetUsers, AspNetRoles,
// AspNetUserClaims, etc.) through IEntityTypeConfiguration applied by the framework.
// OutboxMessages and InboxMessages support the transactional outbox pattern (OUTBOX-001..006,
// ASYNC-005) for auditable authentication events (SEC-005) with atomic database commits -
// account mutations and Identity schema changes stay with Identity schema ownership (Aspire/EF
// framework-managed), and are coordinated with outbox drainage through a timer-triggered
// Function (messaging.md, functions.md) when Identity events must be published.
// Options are supplied by the host's composition root via Aspire's SQL Server EF Core client
// integration (AddSqlServerDbContext) - this type never calls UseSqlServer or holds a
// connection string itself (aspire.md).
public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Transactional outbox (OUTBOX-001..006): Identity mutations and audit events are
        // atomically persisted with outbox records in the same transaction, enabling
        // asynchronous publishing to downstream consumers without losing events on failure.
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());

        // Idempotent inbox (ASYNC-005): when Identity consumers are approved, this supports
        // duplicate-tolerant message processing for cross-service events.
        modelBuilder.ApplyConfiguration(new InboxMessageConfiguration());
    }
}

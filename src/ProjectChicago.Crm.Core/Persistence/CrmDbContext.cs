using Microsoft.EntityFrameworkCore;
using ProjectChicago.Crm.Core.Models.DataModels.Entities;
using ProjectChicago.Shared.Inbox;
using ProjectChicago.Shared.Outbox;

namespace ProjectChicago.Crm.Core.Persistence;

// CRM's service-owned SQL Server database context (database.md: one database per bounded service).
// Options are supplied by the host's composition root via Aspire's SQL Server EF Core client
// integration (AddSqlServerDbContext) - this type never calls UseSqlServer or holds a connection
// string itself. Client is mapped via ClientConfiguration below so DATA-004..008 constraints apply
// to the model; a repository and migration are added in a later microstep (this one is DbSet + EF
// configuration only - no Task DbSet yet). Project is likewise mapped via ProjectConfiguration
// (PROJECT-001..023, DATA-002..005) with its required-FK-to-Client invariant (DATA-002) enforced at
// the EF model level; a repository and migration are added in a later microstep. TaskItem is mapped
// via TaskItemConfiguration (TASK-001..022, DATA-003..005) with its required-FK-to-Project
// invariant (DATA-003) enforced the same way; a repository and migration are added in a later
// microstep.
public sealed class CrmDbContext(DbContextOptions<CrmDbContext> options) : DbContext(options)
{
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    public DbSet<Client> Clients => Set<Client>();

    public DbSet<Project> Projects => Set<Project>();

    public DbSet<TaskItem> Tasks => Set<TaskItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());
        modelBuilder.ApplyConfiguration(new InboxMessageConfiguration());
        modelBuilder.ApplyConfiguration(new ClientConfiguration());
        modelBuilder.ApplyConfiguration(new ProjectConfiguration());
        modelBuilder.ApplyConfiguration(new TaskItemConfiguration());
    }
}

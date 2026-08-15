using Microsoft.EntityFrameworkCore;
using ProjectChicago.Shared.Inbox;

namespace ProjectChicago.Audit.Core.Persistence;

/// <summary>
/// Audit Service database context. Maintains append-only audit entries and inbox for event processing idempotency (ADR-0016, ASYNC-005).
/// Inbox tracks consumed events for duplicate detection; AuditEntries record the audit trail.
/// </summary>
public class AuditDbContext(DbContextOptions<AuditDbContext> options) : DbContext(options)
{
    /// <summary>
    /// Append-only audit entries tracking every Client/Project/Task/User mutation (AUDIT-001..008).
    /// INSERT only; never UPDATE/DELETE in normal workflows.
    /// </summary>
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    /// <summary>
    /// Inbox messages for idempotent Service Bus-triggered consumption (ASYNC-005, AUDIT-004).
    /// Tracks message processing state and prevents duplicate side effects from redelivered events.
    /// </summary>
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply AuditEntry configuration (SQL Server indexes, constraints, column types).
        modelBuilder.ApplyConfiguration(new AuditEntryConfiguration());

        // Apply InboxMessage configuration from Shared (Audit consumes through Service Bus triggers).
        modelBuilder.ApplyConfiguration(new InboxMessageConfiguration());
    }
}

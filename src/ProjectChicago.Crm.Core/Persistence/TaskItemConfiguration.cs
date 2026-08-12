using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectChicago.Crm.Core.Models.DataModels.Entities;

namespace ProjectChicago.Crm.Core.Persistence;

// SQL Server-compatible EF mapping for TaskItem and its required Project foreign key
// (TASK-001..022, DATA-003..005; database.md). Applied via ModelBuilder.ApplyConfiguration only -
// no DbSet/repository/migration/query is added by this microstep, matching the Client/Project
// precedent of shipping mapping metadata ahead of the rest of the persistence stack.
public sealed class TaskItemConfiguration : IEntityTypeConfiguration<TaskItem>
{
    public void Configure(EntityTypeBuilder<TaskItem> builder)
    {
        builder.ToTable("Tasks");

        builder.HasKey(t => t.Id);

        // Application-assigned GUID, not database-generated (DATA-007: externally exposed
        // identifiers resistant to enumeration - TaskItem.Id doubles as the public identifier).
        builder.Property(t => t.Id)
            .HasColumnType("uniqueidentifier")
            .ValueGeneratedNever();

        builder.Property(t => t.ProjectId)
            .IsRequired()
            .HasColumnType("uniqueidentifier");

        builder.Property(t => t.Title)
            .IsRequired()
            .HasMaxLength(200);

        // Reuses Client.Description/Project.Description's precedent bound for free-form text
        // (narrowest reversible assumption - no Task-specific length is specified by TASK-002).
        builder.Property(t => t.Description)
            .HasMaxLength(2000);

        // Stored as the enum member name (matches Client.LifecycleStatus/Project.Status
        // convention) so status is legible directly in SQL Server tooling.
        builder.Property(t => t.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(t => t.Priority)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        // Same actor-identifier bound as Client/Project.OwnerUserId, but optional (TASK-013:
        // assignment happens after creation, so a Task can exist unassigned).
        builder.Property(t => t.AssignedUserId)
            .HasMaxLength(128);

        builder.Property(t => t.StartDateUtc)
            .HasColumnType("datetime2(3)");

        builder.Property(t => t.DueDateUtc)
            .HasColumnType("datetime2(3)");

        builder.Property(t => t.CompletedAtUtc)
            .HasColumnType("datetime2(3)");

        builder.Property(t => t.Notes)
            .HasMaxLength(2000);

        builder.Property(t => t.CreatedAtUtc)
            .IsRequired()
            .HasColumnType("datetime2(3)");

        builder.Property(t => t.CreatedBy)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(t => t.LastModifiedAtUtc)
            .IsRequired()
            .HasColumnType("datetime2(3)");

        builder.Property(t => t.LastModifiedBy)
            .IsRequired()
            .HasMaxLength(128);

        // DATA-008 (via Client/Project precedent): optimistic concurrency token so a stale update
        // never silently overwrites a newer write.
        builder.Property(t => t.RowVersion)
            .IsRowVersion();

        // DATA-003/DATA-004: a Task cannot exist without a Project, enforced at the database
        // layer. No navigation property exists on TaskItem (Data/Repository resolve the
        // relationship - onion-boundaries.md), so this is a shadow relationship on ProjectId
        // alone. Restrict (not Cascade) keeps Project deletion from physically destroying
        // historical Task data, matching the non-destructive/archival-over-deletion model
        // (DATA-020) already applied to Project's own foreign key to Client.
        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(t => t.ProjectId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        // TASK-020/TASK-021: Task lists/views are filtered by Project, status, priority, assignee,
        // and due date, so each gets its own index. ProjectId's FK index is declared explicitly
        // here for a predictable, testable name rather than relying on EF Core's default naming.
        builder.HasIndex(t => t.ProjectId)
            .HasDatabaseName("IX_Tasks_ProjectId");

        builder.HasIndex(t => t.Status)
            .HasDatabaseName("IX_Tasks_Status");

        builder.HasIndex(t => t.Priority)
            .HasDatabaseName("IX_Tasks_Priority");

        builder.HasIndex(t => t.AssignedUserId)
            .HasDatabaseName("IX_Tasks_AssignedUserId");

        builder.HasIndex(t => t.DueDateUtc)
            .HasDatabaseName("IX_Tasks_DueDateUtc");
    }
}

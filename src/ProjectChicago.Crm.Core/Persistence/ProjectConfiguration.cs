using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectChicago.Crm.Core.Models.DataModels.Entities;

namespace ProjectChicago.Crm.Core.Persistence;

// SQL Server-compatible EF mapping for Project and its Client foreign key (PROJECT-001..023,
// DATA-002..005; database.md). Applied via ModelBuilder.ApplyConfiguration and exposed through
// CrmDbContext.Projects - no repository/migration/Task relationship is added by this microstep.
public sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("Projects");

        builder.HasKey(p => p.Id);

        // Application-assigned GUID, not database-generated (DATA-007: externally exposed
        // identifiers resistant to enumeration - Project.Id doubles as the public identifier).
        builder.Property(p => p.Id)
            .HasColumnType("uniqueidentifier")
            .ValueGeneratedNever();

        builder.Property(p => p.ClientId)
            .IsRequired()
            .HasColumnType("uniqueidentifier");

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(200);

        // Reuses Client.Description's precedent bound for free-form text (narrowest reversible
        // assumption - no Project-specific length is specified by PROJECT-002).
        builder.Property(p => p.Description)
            .HasMaxLength(2000);

        // Stored as the enum member name (matches Client.LifecycleStatus/OutboxMessageStatus
        // convention) so status is legible directly in SQL Server tooling.
        builder.Property(p => p.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(p => p.Priority)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        // Same actor-identifier convention/bound as Client.OwnerUserId.
        builder.Property(p => p.OwnerUserId)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(p => p.StartDateUtc)
            .HasColumnType("datetime2(3)");

        builder.Property(p => p.TargetCompletionDateUtc)
            .HasColumnType("datetime2(3)");

        builder.Property(p => p.ActualCompletionDateUtc)
            .HasColumnType("datetime2(3)");

        builder.Property(p => p.Notes)
            .HasMaxLength(2000);

        builder.Property(p => p.CreatedAtUtc)
            .IsRequired()
            .HasColumnType("datetime2(3)");

        builder.Property(p => p.CreatedBy)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(p => p.LastModifiedAtUtc)
            .IsRequired()
            .HasColumnType("datetime2(3)");

        builder.Property(p => p.LastModifiedBy)
            .IsRequired()
            .HasMaxLength(128);

        // DATA-008 (via Client precedent): optimistic concurrency token so a stale update never
        // silently overwrites a newer write.
        builder.Property(p => p.RowVersion)
            .IsRowVersion();

        // DATA-002/DATA-004: a Project cannot exist without a Client, enforced at the database
        // layer. No navigation property exists on Project (Data/Repository resolve the
        // relationship - onion-boundaries.md), so this is a shadow relationship on ClientId alone.
        // Restrict (not Cascade) keeps Client deletion from silently destroying Projects, matching
        // the non-destructive/archival-over-deletion model (PROJECT-014, DATA-020).
        builder.HasOne<Client>()
            .WithMany()
            .HasForeignKey(p => p.ClientId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        // PROJECT-020/PROJECT-021: Projects are viewed/filtered by Client, owner, priority, and
        // date, so each gets its own index. Status shares the FK's implicit query pattern
        // (PROJECT-021) and gets a plain index; ClientId's FK index is declared explicitly here for
        // a predictable, testable name rather than relying on EF Core's default naming.
        builder.HasIndex(p => p.ClientId)
            .HasDatabaseName("IX_Projects_ClientId");

        builder.HasIndex(p => p.Status)
            .HasDatabaseName("IX_Projects_Status");

        builder.HasIndex(p => p.OwnerUserId)
            .HasDatabaseName("IX_Projects_OwnerUserId");

        builder.HasIndex(p => p.Priority)
            .HasDatabaseName("IX_Projects_Priority");

        builder.HasIndex(p => p.StartDateUtc)
            .HasDatabaseName("IX_Projects_StartDateUtc");

        builder.HasIndex(p => p.TargetCompletionDateUtc)
            .HasDatabaseName("IX_Projects_TargetCompletionDateUtc");
    }
}

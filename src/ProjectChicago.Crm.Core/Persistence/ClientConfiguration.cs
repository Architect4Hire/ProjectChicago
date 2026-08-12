using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectChicago.Crm.Core.Models.DataModels.Entities;

namespace ProjectChicago.Crm.Core.Persistence;

// SQL Server-compatible EF mapping for Client (CLIENT-002..004, DATA-004..008; database.md).
// Applied directly via ModelBuilder.ApplyConfiguration - no DbSet/repository/migration is added by
// this microstep, so this configuration is the only thing that currently puts Client in the model.
public sealed class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> builder)
    {
        builder.ToTable("Clients");

        builder.HasKey(c => c.Id);

        // Application-assigned GUID, not database-generated (DATA-007: externally exposed
        // identifiers resistant to enumeration - Client.Id doubles as the public identifier).
        builder.Property(c => c.Id)
            .HasColumnType("uniqueidentifier")
            .ValueGeneratedNever();

        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(c => c.PrimaryContactName)
            .HasMaxLength(200);

        builder.Property(c => c.PrimaryEmail)
            .HasMaxLength(320);

        builder.Property(c => c.PrimaryPhone)
            .HasMaxLength(32);

        builder.Property(c => c.Website)
            .HasMaxLength(2048);

        builder.Property(c => c.AddressLine)
            .HasMaxLength(300);

        builder.Property(c => c.City)
            .HasMaxLength(150);

        builder.Property(c => c.StateOrProvince)
            .HasMaxLength(150);

        builder.Property(c => c.PostalCode)
            .HasMaxLength(20);

        builder.Property(c => c.Country)
            .HasMaxLength(100);

        // Stored as the enum member name (matches OutboxMessageStatus/InboxMessageStatus
        // convention) so "Archived" is legible directly in SQL Server tooling and in the
        // filtered index predicate below.
        builder.Property(c => c.LifecycleStatus)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(c => c.Description)
            .HasMaxLength(2000);

        // Same actor-identifier convention/bound as OutboxMessage.LeaseOwner and
        // InboxMessage.LeaseOwner (Shared Outbox/Inbox configurations).
        builder.Property(c => c.OwnerUserId)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(c => c.CreatedAtUtc)
            .IsRequired()
            .HasColumnType("datetime2(3)");

        builder.Property(c => c.CreatedBy)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(c => c.LastModifiedAtUtc)
            .IsRequired()
            .HasColumnType("datetime2(3)");

        builder.Property(c => c.LastModifiedBy)
            .IsRequired()
            .HasMaxLength(128);

        // DATA-008: optimistic concurrency token so a stale update never silently overwrites a
        // newer write.
        builder.Property(c => c.RowVersion)
            .IsRowVersion();

        // No intra-service foreign keys exist on Client yet (DATA-004 has nothing to enforce at
        // this microstep) - OwnerUserId references the separately owned Identity service and
        // cannot be a database-level FK across bounded-service databases (CLAUDE.md reference
        // direction/database.md ownership rules).

        // CLIENT-003/CLIENT-004: supports name search and name-based duplicate-detection lookups.
        builder.HasIndex(c => c.Name)
            .HasDatabaseName("IX_Clients_Name");

        // CLIENT-004: supports email/phone duplicate-detection lookups during creation.
        builder.HasIndex(c => c.PrimaryEmail)
            .HasDatabaseName("IX_Clients_PrimaryEmail");

        builder.HasIndex(c => c.PrimaryPhone)
            .HasDatabaseName("IX_Clients_PrimaryPhone");

        // CLIENT-013: normal Client lists exclude Archived records. A filtered index keeps that
        // default list/search predicate cheap without indexing Archived rows those queries skip.
        builder.HasIndex(c => c.LifecycleStatus)
            .HasDatabaseName("IX_Clients_LifecycleStatus_ExcludingArchived")
            .HasFilter("[LifecycleStatus] <> N'Archived'");
    }
}

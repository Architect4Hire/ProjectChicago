using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ProjectChicago.Shared.Inbox;

// SQL Server-compatible EF mapping for InboxMessage. Consumed by each subscribing service's own
// DbContext via ApplyConfiguration - this project does not own a DbContext.
public sealed class InboxMessageConfiguration : IEntityTypeConfiguration<InboxMessage>
{
    public void Configure(EntityTypeBuilder<InboxMessage> builder)
    {
        builder.ToTable("InboxMessages");

        // MessageId as the primary key is the explicit idempotency guarantee: a duplicate delivery
        // is a unique-constraint violation, not a separate lookup query.
        builder.HasKey(m => m.MessageId);

        builder.Property(m => m.MessageId)
            .IsRequired()
            .HasMaxLength(128)
            .ValueGeneratedNever();

        builder.Property(m => m.ContractType)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(m => m.ContractVersion)
            .IsRequired();

        builder.Property(m => m.CorrelationId)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(m => m.CausationId)
            .HasMaxLength(64);

        builder.Property(m => m.TraceId)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(m => m.ReceivedAtUtc)
            .IsRequired()
            .HasColumnType("datetime2(3)");

        builder.Property(m => m.ProcessingStartedAtUtc)
            .HasColumnType("datetime2(3)");

        builder.Property(m => m.ProcessingCompletedAtUtc)
            .HasColumnType("datetime2(3)");

        builder.Property(m => m.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(m => m.AttemptCount)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(m => m.LastAttemptAtUtc)
            .HasColumnType("datetime2(3)");

        builder.Property(m => m.LastError)
            .HasMaxLength(1000);

        builder.Property(m => m.LeaseOwner)
            .HasMaxLength(128);

        builder.Property(m => m.LeasedUntilUtc)
            .HasColumnType("datetime2(3)");

        builder.Property(m => m.RowVersion)
            .IsRowVersion();

        // Supports stale-lease recovery scans and dead-letter/failure observability (ASYNC-008)
        // without requiring a full table scan for in-flight or failed rows.
        builder.HasIndex(m => new { m.Status, m.LeasedUntilUtc })
            .HasDatabaseName("IX_InboxMessages_Status_LeasedUntilUtc");
    }
}

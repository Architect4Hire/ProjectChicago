using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ProjectChicago.Shared.Outbox;

// SQL Server-compatible EF mapping for OutboxMessage. Consumed by each publishing service's own
// DbContext via ApplyConfiguration - this project does not own a DbContext.
public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id)
            .HasColumnType("uniqueidentifier")
            .ValueGeneratedNever();

        builder.Property(m => m.ContractType)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(m => m.ContractVersion)
            .IsRequired();

        builder.Property(m => m.Payload)
            .IsRequired()
            .HasColumnType("nvarchar(max)");

        builder.Property(m => m.CorrelationId)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(m => m.CausationId)
            .HasMaxLength(64);

        builder.Property(m => m.TraceId)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(m => m.OccurredAtUtc)
            .IsRequired()
            .HasColumnType("datetime2(3)");

        builder.Property(m => m.CreatedAtUtc)
            .IsRequired()
            .HasColumnType("datetime2(3)");

        builder.Property(m => m.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(m => m.DispatchedAtUtc)
            .HasColumnType("datetime2(3)");

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

        // Supports the relay's pending-batch selection ordered by age, and the OUTBOX-006
        // oldest-unpublished-message-age metric.
        builder.HasIndex(m => new { m.Status, m.CreatedAtUtc })
            .HasDatabaseName("IX_OutboxMessages_Status_CreatedAtUtc");
    }
}

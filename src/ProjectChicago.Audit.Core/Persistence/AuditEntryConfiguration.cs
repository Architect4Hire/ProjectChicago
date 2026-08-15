using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ProjectChicago.Audit.Core.Persistence;

/// <summary>
/// EF Core configuration for AuditEntry (append-only audit trail, ADR-0016).
/// Enforces SQL Server-compatible constraints and indexes for query performance and data integrity.
/// </summary>
public class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.ToTable("AuditEntries", schema: "audit");

        // Primary key: AuditEntryId (GUID).
        builder.HasKey(a => a.AuditEntryId);

        // Unique constraint on EventId for idempotent event consumption (ASYNC-005, AUDIT-004).
        // Prevents duplicate audit entries when Service Bus redelivers the same message.
        builder.HasAlternateKey(a => a.EventId)
            .HasName("AK_AuditEntries_EventId");

        // SQL Server data types.
        builder.Property(a => a.AuditEntryId)
            .HasColumnType("uniqueidentifier")
            .HasDefaultValueSql("newid()");

        builder.Property(a => a.EventId)
            .HasColumnType("nvarchar(256)")
            .IsRequired();

        builder.Property(a => a.EntityType)
            .HasColumnType("nvarchar(64)")
            .IsRequired();

        builder.Property(a => a.EntityId)
            .HasColumnType("uniqueidentifier")
            .IsRequired();

        builder.Property(a => a.Action)
            .HasColumnType("nvarchar(64)")
            .IsRequired();

        builder.Property(a => a.ActionCategory)
            .HasColumnType("nvarchar(32)")
            .IsRequired();

        builder.Property(a => a.ActorUserId)
            .HasColumnType("uniqueidentifier");

        builder.Property(a => a.ActorType)
            .HasColumnType("nvarchar(32)")
            .IsRequired();

        builder.Property(a => a.ActorDisplayName)
            .HasColumnType("nvarchar(256)");

        builder.Property(a => a.SourceService)
            .HasColumnType("nvarchar(64)")
            .IsRequired();

        builder.Property(a => a.SourceEventType)
            .HasColumnType("nvarchar(128)")
            .IsRequired();

        builder.Property(a => a.OccurredAtUtc)
            .HasColumnType("datetime2")
            .IsRequired();

        builder.Property(a => a.AuditedAtUtc)
            .HasColumnType("datetime2")
            .IsRequired();

        builder.Property(a => a.TraceId)
            .HasColumnType("nvarchar(64)")
            .IsRequired();

        builder.Property(a => a.CorrelationId)
            .HasColumnType("nvarchar(256)")
            .IsRequired();

        builder.Property(a => a.CausationId)
            .HasColumnType("nvarchar(256)");

        // ChangedFields: JSON array of field names (array<string> in JSON, stored as nvarchar).
        builder.Property(a => a.ChangedFields)
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        // PreviousValues/NewValues: JSON objects { "fieldName": value } (stored as nvarchar, not jsonb).
        builder.Property(a => a.PreviousValues)
            .HasColumnType("nvarchar(max)");

        builder.Property(a => a.NewValues)
            .HasColumnType("nvarchar(max)");

        builder.Property(a => a.SummaryDescription)
            .HasColumnType("nvarchar(max)");

        // RawEventPayload: Complete integration event, redacted per AuditSensitiveFieldNames.
        builder.Property(a => a.RawEventPayload)
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        // Optimistic concurrency (rowversion prevents accidental updates, though INSERT-only design is primary).
        builder.Property(a => a.RowVersion)
            .IsRowVersion();

        // Indexes for common query patterns (ADR-0016, AUDIT-007).

        // By entity type and ID: retrieve all audit entries for a specific Client/Project/Task.
        builder.HasIndex(a => new { a.EntityType, a.EntityId, a.OccurredAtUtc })
            .HasDatabaseName("IX_AuditEntries_EntityTypeId_OccurredAt");

        // By time range: audit trail queries filtered by date.
        builder.HasIndex(a => a.OccurredAtUtc)
            .HasDatabaseName("IX_AuditEntries_OccurredAt");

        // By trace ID: link audit entry to distributed traces (ADR-0021, AUDIT-007).
        builder.HasIndex(a => a.TraceId)
            .HasDatabaseName("IX_AuditEntries_TraceId");

        // By correlation ID: reconstruct entire request flow across services.
        builder.HasIndex(a => a.CorrelationId)
            .HasDatabaseName("IX_AuditEntries_CorrelationId");

        // By actor: find all actions by a user for compliance/investigation.
        builder.HasIndex(a => new { a.ActorUserId, a.AuditedAtUtc })
            .HasDatabaseName("IX_AuditEntries_Actor_AuditedAt");

        // By source service and action: audit load by service or filter by mutation type.
        builder.HasIndex(a => new { a.SourceService, a.Action, a.AuditedAtUtc })
            .HasDatabaseName("IX_AuditEntries_Service_Action_AuditedAt");
    }
}

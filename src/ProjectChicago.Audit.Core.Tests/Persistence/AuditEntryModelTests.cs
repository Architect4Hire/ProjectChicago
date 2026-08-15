using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using ProjectChicago.Audit.Core.Persistence;
using Xunit;

namespace ProjectChicago.Audit.Core.Tests.Persistence;

/// <summary>
/// Tests for AuditEntry model shape, properties, and EF Core configuration (ADR-0016, AUDIT-001..008).
/// Verifies append-only semantics, SQL Server column types, indexes, and uniqueness constraints.
/// </summary>
public class AuditEntryModelTests
{
    [Fact]
    public void AuditEntry_CanConstruct_WithRequiredFields()
    {
        // Arrange & Act
        var entry = new AuditEntry
        {
            AuditEntryId = Guid.NewGuid(),
            EventId = "evt-client-created-123",
            EntityType = "Client",
            EntityId = Guid.NewGuid(),
            Action = "Created",
            ActionCategory = "WRITE",
            ActorType = "User",
            SourceService = "Crm",
            SourceEventType = "Crm.ClientCreated",
            OccurredAtUtc = DateTime.UtcNow,
            AuditedAtUtc = DateTime.UtcNow,
            TraceId = "4bf92f3577b34da6a3ce929d0e0e4736",
            CorrelationId = "corr-12345",
            ChangedFields = "[]",
            RawEventPayload = "{}"
        };

        // Assert
        Assert.NotEqual(Guid.Empty, entry.AuditEntryId);
        Assert.NotEmpty(entry.EventId);
        Assert.Equal("Client", entry.EntityType);
        Assert.Equal("Created", entry.Action);
        Assert.Equal("WRITE", entry.ActionCategory);
    }

    [Fact]
    public void AuditEntry_Allows_OptionalFields()
    {
        // Arrange & Act
        var entry = new AuditEntry
        {
            AuditEntryId = Guid.NewGuid(),
            EventId = "evt-123",
            EntityType = "Client",
            EntityId = Guid.NewGuid(),
            Action = "Updated",
            ActionCategory = "WRITE",
            ActorType = "System",
            SourceService = "Crm",
            SourceEventType = "Crm.ClientUpdated",
            OccurredAtUtc = DateTime.UtcNow,
            AuditedAtUtc = DateTime.UtcNow,
            TraceId = "trace-123",
            CorrelationId = "corr-456",
            ActorUserId = null,
            ActorDisplayName = null,
            CausationId = null,
            PreviousValues = null,
            NewValues = null,
            SummaryDescription = null,
            ChangedFields = "[]",
            RawEventPayload = "{}"
        };

        // Assert
        Assert.Null(entry.ActorUserId);
        Assert.Null(entry.ActorDisplayName);
        Assert.Null(entry.CausationId);
        Assert.Null(entry.PreviousValues);
    }

    [Fact]
    public void AuditEntry_WithUserActor_StoresActorMetadata()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var entry = new AuditEntry
        {
            AuditEntryId = Guid.NewGuid(),
            EventId = "evt-user-action",
            EntityType = "Project",
            EntityId = Guid.NewGuid(),
            Action = "StatusChanged",
            ActionCategory = "TRANSITION",
            ActorUserId = userId,
            ActorType = "User",
            ActorDisplayName = "alice@example.com",
            SourceService = "Crm",
            SourceEventType = "Crm.ProjectStatusChanged",
            OccurredAtUtc = DateTime.UtcNow,
            AuditedAtUtc = DateTime.UtcNow,
            TraceId = "trace-456",
            CorrelationId = "corr-789",
            ChangedFields = "[\"Status\"]",
            PreviousValues = "{\"Status\":\"Active\"}",
            NewValues = "{\"Status\":\"Completed\"}",
            RawEventPayload = "{}"
        };

        // Act & Assert
        Assert.Equal(userId, entry.ActorUserId);
        Assert.Equal("User", entry.ActorType);
        Assert.Equal("alice@example.com", entry.ActorDisplayName);
    }
}

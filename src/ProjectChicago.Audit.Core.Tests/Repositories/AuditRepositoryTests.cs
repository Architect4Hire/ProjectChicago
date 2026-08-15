using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ProjectChicago.Audit.Core.Persistence;
using ProjectChicago.Audit.Core.Repositories;
using ProjectChicago.Audit.Core.Tests.Persistence;
using Xunit;

namespace ProjectChicago.Audit.Core.Tests.Repositories;

/// <summary>
/// SQL integration tests for AuditRepository read operations (AUDIT-001..008, AUDIT-007, PERF-003/004).
/// Tests ordering, pagination, entity filtering, and trace/correlation ID lookup against a real SQL Server database.
/// Each test gets its own isolated database from the shared container fixture.
/// </summary>
public class AuditRepositoryTests : IClassFixture<MsSqlContainerFixture>
{
    private readonly MsSqlContainerFixture _fixture;

    public AuditRepositoryTests(MsSqlContainerFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<AuditDbContext> CreateContextAsync(string databaseName)
    {
        var builder = new SqlConnectionStringBuilder(_fixture.ConnectionString)
        {
            InitialCatalog = databaseName,
        };

        var options = new DbContextOptionsBuilder<AuditDbContext>()
            .UseSqlServer(builder.ConnectionString)
            .Options;

        var context = new AuditDbContext(options);
        await context.Database.EnsureCreatedAsync();
        return context;
    }

    private async Task SeedTestDataAsync(AuditDbContext context)
    {
        var clientId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var traceId1 = "4bf92f3577b34da6a3ce929d0e0e4736";
        var correlationId1 = "correlation-001";

        // Create audit entries with varied timestamps for ordering tests.
        var baseTime = new DateTime(2026, 8, 15, 10, 0, 0, DateTimeKind.Utc);
        var entries = new List<AuditEntry>
        {
            // Entity entries (Client) - for entity filtering tests
            new()
            {
                AuditEntryId = Guid.NewGuid(),
                EventId = "event-001",
                EntityType = "Client",
                EntityId = clientId,
                Action = "Created",
                ActionCategory = "WRITE",
                ActorUserId = Guid.NewGuid(),
                ActorType = "User",
                ActorDisplayName = "user1",
                SourceService = "Crm",
                SourceEventType = "Crm.EntityMutationAudited",
                OccurredAtUtc = baseTime,
                AuditedAtUtc = baseTime.AddSeconds(1),
                TraceId = traceId1,
                CorrelationId = correlationId1,
                CausationId = null,
                ChangedFields = "[]",
                PreviousValues = null,
                NewValues = null,
                SummaryDescription = "Client created",
                RawEventPayload = "{}",
            },
            new()
            {
                AuditEntryId = Guid.NewGuid(),
                EventId = "event-002",
                EntityType = "Client",
                EntityId = clientId,
                Action = "StatusChanged",
                ActionCategory = "TRANSITION",
                ActorUserId = Guid.NewGuid(),
                ActorType = "User",
                ActorDisplayName = "user2",
                SourceService = "Crm",
                SourceEventType = "Crm.EntityMutationAudited",
                OccurredAtUtc = baseTime.AddSeconds(10),
                AuditedAtUtc = baseTime.AddSeconds(11),
                TraceId = traceId1,
                CorrelationId = correlationId1,
                CausationId = null,
                ChangedFields = "[\"Status\"]",
                PreviousValues = "{\"Status\": \"Lead\"}",
                NewValues = "{\"Status\": \"Prospect\"}",
                SummaryDescription = "Status changed from Lead to Prospect",
                RawEventPayload = "{}",
            },
            new()
            {
                AuditEntryId = Guid.NewGuid(),
                EventId = "event-003",
                EntityType = "Client",
                EntityId = clientId,
                Action = "Updated",
                ActionCategory = "WRITE",
                ActorUserId = Guid.NewGuid(),
                ActorType = "User",
                ActorDisplayName = "user3",
                SourceService = "Crm",
                SourceEventType = "Crm.EntityMutationAudited",
                OccurredAtUtc = baseTime.AddSeconds(20),
                AuditedAtUtc = baseTime.AddSeconds(21),
                TraceId = traceId1,
                CorrelationId = correlationId1,
                CausationId = null,
                ChangedFields = "[\"Name\"]",
                PreviousValues = "{\"Name\": \"Old Name\"}",
                NewValues = "{\"Name\": \"New Name\"}",
                SummaryDescription = "Client updated",
                RawEventPayload = "{}",
            },
            // Project entries - for different entity type filtering
            new()
            {
                AuditEntryId = Guid.NewGuid(),
                EventId = "event-004",
                EntityType = "Project",
                EntityId = projectId,
                Action = "Created",
                ActionCategory = "WRITE",
                ActorUserId = Guid.NewGuid(),
                ActorType = "User",
                ActorDisplayName = "user1",
                SourceService = "Crm",
                SourceEventType = "Crm.EntityMutationAudited",
                OccurredAtUtc = baseTime.AddSeconds(5),
                AuditedAtUtc = baseTime.AddSeconds(6),
                TraceId = "trace-002",
                CorrelationId = "correlation-002",
                CausationId = null,
                ChangedFields = "[]",
                PreviousValues = null,
                NewValues = null,
                SummaryDescription = "Project created",
                RawEventPayload = "{}",
            },
        };

        context.AuditEntries.AddRange(entries);
        await context.SaveChangesAsync();
    }

    // Tests: Ordering

    [Fact]
    public async Task QueryByEntity_OrdersByOccurredAtUtcDescending()
    {
        var db = nameof(QueryByEntity_OrdersByOccurredAtUtcDescending);
        await using var context = await CreateContextAsync(db);
        var repository = new AuditRepository(context);

        await SeedTestDataAsync(context);
        var clientId = context.AuditEntries.First(a => a.EntityType == "Client").EntityId;

        var result = await repository.QueryByEntityAsync("Client", clientId, 1, 10, default);

        Assert.Equal(3, result.Items.Count);
        // Verify ordering: most recent first
        Assert.True(result.Items[0].OccurredAtUtc > result.Items[1].OccurredAtUtc);
        Assert.True(result.Items[1].OccurredAtUtc > result.Items[2].OccurredAtUtc);
    }

    [Fact]
    public async Task QueryByTraceOrCorrelation_OrdersByOccurredAtUtcDescending()
    {
        var db = nameof(QueryByTraceOrCorrelation_OrdersByOccurredAtUtcDescending);
        await using var context = await CreateContextAsync(db);
        var repository = new AuditRepository(context);

        await SeedTestDataAsync(context);

        var result = await repository.QueryByTraceOrCorrelationIdAsync(
            "4bf92f3577b34da6a3ce929d0e0e4736",
            null,
            1,
            10,
            default);

        Assert.Equal(3, result.Items.Count);
        // Verify ordering: most recent first
        Assert.True(result.Items[0].OccurredAtUtc > result.Items[1].OccurredAtUtc);
        Assert.True(result.Items[1].OccurredAtUtc > result.Items[2].OccurredAtUtc);
    }

    // Tests: Pagination

    [Fact]
    public async Task QueryByEntity_SupportsPagination_FirstPage()
    {
        var db = nameof(QueryByEntity_SupportsPagination_FirstPage);
        await using var context = await CreateContextAsync(db);
        var repository = new AuditRepository(context);

        await SeedTestDataAsync(context);
        var clientId = context.AuditEntries.First(a => a.EntityType == "Client").EntityId;

        var result = await repository.QueryByEntityAsync("Client", clientId, 1, 2, default);

        Assert.Equal(2, result.Items.Count);
        Assert.Equal(3, result.TotalCount);
    }

    [Fact]
    public async Task QueryByEntity_SupportsPagination_SecondPage()
    {
        var db = nameof(QueryByEntity_SupportsPagination_SecondPage);
        await using var context = await CreateContextAsync(db);
        var repository = new AuditRepository(context);

        await SeedTestDataAsync(context);
        var clientId = context.AuditEntries.First(a => a.EntityType == "Client").EntityId;

        var result = await repository.QueryByEntityAsync("Client", clientId, 2, 2, default);

        Assert.Single(result.Items);
        Assert.Equal(3, result.TotalCount);
    }

    [Fact]
    public async Task QueryByEntity_SupportsPagination_OutOfBounds()
    {
        var db = nameof(QueryByEntity_SupportsPagination_OutOfBounds);
        await using var context = await CreateContextAsync(db);
        var repository = new AuditRepository(context);

        await SeedTestDataAsync(context);
        var clientId = context.AuditEntries.First(a => a.EntityType == "Client").EntityId;

        var result = await repository.QueryByEntityAsync("Client", clientId, 10, 2, default);

        Assert.Empty(result.Items);
        Assert.Equal(3, result.TotalCount);
    }

    [Fact]
    public async Task QueryByTraceOrCorrelation_SupportsPagination()
    {
        var db = nameof(QueryByTraceOrCorrelation_SupportsPagination);
        await using var context = await CreateContextAsync(db);
        var repository = new AuditRepository(context);

        await SeedTestDataAsync(context);

        var result = await repository.QueryByTraceOrCorrelationIdAsync(
            "4bf92f3577b34da6a3ce929d0e0e4736",
            null,
            2,
            2,
            default);

        Assert.Single(result.Items);
        Assert.Equal(3, result.TotalCount);
    }

    // Tests: Entity Filtering

    [Fact]
    public async Task QueryByEntity_FiltersClientEntries()
    {
        var db = nameof(QueryByEntity_FiltersClientEntries);
        await using var context = await CreateContextAsync(db);
        var repository = new AuditRepository(context);

        await SeedTestDataAsync(context);
        var clientId = context.AuditEntries.First(a => a.EntityType == "Client").EntityId;

        var result = await repository.QueryByEntityAsync("Client", clientId, 1, 10, default);

        Assert.Equal(3, result.Items.Count);
        Assert.All(result.Items, item =>
        {
            Assert.Equal("Client", item.EntityType);
            Assert.Equal(clientId, item.EntityId);
        });
    }

    [Fact]
    public async Task QueryByEntity_FiltersProjectEntries()
    {
        var db = nameof(QueryByEntity_FiltersProjectEntries);
        await using var context = await CreateContextAsync(db);
        var repository = new AuditRepository(context);

        await SeedTestDataAsync(context);
        var projectId = context.AuditEntries.First(a => a.EntityType == "Project").EntityId;

        var result = await repository.QueryByEntityAsync("Project", projectId, 1, 10, default);

        Assert.Single(result.Items);
        Assert.Equal("Project", result.Items[0].EntityType);
        Assert.Equal(projectId, result.Items[0].EntityId);
    }

    [Fact]
    public async Task QueryByEntity_ReturnsEmptyWhenNoMatches()
    {
        var db = nameof(QueryByEntity_ReturnsEmptyWhenNoMatches);
        await using var context = await CreateContextAsync(db);
        var repository = new AuditRepository(context);

        await SeedTestDataAsync(context);

        var result = await repository.QueryByEntityAsync("Client", Guid.NewGuid(), 1, 10, default);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }

    // Tests: Trace/Correlation ID Lookup

    [Fact]
    public async Task QueryByTraceOrCorrelation_FiltersByTraceId()
    {
        var db = nameof(QueryByTraceOrCorrelation_FiltersByTraceId);
        await using var context = await CreateContextAsync(db);
        var repository = new AuditRepository(context);

        await SeedTestDataAsync(context);

        var result = await repository.QueryByTraceOrCorrelationIdAsync(
            "4bf92f3577b34da6a3ce929d0e0e4736",
            null,
            1,
            10,
            default);

        Assert.Equal(3, result.Items.Count);
        Assert.All(result.Items, item => Assert.Equal("4bf92f3577b34da6a3ce929d0e0e4736", item.TraceId));
    }

    [Fact]
    public async Task QueryByTraceOrCorrelation_FiltersByCorrelationId()
    {
        var db = nameof(QueryByTraceOrCorrelation_FiltersByCorrelationId);
        await using var context = await CreateContextAsync(db);
        var repository = new AuditRepository(context);

        await SeedTestDataAsync(context);

        var result = await repository.QueryByTraceOrCorrelationIdAsync(
            null,
            "correlation-001",
            1,
            10,
            default);

        Assert.Equal(3, result.Items.Count);
        Assert.All(result.Items, item => Assert.Equal("correlation-001", item.CorrelationId));
    }

    [Fact]
    public async Task QueryByTraceOrCorrelation_FiltersByEitherTraceOrCorrelation()
    {
        var db = nameof(QueryByTraceOrCorrelation_FiltersByEitherTraceOrCorrelation);
        await using var context = await CreateContextAsync(db);
        var repository = new AuditRepository(context);

        await SeedTestDataAsync(context);

        // Query with both: should match entries with either trace or correlation
        var result = await repository.QueryByTraceOrCorrelationIdAsync(
            "4bf92f3577b34da6a3ce929d0e0e4736",
            "correlation-002",
            1,
            10,
            default);

        // Should return: 3 from trace-001 + 1 from correlation-002 = 4 total
        Assert.Equal(4, result.Items.Count);
        Assert.Equal(4, result.TotalCount);
    }

    [Fact]
    public async Task QueryByTraceOrCorrelation_ReturnsEmptyWhenNoMatches()
    {
        var db = nameof(QueryByTraceOrCorrelation_ReturnsEmptyWhenNoMatches);
        await using var context = await CreateContextAsync(db);
        var repository = new AuditRepository(context);

        await SeedTestDataAsync(context);

        var result = await repository.QueryByTraceOrCorrelationIdAsync(
            "nonexistent-trace",
            null,
            1,
            10,
            default);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }

    // Tests: Field Visibility (ensure RawEventPayload is excluded)

    [Fact]
    public async Task QueryByEntity_ExcludesRawEventPayload()
    {
        var db = nameof(QueryByEntity_ExcludesRawEventPayload);
        await using var context = await CreateContextAsync(db);
        var repository = new AuditRepository(context);

        await SeedTestDataAsync(context);
        var clientId = context.AuditEntries.First(a => a.EntityType == "Client").EntityId;

        var result = await repository.QueryByEntityAsync("Client", clientId, 1, 10, default);

        Assert.All(result.Items, item =>
        {
            // AuditEntryResult should have basic metadata but no RawEventPayload
            Assert.NotEqual(default, item.AuditEntryId);
            Assert.NotNull(item.EntityType);
            Assert.NotNull(item.Action);
            // RawEventPayload property doesn't exist on AuditEntryResult
        });
    }

    [Fact]
    public async Task QueryByEntity_IncludesSafeFields()
    {
        var db = nameof(QueryByEntity_IncludesSafeFields);
        await using var context = await CreateContextAsync(db);
        var repository = new AuditRepository(context);

        await SeedTestDataAsync(context);
        var clientId = context.AuditEntries.First(a => a.EntityType == "Client").EntityId;

        var result = await repository.QueryByEntityAsync("Client", clientId, 1, 10, default);

        var item = result.Items.First();
        Assert.NotEqual(default, item.AuditEntryId);
        Assert.NotNull(item.EntityType);
        Assert.NotEqual(default, item.EntityId);
        Assert.NotNull(item.Action);
        Assert.NotNull(item.ActionCategory);
        Assert.NotNull(item.ActorType);
        Assert.NotNull(item.SourceService);
        Assert.NotEqual(default, item.OccurredAtUtc);
        Assert.NotEqual(default, item.AuditedAtUtc);
        Assert.NotNull(item.TraceId);
        Assert.NotNull(item.CorrelationId);
        Assert.NotNull(item.ChangedFields);
        // PreviousValues/NewValues/SummaryDescription/CausationId may be null
    }

    // Tests: Input Validation

    [Fact]
    public async Task QueryByEntity_ThrowsOnNullEntityType()
    {
        var db = nameof(QueryByEntity_ThrowsOnNullEntityType);
        await using var context = await CreateContextAsync(db);
        var repository = new AuditRepository(context);

        await SeedTestDataAsync(context);

        await Assert.ThrowsAsync<ArgumentException>(
            () => repository.QueryByEntityAsync(null!, Guid.NewGuid(), 1, 10, default));
    }

    [Fact]
    public async Task QueryByEntity_ThrowsOnEmptyEntityId()
    {
        var db = nameof(QueryByEntity_ThrowsOnEmptyEntityId);
        await using var context = await CreateContextAsync(db);
        var repository = new AuditRepository(context);

        await SeedTestDataAsync(context);

        await Assert.ThrowsAsync<ArgumentException>(
            () => repository.QueryByEntityAsync("Client", Guid.Empty, 1, 10, default));
    }

    [Fact]
    public async Task QueryByEntity_ThrowsOnInvalidPageNumber()
    {
        var db = nameof(QueryByEntity_ThrowsOnInvalidPageNumber);
        await using var context = await CreateContextAsync(db);
        var repository = new AuditRepository(context);

        await SeedTestDataAsync(context);

        await Assert.ThrowsAsync<ArgumentException>(
            () => repository.QueryByEntityAsync("Client", Guid.NewGuid(), 0, 10, default));
    }

    [Fact]
    public async Task QueryByTraceOrCorrelation_ThrowsWhenBothAreEmpty()
    {
        var db = nameof(QueryByTraceOrCorrelation_ThrowsWhenBothAreEmpty);
        await using var context = await CreateContextAsync(db);
        var repository = new AuditRepository(context);

        await SeedTestDataAsync(context);

        await Assert.ThrowsAsync<ArgumentException>(
            () => repository.QueryByTraceOrCorrelationIdAsync(null, null, 1, 10, default));
    }
}

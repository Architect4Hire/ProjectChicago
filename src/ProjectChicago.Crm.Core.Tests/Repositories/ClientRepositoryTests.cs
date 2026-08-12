using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ProjectChicago.Crm.Core.Models.DataModels.Entities;
using ProjectChicago.Crm.Core.Persistence;
using ProjectChicago.Crm.Core.Repositories;
using ProjectChicago.Crm.Core.Tests.Persistence;
using Xunit;

namespace ProjectChicago.Crm.Core.Tests.Repositories;

// Real SQL Server integration tests for ClientRepository (CLIENT-001/CLIENT-004, DATA-004/DATA-005).
// Each test gets its own database inside the shared container (see MsSqlContainerFixture) so tests
// never interfere with each other despite sharing one running SQL Server instance.
public class ClientRepositoryTests : IClassFixture<MsSqlContainerFixture>
{
    private readonly MsSqlContainerFixture _fixture;

    public ClientRepositoryTests(MsSqlContainerFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<CrmDbContext> CreateContextAsync(string databaseName)
    {
        var builder = new SqlConnectionStringBuilder(_fixture.ConnectionString)
        {
            InitialCatalog = databaseName,
        };

        var options = new DbContextOptionsBuilder<CrmDbContext>()
            .UseSqlServer(builder.ConnectionString)
            .Options;

        var context = new CrmDbContext(options);
        await context.Database.EnsureCreatedAsync();
        return context;
    }

    private static readonly DateTime CreatedAtUtc = new(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);

    private static Client CreateClient(
        string name,
        string? primaryEmail = null,
        string? primaryPhone = null) =>
        Client.Create(
            id: Guid.NewGuid(),
            name: name,
            lifecycleStatus: ClientLifecycleStatus.Lead,
            ownerUserId: "owner-1",
            createdBy: "creator-1",
            createdAtUtc: CreatedAtUtc,
            primaryEmail: primaryEmail,
            primaryPhone: primaryPhone);

    [Fact]
    public async Task InsertAsync_StagesTheClient_AndItIsPersistedOnceSaveChangesIsCalled()
    {
        var db = nameof(InsertAsync_StagesTheClient_AndItIsPersistedOnceSaveChangesIsCalled);
        await using var context = await CreateContextAsync(db);
        var repository = new ClientRepository(context);
        var client = CreateClient("Acme Corporation", "jane@acme.example", "+1-555-0100");

        await repository.InsertAsync(client, CancellationToken.None);
        await context.SaveChangesAsync();

        await using var verifyContext = await CreateContextAsync(db);
        var persisted = await verifyContext.Clients.SingleAsync(c => c.Id == client.Id);
        Assert.Equal("Acme Corporation", persisted.Name);
        Assert.Equal("jane@acme.example", persisted.PrimaryEmail);
        Assert.Equal("+1-555-0100", persisted.PrimaryPhone);
    }

    [Fact]
    public async Task InsertAsync_DoesNotPersistAnything_UntilSaveChangesIsCalled()
    {
        var db = nameof(InsertAsync_DoesNotPersistAnything_UntilSaveChangesIsCalled);
        await using var context = await CreateContextAsync(db);
        var repository = new ClientRepository(context);
        var client = CreateClient("Acme Corporation");

        await repository.InsertAsync(client, CancellationToken.None);

        await using var verifyContext = await CreateContextAsync(db);
        Assert.False(await verifyContext.Clients.AnyAsync(c => c.Id == client.Id));
    }

    [Fact]
    public async Task FindDuplicateCandidatesAsync_MatchesOnName()
    {
        var db = nameof(FindDuplicateCandidatesAsync_MatchesOnName);
        await using var context = await CreateContextAsync(db);
        var match = CreateClient("Acme Corporation");
        var unrelated = CreateClient("Globex Corporation");
        context.Clients.AddRange(match, unrelated);
        await context.SaveChangesAsync();

        var repository = new ClientRepository(context);
        var candidates = await repository.FindDuplicateCandidatesAsync(
            normalizedName: "Acme Corporation", normalizedEmail: null, normalizedPhone: null, CancellationToken.None);

        var candidate = Assert.Single(candidates);
        Assert.Equal(match.Id, candidate.Id);
    }

    [Fact]
    public async Task FindDuplicateCandidatesAsync_MatchesOnEmail()
    {
        var db = nameof(FindDuplicateCandidatesAsync_MatchesOnEmail);
        await using var context = await CreateContextAsync(db);
        var match = CreateClient("Acme Corporation", primaryEmail: "jane@acme.example");
        var unrelated = CreateClient("Globex Corporation", primaryEmail: "hank@globex.example");
        context.Clients.AddRange(match, unrelated);
        await context.SaveChangesAsync();

        var repository = new ClientRepository(context);
        var candidates = await repository.FindDuplicateCandidatesAsync(
            normalizedName: null, normalizedEmail: "jane@acme.example", normalizedPhone: null, CancellationToken.None);

        var candidate = Assert.Single(candidates);
        Assert.Equal(match.Id, candidate.Id);
    }

    [Fact]
    public async Task FindDuplicateCandidatesAsync_MatchesOnPhone()
    {
        var db = nameof(FindDuplicateCandidatesAsync_MatchesOnPhone);
        await using var context = await CreateContextAsync(db);
        var match = CreateClient("Acme Corporation", primaryPhone: "+1-555-0100");
        var unrelated = CreateClient("Globex Corporation", primaryPhone: "+1-555-0199");
        context.Clients.AddRange(match, unrelated);
        await context.SaveChangesAsync();

        var repository = new ClientRepository(context);
        var candidates = await repository.FindDuplicateCandidatesAsync(
            normalizedName: null, normalizedEmail: null, normalizedPhone: "+1-555-0100", CancellationToken.None);

        var candidate = Assert.Single(candidates);
        Assert.Equal(match.Id, candidate.Id);
    }

    [Fact]
    public async Task FindDuplicateCandidatesAsync_MatchingOnMultipleFieldsReturnsTheClientOnce()
    {
        var db = nameof(FindDuplicateCandidatesAsync_MatchingOnMultipleFieldsReturnsTheClientOnce);
        await using var context = await CreateContextAsync(db);
        var match = CreateClient("Acme Corporation", primaryEmail: "jane@acme.example", primaryPhone: "+1-555-0100");
        context.Clients.Add(match);
        await context.SaveChangesAsync();

        var repository = new ClientRepository(context);
        var candidates = await repository.FindDuplicateCandidatesAsync(
            normalizedName: "Acme Corporation",
            normalizedEmail: "jane@acme.example",
            normalizedPhone: "+1-555-0100",
            CancellationToken.None);

        var candidate = Assert.Single(candidates);
        Assert.Equal(match.Id, candidate.Id);
    }

    [Fact]
    public async Task FindDuplicateCandidatesAsync_ReturnsUnrelatedClientsThatMatchNoCriteria()
    {
        var db = nameof(FindDuplicateCandidatesAsync_ReturnsUnrelatedClientsThatMatchNoCriteria);
        await using var context = await CreateContextAsync(db);
        var unrelated = CreateClient("Globex Corporation", primaryEmail: "hank@globex.example", primaryPhone: "+1-555-0199");
        context.Clients.Add(unrelated);
        await context.SaveChangesAsync();

        var repository = new ClientRepository(context);
        var candidates = await repository.FindDuplicateCandidatesAsync(
            normalizedName: "Acme Corporation", normalizedEmail: "jane@acme.example", normalizedPhone: "+1-555-0100", CancellationToken.None);

        Assert.Empty(candidates);
    }

    [Fact]
    public async Task FindDuplicateCandidatesAsync_WithNoCriteriaSupplied_ReturnsNoCandidatesWithoutQueryingTheDatabase()
    {
        var db = nameof(FindDuplicateCandidatesAsync_WithNoCriteriaSupplied_ReturnsNoCandidatesWithoutQueryingTheDatabase);
        await using var context = await CreateContextAsync(db);
        context.Clients.Add(CreateClient("Acme Corporation"));
        await context.SaveChangesAsync();

        var repository = new ClientRepository(context);
        var candidates = await repository.FindDuplicateCandidatesAsync(
            normalizedName: null, normalizedEmail: null, normalizedPhone: null, CancellationToken.None);

        Assert.Empty(candidates);
    }
}

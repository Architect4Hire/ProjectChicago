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

    private static Client CreateListClient(
        string name,
        ClientLifecycleStatus lifecycleStatus = ClientLifecycleStatus.Lead,
        string ownerUserId = "owner-1",
        string? primaryContactName = null,
        string? primaryEmail = null,
        string? primaryPhone = null,
        DateTime? createdAtUtc = null) =>
        Client.Create(
            id: Guid.NewGuid(),
            name: name,
            lifecycleStatus: lifecycleStatus,
            ownerUserId: ownerUserId,
            createdBy: "creator-1",
            createdAtUtc: createdAtUtc ?? CreatedAtUtc,
            primaryContactName: primaryContactName,
            primaryEmail: primaryEmail,
            primaryPhone: primaryPhone);

    private static ClientListFilter Filter(
        string? search = null,
        ClientLifecycleStatus? lifecycleStatus = null,
        string? ownerUserId = null,
        bool? isActive = null,
        ClientListSortField sortBy = ClientListSortField.Name,
        ClientListSortDirection sortDirection = ClientListSortDirection.Ascending,
        int page = 1,
        int pageSize = 25) =>
        new()
        {
            Search = search,
            LifecycleStatus = lifecycleStatus,
            OwnerUserId = ownerUserId,
            IsActive = isActive,
            SortBy = sortBy,
            SortDirection = sortDirection,
            Page = page,
            PageSize = pageSize,
        };

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

    // --- GetForUpdateAsync (CLIENT-010..015, DATA-008) ---

    [Fact]
    public async Task GetForUpdateAsync_ReturnsTheClientWithItsCurrentRowVersion()
    {
        var db = nameof(GetForUpdateAsync_ReturnsTheClientWithItsCurrentRowVersion);
        await using var context = await CreateContextAsync(db);
        var repository = new ClientRepository(context);
        var client = CreateClient("Acme Corporation");
        await repository.InsertAsync(client, CancellationToken.None);
        await context.SaveChangesAsync();

        await using var updateContext = await CreateContextAsync(db);
        var updateRepository = new ClientRepository(updateContext);
        var loaded = await updateRepository.GetForUpdateAsync(client.Id, CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Equal(client.Id, loaded!.Id);
        Assert.NotEmpty(loaded.RowVersion);
    }

    [Fact]
    public async Task GetForUpdateAsync_WhenTheClientDoesNotExist_ReturnsNull()
    {
        var db = nameof(GetForUpdateAsync_WhenTheClientDoesNotExist_ReturnsNull);
        await using var context = await CreateContextAsync(db);
        var repository = new ClientRepository(context);

        var loaded = await repository.GetForUpdateAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Null(loaded);
    }

    [Fact]
    public async Task GetForUpdateAsync_ReturnsATrackedInstance_ThatSaveChangesPersistsWhenMutated()
    {
        // Unlike ListAsync/GetDetailAsync (AsNoTracking), GetForUpdateAsync must return a tracked
        // instance so a Business-layer mutation (Client.ChangeLifecycleStatus) followed by
        // SaveChangesAsync on the same context actually reaches the database - this is the
        // mechanism ClientData.SaveLifecycleChangeAsync relies on.
        var db = nameof(GetForUpdateAsync_ReturnsATrackedInstance_ThatSaveChangesPersistsWhenMutated);
        await using var context = await CreateContextAsync(db);
        var repository = new ClientRepository(context);
        var client = CreateClient("Acme Corporation");
        await repository.InsertAsync(client, CancellationToken.None);
        await context.SaveChangesAsync();

        await using var updateContext = await CreateContextAsync(db);
        var updateRepository = new ClientRepository(updateContext);
        var loaded = await updateRepository.GetForUpdateAsync(client.Id, CancellationToken.None);
        loaded!.ChangeLifecycleStatus(ClientLifecycleStatus.Active, "modifier-1", CreatedAtUtc.AddDays(1));
        await updateContext.SaveChangesAsync();

        await using var verifyContext = await CreateContextAsync(db);
        var persisted = await verifyContext.Clients.SingleAsync(c => c.Id == client.Id);
        Assert.Equal(ClientLifecycleStatus.Active, persisted.LifecycleStatus);
        Assert.Equal("modifier-1", persisted.LastModifiedBy);
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

    // -- ListAsync: CLIENT-021 search --

    [Fact]
    public async Task ListAsync_SearchMatchesName()
    {
        var db = nameof(ListAsync_SearchMatchesName);
        await using var context = await CreateContextAsync(db);
        var match = CreateListClient("Acme Corporation");
        var unrelated = CreateListClient("Globex Corporation");
        context.Clients.AddRange(match, unrelated);
        await context.SaveChangesAsync();

        var repository = new ClientRepository(context);
        var result = await repository.ListAsync(Filter(search: "Acme"), CancellationToken.None);

        var item = Assert.Single(result.Items);
        Assert.Equal(match.Id, item.Id);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task ListAsync_SearchMatchesPrimaryContactName()
    {
        var db = nameof(ListAsync_SearchMatchesPrimaryContactName);
        await using var context = await CreateContextAsync(db);
        var match = CreateListClient("Acme Corporation", primaryContactName: "Jane Doe");
        var unrelated = CreateListClient("Globex Corporation", primaryContactName: "Hank Scorpio");
        context.Clients.AddRange(match, unrelated);
        await context.SaveChangesAsync();

        var repository = new ClientRepository(context);
        var result = await repository.ListAsync(Filter(search: "Jane"), CancellationToken.None);

        var item = Assert.Single(result.Items);
        Assert.Equal(match.Id, item.Id);
    }

    [Fact]
    public async Task ListAsync_SearchMatchesEmail()
    {
        var db = nameof(ListAsync_SearchMatchesEmail);
        await using var context = await CreateContextAsync(db);
        var match = CreateListClient("Acme Corporation", primaryEmail: "jane@acme.example");
        var unrelated = CreateListClient("Globex Corporation", primaryEmail: "hank@globex.example");
        context.Clients.AddRange(match, unrelated);
        await context.SaveChangesAsync();

        var repository = new ClientRepository(context);
        var result = await repository.ListAsync(Filter(search: "jane@acme"), CancellationToken.None);

        var item = Assert.Single(result.Items);
        Assert.Equal(match.Id, item.Id);
    }

    [Fact]
    public async Task ListAsync_SearchMatchesPhone()
    {
        var db = nameof(ListAsync_SearchMatchesPhone);
        await using var context = await CreateContextAsync(db);
        var match = CreateListClient("Acme Corporation", primaryPhone: "+1-555-0100");
        var unrelated = CreateListClient("Globex Corporation", primaryPhone: "+1-555-0199");
        context.Clients.AddRange(match, unrelated);
        await context.SaveChangesAsync();

        var repository = new ClientRepository(context);
        var result = await repository.ListAsync(Filter(search: "0100"), CancellationToken.None);

        var item = Assert.Single(result.Items);
        Assert.Equal(match.Id, item.Id);
    }

    [Fact]
    public async Task ListAsync_WithNoSearchTerm_ReturnsEveryNonArchivedClient()
    {
        var db = nameof(ListAsync_WithNoSearchTerm_ReturnsEveryNonArchivedClient);
        await using var context = await CreateContextAsync(db);
        context.Clients.AddRange(CreateListClient("Acme Corporation"), CreateListClient("Globex Corporation"));
        await context.SaveChangesAsync();

        var repository = new ClientRepository(context);
        var result = await repository.ListAsync(Filter(), CancellationToken.None);

        Assert.Equal(2, result.TotalCount);
    }

    // -- ListAsync: CLIENT-022 filters --

    [Fact]
    public async Task ListAsync_FiltersByLifecycleStatus()
    {
        var db = nameof(ListAsync_FiltersByLifecycleStatus);
        await using var context = await CreateContextAsync(db);
        var match = CreateListClient("Acme Corporation", lifecycleStatus: ClientLifecycleStatus.Prospect);
        var unrelated = CreateListClient("Globex Corporation", lifecycleStatus: ClientLifecycleStatus.Active);
        context.Clients.AddRange(match, unrelated);
        await context.SaveChangesAsync();

        var repository = new ClientRepository(context);
        var result = await repository.ListAsync(
            Filter(lifecycleStatus: ClientLifecycleStatus.Prospect), CancellationToken.None);

        var item = Assert.Single(result.Items);
        Assert.Equal(match.Id, item.Id);
    }

    [Fact]
    public async Task ListAsync_FiltersByOwnerUserId()
    {
        var db = nameof(ListAsync_FiltersByOwnerUserId);
        await using var context = await CreateContextAsync(db);
        var match = CreateListClient("Acme Corporation", ownerUserId: "owner-a");
        var unrelated = CreateListClient("Globex Corporation", ownerUserId: "owner-b");
        context.Clients.AddRange(match, unrelated);
        await context.SaveChangesAsync();

        var repository = new ClientRepository(context);
        var result = await repository.ListAsync(Filter(ownerUserId: "owner-a"), CancellationToken.None);

        var item = Assert.Single(result.Items);
        Assert.Equal(match.Id, item.Id);
    }

    [Fact]
    public async Task ListAsync_IsActiveTrue_ExcludesArchivedClients()
    {
        var db = nameof(ListAsync_IsActiveTrue_ExcludesArchivedClients);
        await using var context = await CreateContextAsync(db);
        var match = CreateListClient("Acme Corporation", lifecycleStatus: ClientLifecycleStatus.Active);
        var archived = CreateListClient("Globex Corporation", lifecycleStatus: ClientLifecycleStatus.Archived);
        context.Clients.AddRange(match, archived);
        await context.SaveChangesAsync();

        var repository = new ClientRepository(context);
        var result = await repository.ListAsync(Filter(isActive: true), CancellationToken.None);

        var item = Assert.Single(result.Items);
        Assert.Equal(match.Id, item.Id);
    }

    [Fact]
    public async Task ListAsync_IsActiveFalse_ReturnsOnlyArchivedClients()
    {
        var db = nameof(ListAsync_IsActiveFalse_ReturnsOnlyArchivedClients);
        await using var context = await CreateContextAsync(db);
        var active = CreateListClient("Acme Corporation", lifecycleStatus: ClientLifecycleStatus.Active);
        var archived = CreateListClient("Globex Corporation", lifecycleStatus: ClientLifecycleStatus.Archived);
        context.Clients.AddRange(active, archived);
        await context.SaveChangesAsync();

        var repository = new ClientRepository(context);
        var result = await repository.ListAsync(Filter(isActive: false), CancellationToken.None);

        var item = Assert.Single(result.Items);
        Assert.Equal(archived.Id, item.Id);
    }

    // -- ListAsync: CLIENT-013 archived-default exclusion --

    [Fact]
    public async Task ListAsync_WithNoFiltersApplied_ExcludesArchivedClientsByDefault()
    {
        var db = nameof(ListAsync_WithNoFiltersApplied_ExcludesArchivedClientsByDefault);
        await using var context = await CreateContextAsync(db);
        var active = CreateListClient("Acme Corporation", lifecycleStatus: ClientLifecycleStatus.Active);
        var archived = CreateListClient("Globex Corporation", lifecycleStatus: ClientLifecycleStatus.Archived);
        context.Clients.AddRange(active, archived);
        await context.SaveChangesAsync();

        var repository = new ClientRepository(context);
        var result = await repository.ListAsync(Filter(), CancellationToken.None);

        var item = Assert.Single(result.Items);
        Assert.Equal(active.Id, item.Id);
    }

    [Fact]
    public async Task ListAsync_ExplicitlyFilteringByArchivedLifecycleStatus_ReturnsArchivedClients()
    {
        var db = nameof(ListAsync_ExplicitlyFilteringByArchivedLifecycleStatus_ReturnsArchivedClients);
        await using var context = await CreateContextAsync(db);
        var active = CreateListClient("Acme Corporation", lifecycleStatus: ClientLifecycleStatus.Active);
        var archived = CreateListClient("Globex Corporation", lifecycleStatus: ClientLifecycleStatus.Archived);
        context.Clients.AddRange(active, archived);
        await context.SaveChangesAsync();

        var repository = new ClientRepository(context);
        var result = await repository.ListAsync(
            Filter(lifecycleStatus: ClientLifecycleStatus.Archived), CancellationToken.None);

        var item = Assert.Single(result.Items);
        Assert.Equal(archived.Id, item.Id);
    }

    // -- ListAsync: CLIENT-023 sort --

    [Fact]
    public async Task ListAsync_SortsByNameAscending()
    {
        var db = nameof(ListAsync_SortsByNameAscending);
        await using var context = await CreateContextAsync(db);
        context.Clients.AddRange(CreateListClient("Zephyr Inc"), CreateListClient("Acme Corporation"), CreateListClient("Mid Co"));
        await context.SaveChangesAsync();

        var repository = new ClientRepository(context);
        var result = await repository.ListAsync(
            Filter(sortBy: ClientListSortField.Name, sortDirection: ClientListSortDirection.Ascending),
            CancellationToken.None);

        Assert.Equal(["Acme Corporation", "Mid Co", "Zephyr Inc"], result.Items.Select(c => c.Name));
    }

    [Fact]
    public async Task ListAsync_SortsByNameDescending()
    {
        var db = nameof(ListAsync_SortsByNameDescending);
        await using var context = await CreateContextAsync(db);
        context.Clients.AddRange(CreateListClient("Zephyr Inc"), CreateListClient("Acme Corporation"), CreateListClient("Mid Co"));
        await context.SaveChangesAsync();

        var repository = new ClientRepository(context);
        var result = await repository.ListAsync(
            Filter(sortBy: ClientListSortField.Name, sortDirection: ClientListSortDirection.Descending),
            CancellationToken.None);

        Assert.Equal(["Zephyr Inc", "Mid Co", "Acme Corporation"], result.Items.Select(c => c.Name));
    }

    [Fact]
    public async Task ListAsync_SortsByCreatedAtUtc()
    {
        var db = nameof(ListAsync_SortsByCreatedAtUtc);
        await using var context = await CreateContextAsync(db);
        var earliest = CreateListClient("Zephyr Inc", createdAtUtc: CreatedAtUtc);
        var latest = CreateListClient("Acme Corporation", createdAtUtc: CreatedAtUtc.AddDays(2));
        var middle = CreateListClient("Mid Co", createdAtUtc: CreatedAtUtc.AddDays(1));
        context.Clients.AddRange(earliest, latest, middle);
        await context.SaveChangesAsync();

        var repository = new ClientRepository(context);
        var result = await repository.ListAsync(
            Filter(sortBy: ClientListSortField.CreatedAtUtc, sortDirection: ClientListSortDirection.Ascending),
            CancellationToken.None);

        Assert.Equal([earliest.Id, middle.Id, latest.Id], result.Items.Select(c => c.Id));
    }

    [Fact]
    public async Task ListAsync_SortsByLastModifiedAtUtc()
    {
        var db = nameof(ListAsync_SortsByLastModifiedAtUtc);
        await using var context = await CreateContextAsync(db);
        var earliest = CreateListClient("Zephyr Inc", createdAtUtc: CreatedAtUtc);
        var latest = CreateListClient("Acme Corporation", createdAtUtc: CreatedAtUtc.AddDays(2));
        context.Clients.AddRange(earliest, latest);
        await context.SaveChangesAsync();

        var repository = new ClientRepository(context);
        var result = await repository.ListAsync(
            Filter(sortBy: ClientListSortField.LastModifiedAtUtc, sortDirection: ClientListSortDirection.Descending),
            CancellationToken.None);

        Assert.Equal([latest.Id, earliest.Id], result.Items.Select(c => c.Id));
    }

    [Fact]
    public async Task ListAsync_SortsByLifecycleStatus()
    {
        var db = nameof(ListAsync_SortsByLifecycleStatus);
        await using var context = await CreateContextAsync(db);
        var lead = CreateListClient("Lead Co", lifecycleStatus: ClientLifecycleStatus.Lead);
        var active = CreateListClient("Active Co", lifecycleStatus: ClientLifecycleStatus.Active);
        context.Clients.AddRange(lead, active);
        await context.SaveChangesAsync();

        var repository = new ClientRepository(context);
        var result = await repository.ListAsync(
            Filter(sortBy: ClientListSortField.LifecycleStatus, sortDirection: ClientListSortDirection.Ascending),
            CancellationToken.None);

        Assert.Equal([lead.Id, active.Id], result.Items.Select(c => c.Id));
    }

    [Fact]
    public async Task ListAsync_WhenSortValuesTie_UsesIdAsADeterministicTieBreaker()
    {
        var db = nameof(ListAsync_WhenSortValuesTie_UsesIdAsADeterministicTieBreaker);
        await using var context = await CreateContextAsync(db);
        var clients = Enumerable.Range(0, 5).Select(_ => CreateListClient("Same Name")).ToArray();
        context.Clients.AddRange(clients);
        await context.SaveChangesAsync();

        var repository = new ClientRepository(context);
        var first = await repository.ListAsync(
            Filter(sortBy: ClientListSortField.Name, sortDirection: ClientListSortDirection.Ascending), CancellationToken.None);
        var second = await repository.ListAsync(
            Filter(sortBy: ClientListSortField.Name, sortDirection: ClientListSortDirection.Ascending), CancellationToken.None);

        Assert.Equal(first.Items.Select(c => c.Id), second.Items.Select(c => c.Id));
        Assert.Equal(clients.OrderBy(c => c.Id).Select(c => c.Id), first.Items.Select(c => c.Id));
    }

    // -- ListAsync: CLIENT-024/PERF-003 pagination boundaries --

    [Fact]
    public async Task ListAsync_ReturnsOnlyThePageSizeRequested_AndTotalCountAcrossAllPages()
    {
        var db = nameof(ListAsync_ReturnsOnlyThePageSizeRequested_AndTotalCountAcrossAllPages);
        await using var context = await CreateContextAsync(db);
        var clients = Enumerable.Range(1, 5)
            .Select(i => CreateListClient($"Client {i:00}"))
            .ToArray();
        context.Clients.AddRange(clients);
        await context.SaveChangesAsync();

        var repository = new ClientRepository(context);
        var result = await repository.ListAsync(Filter(page: 1, pageSize: 2), CancellationToken.None);

        Assert.Equal(2, result.Items.Count);
        Assert.Equal(5, result.TotalCount);
        Assert.Equal(["Client 01", "Client 02"], result.Items.Select(c => c.Name));
    }

    [Fact]
    public async Task ListAsync_ReturnsTheSecondPage()
    {
        var db = nameof(ListAsync_ReturnsTheSecondPage);
        await using var context = await CreateContextAsync(db);
        var clients = Enumerable.Range(1, 5)
            .Select(i => CreateListClient($"Client {i:00}"))
            .ToArray();
        context.Clients.AddRange(clients);
        await context.SaveChangesAsync();

        var repository = new ClientRepository(context);
        var result = await repository.ListAsync(Filter(page: 2, pageSize: 2), CancellationToken.None);

        Assert.Equal(["Client 03", "Client 04"], result.Items.Select(c => c.Name));
    }

    [Fact]
    public async Task ListAsync_ReturnsAPartialFinalPage()
    {
        var db = nameof(ListAsync_ReturnsAPartialFinalPage);
        await using var context = await CreateContextAsync(db);
        var clients = Enumerable.Range(1, 5)
            .Select(i => CreateListClient($"Client {i:00}"))
            .ToArray();
        context.Clients.AddRange(clients);
        await context.SaveChangesAsync();

        var repository = new ClientRepository(context);
        var result = await repository.ListAsync(Filter(page: 3, pageSize: 2), CancellationToken.None);

        Assert.Equal(["Client 05"], result.Items.Select(c => c.Name));
        Assert.Equal(5, result.TotalCount);
    }

    [Fact]
    public async Task ListAsync_PageBeyondTheLastPage_ReturnsNoItemsButStillReportsTotalCount()
    {
        var db = nameof(ListAsync_PageBeyondTheLastPage_ReturnsNoItemsButStillReportsTotalCount);
        await using var context = await CreateContextAsync(db);
        var clients = Enumerable.Range(1, 3)
            .Select(i => CreateListClient($"Client {i:00}"))
            .ToArray();
        context.Clients.AddRange(clients);
        await context.SaveChangesAsync();

        var repository = new ClientRepository(context);
        var result = await repository.ListAsync(Filter(page: 5, pageSize: 2), CancellationToken.None);

        Assert.Empty(result.Items);
        Assert.Equal(3, result.TotalCount);
    }

    [Fact]
    public async Task ListAsync_WithNoMatchingClients_ReturnsEmptyItemsAndZeroTotalCount()
    {
        var db = nameof(ListAsync_WithNoMatchingClients_ReturnsEmptyItemsAndZeroTotalCount);
        await using var context = await CreateContextAsync(db);

        var repository = new ClientRepository(context);
        var result = await repository.ListAsync(Filter(), CancellationToken.None);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public async Task ListAsync_ThrowsForAPageBelowOne()
    {
        var db = nameof(ListAsync_ThrowsForAPageBelowOne);
        await using var context = await CreateContextAsync(db);
        var repository = new ClientRepository(context);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => repository.ListAsync(Filter(page: 0), CancellationToken.None));
    }

    [Fact]
    public async Task ListAsync_ThrowsForAPageSizeBelowOne()
    {
        var db = nameof(ListAsync_ThrowsForAPageSizeBelowOne);
        await using var context = await CreateContextAsync(db);
        var repository = new ClientRepository(context);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => repository.ListAsync(Filter(pageSize: 0), CancellationToken.None));
    }

    // -- GetDetailAsync: CLIENT-030..032 --

    private static Project CreateProject(
        Guid clientId,
        string name,
        ProjectStatus status = ProjectStatus.Active) =>
        Project.Create(
            id: Guid.NewGuid(),
            clientId: clientId,
            name: name,
            status: status,
            priority: ProjectPriority.Normal,
            ownerUserId: "owner-1",
            createdBy: "creator-1",
            createdAtUtc: CreatedAtUtc,
            actualCompletionDateUtc: status == ProjectStatus.Completed ? CreatedAtUtc : null);

    private static TaskItem CreateTask(
        Guid projectId,
        string title,
        TaskItemStatus status = TaskItemStatus.ToDo,
        DateTime? dueDateUtc = null,
        DateTime? completedAtUtc = null) =>
        TaskItem.Create(
            id: Guid.NewGuid(),
            projectId: projectId,
            title: title,
            status: status,
            priority: TaskItemPriority.Normal,
            createdBy: "creator-1",
            createdAtUtc: CreatedAtUtc,
            dueDateUtc: dueDateUtc,
            completedAtUtc: status == TaskItemStatus.Completed ? (completedAtUtc ?? CreatedAtUtc) : completedAtUtc);

    [Fact]
    public async Task GetDetailAsync_WhenTheClientDoesNotExist_ReturnsNull()
    {
        var db = nameof(GetDetailAsync_WhenTheClientDoesNotExist_ReturnsNull);
        await using var context = await CreateContextAsync(db);
        var repository = new ClientRepository(context);

        var result = await repository.GetDetailAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetDetailAsync_WhenTheClientExistsWithNoProjects_ReturnsTheClientWithEmptyCollections()
    {
        var db = nameof(GetDetailAsync_WhenTheClientExistsWithNoProjects_ReturnsTheClientWithEmptyCollections);
        await using var context = await CreateContextAsync(db);
        var client = CreateListClient("Acme Corporation");
        context.Clients.Add(client);
        await context.SaveChangesAsync();

        var repository = new ClientRepository(context);
        var result = await repository.GetDetailAsync(client.Id, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(client.Id, result!.Client.Id);
        Assert.Empty(result.ActiveProjects);
        Assert.Empty(result.HistoricalProjects);
        Assert.Empty(result.OpenTasks);
        Assert.Empty(result.RecentlyCompletedTasks);
    }

    // CLIENT-013 excludes Archived Clients from lists by default, but detail is a direct
    // by-Id lookup, not a list - an Archived Client must still be retrievable here (DATA-021).
    [Fact]
    public async Task GetDetailAsync_ForAnArchivedClient_StillReturnsTheClient()
    {
        var db = nameof(GetDetailAsync_ForAnArchivedClient_StillReturnsTheClient);
        await using var context = await CreateContextAsync(db);
        var client = CreateListClient("Acme Corporation", lifecycleStatus: ClientLifecycleStatus.Archived);
        context.Clients.Add(client);
        await context.SaveChangesAsync();

        var repository = new ClientRepository(context);
        var result = await repository.GetDetailAsync(client.Id, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(ClientLifecycleStatus.Archived, result!.Client.LifecycleStatus);
    }

    [Fact]
    public async Task GetDetailAsync_SplitsProjectsIntoActiveAndHistoricalByStatus()
    {
        var db = nameof(GetDetailAsync_SplitsProjectsIntoActiveAndHistoricalByStatus);
        await using var context = await CreateContextAsync(db);
        var client = CreateListClient("Acme Corporation");
        var planned = CreateProject(client.Id, "Planned Project", ProjectStatus.Planned);
        var active = CreateProject(client.Id, "Active Project", ProjectStatus.Active);
        var onHold = CreateProject(client.Id, "On Hold Project", ProjectStatus.OnHold);
        var completed = CreateProject(client.Id, "Completed Project", ProjectStatus.Completed);
        var cancelled = CreateProject(client.Id, "Cancelled Project", ProjectStatus.Cancelled);
        var archived = CreateProject(client.Id, "Archived Project", ProjectStatus.Archived);
        context.Clients.Add(client);
        context.Projects.AddRange(planned, active, onHold, completed, cancelled, archived);
        await context.SaveChangesAsync();

        var repository = new ClientRepository(context);
        var result = await repository.GetDetailAsync(client.Id, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(
            [planned.Id, active.Id, onHold.Id],
            result!.ActiveProjects.Select(p => p.Id).OrderBy(id => id));
        Assert.Equal(
            [completed.Id, cancelled.Id, archived.Id],
            result.HistoricalProjects.Select(p => p.Id).OrderBy(id => id));
    }

    [Fact]
    public async Task GetDetailAsync_OnlyReturnsProjectsBelongingToTheRequestedClient()
    {
        var db = nameof(GetDetailAsync_OnlyReturnsProjectsBelongingToTheRequestedClient);
        await using var context = await CreateContextAsync(db);
        var client = CreateListClient("Acme Corporation");
        var otherClient = CreateListClient("Globex Corporation");
        var ownProject = CreateProject(client.Id, "Own Project");
        var otherProject = CreateProject(otherClient.Id, "Other Client's Project");
        context.Clients.AddRange(client, otherClient);
        context.Projects.AddRange(ownProject, otherProject);
        await context.SaveChangesAsync();

        var repository = new ClientRepository(context);
        var result = await repository.GetDetailAsync(client.Id, CancellationToken.None);

        var project = Assert.Single(result!.ActiveProjects);
        Assert.Equal(ownProject.Id, project.Id);
    }

    [Fact]
    public async Task GetDetailAsync_SplitsTasksIntoOpenAndRecentlyCompleted()
    {
        var db = nameof(GetDetailAsync_SplitsTasksIntoOpenAndRecentlyCompleted);
        await using var context = await CreateContextAsync(db);
        var client = CreateListClient("Acme Corporation");
        var project = CreateProject(client.Id, "Active Project");
        var backlog = CreateTask(project.Id, "Backlog Task", TaskItemStatus.Backlog);
        var inProgress = CreateTask(project.Id, "In Progress Task", TaskItemStatus.InProgress);
        var blocked = CreateTask(project.Id, "Blocked Task", TaskItemStatus.Blocked);
        var completed = CreateTask(project.Id, "Completed Task", TaskItemStatus.Completed);
        var cancelled = CreateTask(project.Id, "Cancelled Task", TaskItemStatus.Cancelled);
        context.Clients.Add(client);
        context.Projects.Add(project);
        context.Tasks.AddRange(backlog, inProgress, blocked, completed, cancelled);
        await context.SaveChangesAsync();

        var repository = new ClientRepository(context);
        var result = await repository.GetDetailAsync(client.Id, CancellationToken.None);

        Assert.Equal(
            [backlog.Id, inProgress.Id, blocked.Id],
            result!.OpenTasks.Select(t => t.Id).OrderBy(id => id));
        var recentlyCompleted = Assert.Single(result.RecentlyCompletedTasks);
        Assert.Equal(completed.Id, recentlyCompleted.Id);
    }

    [Fact]
    public async Task GetDetailAsync_OnlyReturnsTasksBelongingToTheRequestedClientsProjects()
    {
        var db = nameof(GetDetailAsync_OnlyReturnsTasksBelongingToTheRequestedClientsProjects);
        await using var context = await CreateContextAsync(db);
        var client = CreateListClient("Acme Corporation");
        var otherClient = CreateListClient("Globex Corporation");
        var ownProject = CreateProject(client.Id, "Own Project");
        var otherProject = CreateProject(otherClient.Id, "Other Client's Project");
        var ownTask = CreateTask(ownProject.Id, "Own Task");
        var otherTask = CreateTask(otherProject.Id, "Other Client's Task");
        context.Clients.AddRange(client, otherClient);
        context.Projects.AddRange(ownProject, otherProject);
        context.Tasks.AddRange(ownTask, otherTask);
        await context.SaveChangesAsync();

        var repository = new ClientRepository(context);
        var result = await repository.GetDetailAsync(client.Id, CancellationToken.None);

        var task = Assert.Single(result!.OpenTasks);
        Assert.Equal(ownTask.Id, task.Id);
    }

    [Fact]
    public async Task GetDetailAsync_OrdersOpenTasksByDueDateAscendingWithNullsLast()
    {
        var db = nameof(GetDetailAsync_OrdersOpenTasksByDueDateAscendingWithNullsLast);
        await using var context = await CreateContextAsync(db);
        var client = CreateListClient("Acme Corporation");
        var project = CreateProject(client.Id, "Active Project");
        var noDueDate = CreateTask(project.Id, "No Due Date", dueDateUtc: null);
        var dueLater = CreateTask(project.Id, "Due Later", dueDateUtc: CreatedAtUtc.AddDays(5));
        var dueSoonest = CreateTask(project.Id, "Due Soonest", dueDateUtc: CreatedAtUtc.AddDays(1));
        context.Clients.Add(client);
        context.Projects.Add(project);
        context.Tasks.AddRange(noDueDate, dueLater, dueSoonest);
        await context.SaveChangesAsync();

        var repository = new ClientRepository(context);
        var result = await repository.GetDetailAsync(client.Id, CancellationToken.None);

        Assert.Equal(
            [dueSoonest.Id, dueLater.Id, noDueDate.Id],
            result!.OpenTasks.Select(t => t.Id));
    }

    [Fact]
    public async Task GetDetailAsync_OrdersRecentlyCompletedTasksByCompletedAtUtcDescending()
    {
        var db = nameof(GetDetailAsync_OrdersRecentlyCompletedTasksByCompletedAtUtcDescending);
        await using var context = await CreateContextAsync(db);
        var client = CreateListClient("Acme Corporation");
        var project = CreateProject(client.Id, "Active Project");
        var completedEarliest = CreateTask(
            project.Id, "Completed Earliest", TaskItemStatus.Completed, completedAtUtc: CreatedAtUtc);
        var completedLatest = CreateTask(
            project.Id, "Completed Latest", TaskItemStatus.Completed, completedAtUtc: CreatedAtUtc.AddDays(3));
        context.Clients.Add(client);
        context.Projects.Add(project);
        context.Tasks.AddRange(completedEarliest, completedLatest);
        await context.SaveChangesAsync();

        var repository = new ClientRepository(context);
        var result = await repository.GetDetailAsync(client.Id, CancellationToken.None);

        Assert.Equal(
            [completedLatest.Id, completedEarliest.Id],
            result!.RecentlyCompletedTasks.Select(t => t.Id));
    }
}

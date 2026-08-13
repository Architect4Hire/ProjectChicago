using Microsoft.EntityFrameworkCore;
using ProjectChicago.Crm.Core.Models.DataModels.Entities;
using ProjectChicago.Crm.Core.Persistence;
using ProjectChicago.Crm.Core.Repositories;
using Xunit;

namespace ProjectChicago.Crm.Core.Tests.Repositories;

// SQL Server integration tests for IProjectRepository.ListAsync (PROJECT-020..023, PERF-001..004).
// Uses a real test database to verify that filtering, searching, sorting, and pagination work
// correctly with SQL Server query translation. Do not use EF InMemory - it does not validate SQL
// Server-specific query behavior (database.md Tests).
[Collection("ProjectRepository")]
public class ProjectRepositoryListTests
{
    private readonly CrmDbContextFactory _dbContextFactory;

    public ProjectRepositoryListTests()
    {
        _dbContextFactory = new CrmDbContextFactory();
    }

    [Fact(DisplayName = "ListAsync returns all Projects when no filter is applied")]
    public async Task ListAsync_NoFilter_ReturnsAllProjects()
    {
        // Arrange
        await using var dbContext = _dbContextFactory.CreateDbContext();
        var client = CreateClient("Client A");
        await dbContext.Clients.AddAsync(client);
        await dbContext.SaveChangesAsync();

        var project1 = CreateProject(client.Id, "Alpha Project", ProjectStatus.Active, ProjectPriority.Normal);
        var project2 = CreateProject(client.Id, "Beta Project", ProjectStatus.Planned, ProjectPriority.High);
        var project3 = CreateProject(client.Id, "Gamma Project", ProjectStatus.OnHold, ProjectPriority.Low);
        await dbContext.Projects.AddRangeAsync(project1, project2, project3);
        await dbContext.SaveChangesAsync();

        var repository = new ProjectRepository(dbContext);
        var filter = new ProjectListFilter
        {
            ClientId = Guid.Empty,
            SortBy = ProjectListSortField.Name,
            SortDirection = ProjectListSortDirection.Ascending,
            Page = 1,
            PageSize = 10,
        };

        // Act
        var result = await repository.ListAsync(filter, CancellationToken.None);

        // Assert
        Assert.Equal(3, result.TotalCount);
        Assert.Equal(3, result.Items.Count);
    }

    [Fact(DisplayName = "ListAsync filters by ClientId")]
    public async Task ListAsync_ClientIdFilter_ReturnsOnlyProjectsForSpecifiedClient()
    {
        // Arrange
        await using var dbContext = _dbContextFactory.CreateDbContext();
        var clientA = CreateClient("Client A");
        var clientB = CreateClient("Client B");
        await dbContext.Clients.AddRangeAsync(clientA, clientB);
        await dbContext.SaveChangesAsync();

        var projectA1 = CreateProject(clientA.Id, "Project A1");
        var projectA2 = CreateProject(clientA.Id, "Project A2");
        var projectB1 = CreateProject(clientB.Id, "Project B1");
        await dbContext.Projects.AddRangeAsync(projectA1, projectA2, projectB1);
        await dbContext.SaveChangesAsync();

        var repository = new ProjectRepository(dbContext);
        var filter = new ProjectListFilter
        {
            ClientId = clientA.Id,
            SortBy = ProjectListSortField.Name,
            SortDirection = ProjectListSortDirection.Ascending,
            Page = 1,
            PageSize = 10,
        };

        // Act
        var result = await repository.ListAsync(filter, CancellationToken.None);

        // Assert
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Items.Count);
        Assert.All(result.Items, p => Assert.Equal(clientA.Id, p.ClientId));
    }

    [Fact(DisplayName = "ListAsync filters by Status")]
    public async Task ListAsync_StatusFilter_ReturnsOnlyProjectsWithSpecifiedStatus()
    {
        // Arrange
        await using var dbContext = _dbContextFactory.CreateDbContext();
        var client = CreateClient("Client");
        await dbContext.Clients.AddAsync(client);
        await dbContext.SaveChangesAsync();

        var activeProject = CreateProject(client.Id, "Active", ProjectStatus.Active);
        var plannedProject = CreateProject(client.Id, "Planned", ProjectStatus.Planned);
        var completedProject = CreateProject(client.Id, "Completed", ProjectStatus.Completed);
        await dbContext.Projects.AddRangeAsync(activeProject, plannedProject, completedProject);
        await dbContext.SaveChangesAsync();

        var repository = new ProjectRepository(dbContext);
        var filter = new ProjectListFilter
        {
            ClientId = Guid.Empty,
            Status = ProjectStatus.Active,
            SortBy = ProjectListSortField.Name,
            SortDirection = ProjectListSortDirection.Ascending,
            Page = 1,
            PageSize = 10,
        };

        // Act
        var result = await repository.ListAsync(filter, CancellationToken.None);

        // Assert
        Assert.Single(result.Items);
        Assert.Equal(ProjectStatus.Active, result.Items[0].Status);
    }

    [Fact(DisplayName = "ListAsync filters by OwnerUserId")]
    public async Task ListAsync_OwnerUserIdFilter_ReturnsOnlyProjectsOwnedBySpecifiedUser()
    {
        // Arrange
        await using var dbContext = _dbContextFactory.CreateDbContext();
        var client = CreateClient("Client");
        await dbContext.Clients.AddAsync(client);
        await dbContext.SaveChangesAsync();

        var projectUser1 = CreateProject(client.Id, "User1 Project", ownerUserId: "user1");
        var projectUser2 = CreateProject(client.Id, "User2 Project", ownerUserId: "user2");
        var projectUser3 = CreateProject(client.Id, "User1 Project2", ownerUserId: "user1");
        await dbContext.Projects.AddRangeAsync(projectUser1, projectUser2, projectUser3);
        await dbContext.SaveChangesAsync();

        var repository = new ProjectRepository(dbContext);
        var filter = new ProjectListFilter
        {
            ClientId = Guid.Empty,
            OwnerUserId = "user1",
            SortBy = ProjectListSortField.Name,
            SortDirection = ProjectListSortDirection.Ascending,
            Page = 1,
            PageSize = 10,
        };

        // Act
        var result = await repository.ListAsync(filter, CancellationToken.None);

        // Assert
        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Items, p => Assert.Equal("user1", p.OwnerUserId));
    }

    [Fact(DisplayName = "ListAsync filters by Priority")]
    public async Task ListAsync_PriorityFilter_ReturnsOnlyProjectsWithSpecifiedPriority()
    {
        // Arrange
        await using var dbContext = _dbContextFactory.CreateDbContext();
        var client = CreateClient("Client");
        await dbContext.Clients.AddAsync(client);
        await dbContext.SaveChangesAsync();

        var criticalProject = CreateProject(client.Id, "Critical", priority: ProjectPriority.Critical);
        var highProject = CreateProject(client.Id, "High", priority: ProjectPriority.High);
        var normalProject = CreateProject(client.Id, "Normal", priority: ProjectPriority.Normal);
        await dbContext.Projects.AddRangeAsync(criticalProject, highProject, normalProject);
        await dbContext.SaveChangesAsync();

        var repository = new ProjectRepository(dbContext);
        var filter = new ProjectListFilter
        {
            ClientId = Guid.Empty,
            Priority = ProjectPriority.Critical,
            SortBy = ProjectListSortField.Name,
            SortDirection = ProjectListSortDirection.Ascending,
            Page = 1,
            PageSize = 10,
        };

        // Act
        var result = await repository.ListAsync(filter, CancellationToken.None);

        // Assert
        Assert.Single(result.Items);
        Assert.Equal(ProjectPriority.Critical, result.Items[0].Priority);
    }

    [Fact(DisplayName = "ListAsync searches by Project Name")]
    public async Task ListAsync_SearchByName_ReturnsProjectsMatchingNameSearch()
    {
        // Arrange
        await using var dbContext = _dbContextFactory.CreateDbContext();
        var client = CreateClient("Client");
        await dbContext.Clients.AddAsync(client);
        await dbContext.SaveChangesAsync();

        var project1 = CreateProject(client.Id, "Marketing Campaign");
        var project2 = CreateProject(client.Id, "Product Development");
        var project3 = CreateProject(client.Id, "Marketing Review");
        await dbContext.Projects.AddRangeAsync(project1, project2, project3);
        await dbContext.SaveChangesAsync();

        var repository = new ProjectRepository(dbContext);
        var filter = new ProjectListFilter
        {
            ClientId = Guid.Empty,
            Search = "Marketing",
            SortBy = ProjectListSortField.Name,
            SortDirection = ProjectListSortDirection.Ascending,
            Page = 1,
            PageSize = 10,
        };

        // Act
        var result = await repository.ListAsync(filter, CancellationToken.None);

        // Assert
        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Items, p => Assert.Contains("Marketing", p.Name));
    }

    [Fact(DisplayName = "ListAsync searches by Project Description")]
    public async Task ListAsync_SearchByDescription_ReturnsProjectsMatchingDescriptionSearch()
    {
        // Arrange
        await using var dbContext = _dbContextFactory.CreateDbContext();
        var client = CreateClient("Client");
        await dbContext.Clients.AddAsync(client);
        await dbContext.SaveChangesAsync();

        var project1 = CreateProject(client.Id, "Project 1", description: "Focus on cost reduction");
        var project2 = CreateProject(client.Id, "Project 2", description: "Build new features");
        var project3 = CreateProject(client.Id, "Project 3", description: "Cost analysis required");
        await dbContext.Projects.AddRangeAsync(project1, project2, project3);
        await dbContext.SaveChangesAsync();

        var repository = new ProjectRepository(dbContext);
        var filter = new ProjectListFilter
        {
            ClientId = Guid.Empty,
            Search = "cost",
            SortBy = ProjectListSortField.Name,
            SortDirection = ProjectListSortDirection.Ascending,
            Page = 1,
            PageSize = 10,
        };

        // Act
        var result = await repository.ListAsync(filter, CancellationToken.None);

        // Assert
        Assert.Equal(2, result.TotalCount);
    }

    [Fact(DisplayName = "ListAsync searches by Client Name")]
    public async Task ListAsync_SearchByClientName_ReturnsProjectsOfClientsMatchingSearch()
    {
        // Arrange
        await using var dbContext = _dbContextFactory.CreateDbContext();
        var clientA = CreateClient("Acme Corporation");
        var clientB = CreateClient("Beta Industries");
        await dbContext.Clients.AddRangeAsync(clientA, clientB);
        await dbContext.SaveChangesAsync();

        var projectA1 = CreateProject(clientA.Id, "Project A1");
        var projectA2 = CreateProject(clientA.Id, "Project A2");
        var projectB1 = CreateProject(clientB.Id, "Project B1");
        await dbContext.Projects.AddRangeAsync(projectA1, projectA2, projectB1);
        await dbContext.SaveChangesAsync();

        var repository = new ProjectRepository(dbContext);
        var filter = new ProjectListFilter
        {
            ClientId = Guid.Empty,
            Search = "Acme",
            SortBy = ProjectListSortField.Name,
            SortDirection = ProjectListSortDirection.Ascending,
            Page = 1,
            PageSize = 10,
        };

        // Act
        var result = await repository.ListAsync(filter, CancellationToken.None);

        // Assert
        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Items, p => Assert.Equal(clientA.Id, p.ClientId));
    }

    [Fact(DisplayName = "ListAsync sorts by Name Ascending")]
    public async Task ListAsync_SortByNameAscending_ReturnsSortedByNameAscending()
    {
        // Arrange
        await using var dbContext = _dbContextFactory.CreateDbContext();
        var client = CreateClient("Client");
        await dbContext.Clients.AddAsync(client);
        await dbContext.SaveChangesAsync();

        var projectC = CreateProject(client.Id, "Zebra Project");
        var projectA = CreateProject(client.Id, "Alpha Project");
        var projectB = CreateProject(client.Id, "Beta Project");
        await dbContext.Projects.AddRangeAsync(projectC, projectA, projectB);
        await dbContext.SaveChangesAsync();

        var repository = new ProjectRepository(dbContext);
        var filter = new ProjectListFilter
        {
            ClientId = Guid.Empty,
            SortBy = ProjectListSortField.Name,
            SortDirection = ProjectListSortDirection.Ascending,
            Page = 1,
            PageSize = 10,
        };

        // Act
        var result = await repository.ListAsync(filter, CancellationToken.None);

        // Assert
        Assert.Equal(3, result.Items.Count);
        Assert.Equal("Alpha Project", result.Items[0].Name);
        Assert.Equal("Beta Project", result.Items[1].Name);
        Assert.Equal("Zebra Project", result.Items[2].Name);
    }

    [Fact(DisplayName = "ListAsync sorts by Name Descending")]
    public async Task ListAsync_SortByNameDescending_ReturnsSortedByNameDescending()
    {
        // Arrange
        await using var dbContext = _dbContextFactory.CreateDbContext();
        var client = CreateClient("Client");
        await dbContext.Clients.AddAsync(client);
        await dbContext.SaveChangesAsync();

        var projectC = CreateProject(client.Id, "Zebra Project");
        var projectA = CreateProject(client.Id, "Alpha Project");
        var projectB = CreateProject(client.Id, "Beta Project");
        await dbContext.Projects.AddRangeAsync(projectC, projectA, projectB);
        await dbContext.SaveChangesAsync();

        var repository = new ProjectRepository(dbContext);
        var filter = new ProjectListFilter
        {
            ClientId = Guid.Empty,
            SortBy = ProjectListSortField.Name,
            SortDirection = ProjectListSortDirection.Descending,
            Page = 1,
            PageSize = 10,
        };

        // Act
        var result = await repository.ListAsync(filter, CancellationToken.None);

        // Assert
        Assert.Equal(3, result.Items.Count);
        Assert.Equal("Zebra Project", result.Items[0].Name);
        Assert.Equal("Beta Project", result.Items[1].Name);
        Assert.Equal("Alpha Project", result.Items[2].Name);
    }

    [Fact(DisplayName = "ListAsync applies server-side pagination correctly")]
    public async Task ListAsync_Pagination_ReturnsCorrectPage()
    {
        // Arrange
        await using var dbContext = _dbContextFactory.CreateDbContext();
        var client = CreateClient("Client");
        await dbContext.Clients.AddAsync(client);
        await dbContext.SaveChangesAsync();

        var projects = Enumerable.Range(1, 25)
            .Select(i => CreateProject(client.Id, $"Project {i:D2}"))
            .ToList();
        await dbContext.Projects.AddRangeAsync(projects);
        await dbContext.SaveChangesAsync();

        var repository = new ProjectRepository(dbContext);

        // Act - Page 1
        var filter1 = new ProjectListFilter
        {
            ClientId = Guid.Empty,
            SortBy = ProjectListSortField.Name,
            SortDirection = ProjectListSortDirection.Ascending,
            Page = 1,
            PageSize = 10,
        };
        var result1 = await repository.ListAsync(filter1, CancellationToken.None);

        // Act - Page 2
        var filter2 = new ProjectListFilter
        {
            ClientId = Guid.Empty,
            SortBy = ProjectListSortField.Name,
            SortDirection = ProjectListSortDirection.Ascending,
            Page = 2,
            PageSize = 10,
        };
        var result2 = await repository.ListAsync(filter2, CancellationToken.None);

        // Act - Page 3
        var filter3 = new ProjectListFilter
        {
            ClientId = Guid.Empty,
            SortBy = ProjectListSortField.Name,
            SortDirection = ProjectListSortDirection.Ascending,
            Page = 3,
            PageSize = 10,
        };
        var result3 = await repository.ListAsync(filter3, CancellationToken.None);

        // Assert
        Assert.Equal(25, result1.TotalCount);
        Assert.Equal(10, result1.Items.Count);
        Assert.Equal(10, result2.Items.Count);
        Assert.Equal(5, result3.Items.Count);

        // Verify no duplicates and proper ordering across pages
        var allItems = result1.Items.Concat(result2.Items).Concat(result3.Items).ToList();
        Assert.Equal(25, allItems.Count);
        Assert.Equal(allItems.Count, allItems.Select(p => p.Id).Distinct().Count());
    }

    [Fact(DisplayName = "ListAsync provides deterministic tie-breaking with Id")]
    public async Task ListAsync_DeterministicTieBreaker_SameNameProjects()
    {
        // Arrange
        await using var dbContext = _dbContextFactory.CreateDbContext();
        var client = CreateClient("Client");
        await dbContext.Clients.AddAsync(client);
        await dbContext.SaveChangesAsync();

        // Create projects with same name - tie-breaker should be by Id
        var project1 = CreateProject(client.Id, "Same Name");
        var project2 = CreateProject(client.Id, "Same Name");
        var project3 = CreateProject(client.Id, "Same Name");
        await dbContext.Projects.AddRangeAsync(project1, project2, project3);
        await dbContext.SaveChangesAsync();

        var repository = new ProjectRepository(dbContext);
        var filter = new ProjectListFilter
        {
            ClientId = Guid.Empty,
            SortBy = ProjectListSortField.Name,
            SortDirection = ProjectListSortDirection.Ascending,
            Page = 1,
            PageSize = 10,
        };

        // Act
        var result = await repository.ListAsync(filter, CancellationToken.None);

        // Assert
        Assert.Equal(3, result.Items.Count);
        // Verify consistent ordering by Id when primary sort is identical
        var ids = result.Items.Select(p => p.Id).ToList();
        Assert.Equal(ids, ids.OrderBy(id => id));
    }

    [Fact(DisplayName = "ListAsync throws when Page is less than 1")]
    public async Task ListAsync_PageLessThan1_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        await using var dbContext = _dbContextFactory.CreateDbContext();
        var repository = new ProjectRepository(dbContext);
        var filter = new ProjectListFilter
        {
            ClientId = Guid.Empty,
            SortBy = ProjectListSortField.Name,
            SortDirection = ProjectListSortDirection.Ascending,
            Page = 0,
            PageSize = 10,
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => repository.ListAsync(filter, CancellationToken.None));
        Assert.Contains("Page must be 1 or greater", ex.Message);
    }

    [Fact(DisplayName = "ListAsync throws when PageSize is less than 1")]
    public async Task ListAsync_PageSizeLessThan1_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        await using var dbContext = _dbContextFactory.CreateDbContext();
        var repository = new ProjectRepository(dbContext);
        var filter = new ProjectListFilter
        {
            ClientId = Guid.Empty,
            SortBy = ProjectListSortField.Name,
            SortDirection = ProjectListSortDirection.Ascending,
            Page = 1,
            PageSize = 0,
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => repository.ListAsync(filter, CancellationToken.None));
        Assert.Contains("PageSize must be 1 or greater", ex.Message);
    }

    [Fact(DisplayName = "ListAsync excludes archived Projects by default")]
    public async Task ListAsync_DefaultFilter_ExcludesArchivedProjects()
    {
        // Arrange - PROJECT-014/DATA-020: Archived Projects shall not appear in normal active lists
        await using var dbContext = _dbContextFactory.CreateDbContext();
        var client = CreateClient("Client");
        await dbContext.Clients.AddAsync(client);
        await dbContext.SaveChangesAsync();

        var activeProject = CreateProject(client.Id, "Active Project", ProjectStatus.Active);
        var completedProject = CreateProject(client.Id, "Completed Project", ProjectStatus.Completed);
        var archivedProject = CreateProject(client.Id, "Archived Project", ProjectStatus.Archived);
        await dbContext.Projects.AddRangeAsync(activeProject, completedProject, archivedProject);
        await dbContext.SaveChangesAsync();

        var repository = new ProjectRepository(dbContext);
        var filter = new ProjectListFilter
        {
            ClientId = Guid.Empty,
            SortBy = ProjectListSortField.Name,
            SortDirection = ProjectListSortDirection.Ascending,
            Page = 1,
            PageSize = 10,
            IncludeArchived = false,
        };

        // Act
        var result = await repository.ListAsync(filter, CancellationToken.None);

        // Assert
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Items.Count);
        Assert.DoesNotContain(result.Items, p => p.Status == ProjectStatus.Archived);
    }

    [Fact(DisplayName = "ListAsync includes archived Projects when explicitly requested")]
    public async Task ListAsync_IncludeArchivedTrue_IncludesArchivedProjects()
    {
        // Arrange - PROJECT-014/DATA-020: Archived Projects can be included when explicitly requested
        await using var dbContext = _dbContextFactory.CreateDbContext();
        var client = CreateClient("Client");
        await dbContext.Clients.AddAsync(client);
        await dbContext.SaveChangesAsync();

        var activeProject = CreateProject(client.Id, "Active Project", ProjectStatus.Active);
        var archivedProject = CreateProject(client.Id, "Archived Project", ProjectStatus.Archived);
        await dbContext.Projects.AddRangeAsync(activeProject, archivedProject);
        await dbContext.SaveChangesAsync();

        var repository = new ProjectRepository(dbContext);
        var filter = new ProjectListFilter
        {
            ClientId = Guid.Empty,
            SortBy = ProjectListSortField.Name,
            SortDirection = ProjectListSortDirection.Ascending,
            Page = 1,
            PageSize = 10,
            IncludeArchived = true,
        };

        // Act
        var result = await repository.ListAsync(filter, CancellationToken.None);

        // Assert
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Items.Count);
        Assert.Single(result.Items, p => p.Status == ProjectStatus.Archived);
    }

    [Fact(DisplayName = "GetAsync retrieves archived Projects for detail view")]
    public async Task GetAsync_ArchivedProject_ReturnsArchived()
    {
        // Arrange - PROJECT-014/DATA-021: Archived Projects remain available for audit and history
        await using var dbContext = _dbContextFactory.CreateDbContext();
        var client = CreateClient("Client");
        await dbContext.Clients.AddAsync(client);
        await dbContext.SaveChangesAsync();

        var archivedProject = CreateProject(client.Id, "Archived Project", ProjectStatus.Archived);
        await dbContext.Projects.AddAsync(archivedProject);
        await dbContext.SaveChangesAsync();

        var repository = new ProjectRepository(dbContext);

        // Act
        var result = await repository.GetAsync(archivedProject.Id, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(ProjectStatus.Archived, result.Status);
        Assert.Equal(archivedProject.Name, result.Name);
    }

    [Fact(DisplayName = "GetDetailAsync preserves Project history when archived")]
    public async Task GetDetailAsync_ArchivedProject_PreservesAllHistory()
    {
        // Arrange - PROJECT-014/DATA-021: Archiving is non-destructive; all history is preserved
        await using var dbContext = _dbContextFactory.CreateDbContext();
        var client = CreateClient("Client");
        await dbContext.Clients.AddAsync(client);
        await dbContext.SaveChangesAsync();

        var originalName = "Important Project";
        var originalDescription = "This project was very important";
        var originalOwner = "project-owner-1";
        var originalStartDate = DateTime.UtcNow.AddDays(-30);
        var originalTargetDate = DateTime.UtcNow.AddDays(30);
        var originalPriority = ProjectPriority.Critical;

        var archivedProject = Project.Create(
            id: Guid.NewGuid(),
            clientId: client.Id,
            name: originalName,
            status: ProjectStatus.Archived,
            priority: originalPriority,
            ownerUserId: originalOwner,
            createdBy: "test-user",
            createdAtUtc: DateTime.UtcNow.AddDays(-60),
            description: originalDescription,
            startDateUtc: originalStartDate,
            targetCompletionDateUtc: originalTargetDate);

        await dbContext.Projects.AddAsync(archivedProject);
        await dbContext.SaveChangesAsync();

        var repository = new ProjectRepository(dbContext);

        // Act
        var result = await repository.GetDetailAsync(archivedProject.Id, CancellationToken.None);

        // Assert - verify all historical data is intact
        Assert.NotNull(result);
        Assert.Equal(ProjectStatus.Archived, result.Project.Status);
        Assert.Equal(originalName, result.Project.Name);
        Assert.Equal(originalDescription, result.Project.Description);
        Assert.Equal(originalOwner, result.Project.OwnerUserId);
        Assert.Equal(originalStartDate, result.Project.StartDateUtc);
        Assert.Equal(originalTargetDate, result.Project.TargetCompletionDateUtc);
        Assert.Equal(originalPriority, result.Project.Priority);
        Assert.Equal(client.Id, result.Client.Id);
    }

    // Test helpers

    private static Client CreateClient(string name) =>
        Client.Create(
            id: Guid.NewGuid(),
            name: name,
            lifecycleStatus: ClientLifecycleStatus.Active,
            ownerUserId: "test-owner",
            createdBy: "test-user",
            createdAtUtc: DateTime.UtcNow);

    private static Project CreateProject(
        Guid clientId,
        string name,
        ProjectStatus status = ProjectStatus.Planned,
        ProjectPriority priority = ProjectPriority.Normal,
        string? description = null,
        string ownerUserId = "project-owner") =>
        Project.Create(
            id: Guid.NewGuid(),
            clientId: clientId,
            name: name,
            status: status,
            priority: priority,
            ownerUserId: ownerUserId,
            createdBy: "test-user",
            createdAtUtc: DateTime.UtcNow,
            description: description);
}

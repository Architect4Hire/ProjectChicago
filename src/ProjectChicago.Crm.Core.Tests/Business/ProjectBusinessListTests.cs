using Moq;
using ProjectChicago.Crm.Contracts.Common;
using ProjectChicago.Crm.Contracts.Projects;
using ProjectChicago.Crm.Core.Business;
using ProjectChicago.Crm.Core.Data;
using ProjectChicago.Crm.Core.Models.DataModels.Entities;
using ProjectChicago.Crm.Core.Repositories;
using Xunit;

namespace ProjectChicago.Crm.Core.Tests.Business;

// Unit tests for ProjectBusiness.ListAsync (PROJECT-020..023; onion-boundaries.md,
// backend.md Business responsibilities). Tests the translation from wire ListProjectsRequest
// to repository-facing ProjectListFilter, the Data layer call, result mapping, and default
// sort/status application.
public class ProjectBusinessListTests
{
    [Fact(DisplayName = "ListAsync translates wire request to filter with defaults")]
    public async Task ListAsync_NoSortOrStatus_AppliesDefaults()
    {
        // Arrange
        var mockData = new Mock<IProjectData>();
        var emptyResult = new ProjectListResult { Items = [], TotalCount = 0 };
        mockData.Setup(d => d.ListAsync(
                It.Is<ProjectListFilter>(f =>
                    f.SortBy == ProjectListSortField.Name &&
                    f.SortDirection == ProjectListSortDirection.Ascending),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(emptyResult);

        var business = new ProjectBusiness(mockData.Object);
        var request = new ListProjectsRequest
        {
            Page = 1,
            PageSize = 25,
        };

        // Act
        var result = await business.ListAsync(request, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0, result.TotalCount);
        mockData.Verify(d => d.ListAsync(
            It.Is<ProjectListFilter>(f =>
                f.SortBy == ProjectListSortField.Name &&
                f.SortDirection == ProjectListSortDirection.Ascending),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "ListAsync includes provided search term in filter")]
    public async Task ListAsync_SearchProvided_IncludesInFilter()
    {
        // Arrange
        var mockData = new Mock<IProjectData>();
        var emptyResult = new ProjectListResult { Items = [], TotalCount = 0 };
        mockData.Setup(d => d.ListAsync(
                It.Is<ProjectListFilter>(f => f.Search == "test search"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(emptyResult);

        var business = new ProjectBusiness(mockData.Object);
        var request = new ListProjectsRequest
        {
            Search = "test search",
            Page = 1,
            PageSize = 25,
        };

        // Act
        var result = await business.ListAsync(request, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        mockData.Verify(d => d.ListAsync(
            It.Is<ProjectListFilter>(f => f.Search == "test search"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "ListAsync includes ClientId filter when provided")]
    public async Task ListAsync_ClientIdProvided_IncludesInFilter()
    {
        // Arrange
        var clientId = Guid.NewGuid();
        var mockData = new Mock<IProjectData>();
        var emptyResult = new ProjectListResult { Items = [], TotalCount = 0 };
        mockData.Setup(d => d.ListAsync(
                It.Is<ProjectListFilter>(f => f.ClientId == clientId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(emptyResult);

        var business = new ProjectBusiness(mockData.Object);
        var request = new ListProjectsRequest
        {
            ClientId = clientId,
            Page = 1,
            PageSize = 25,
        };

        // Act
        var result = await business.ListAsync(request, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        mockData.Verify(d => d.ListAsync(
            It.Is<ProjectListFilter>(f => f.ClientId == clientId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "ListAsync translates Status contract to core status")]
    public async Task ListAsync_StatusProvided_TranslatesAndIncludesInFilter()
    {
        // Arrange
        var mockData = new Mock<IProjectData>();
        var emptyResult = new ProjectListResult { Items = [], TotalCount = 0 };
        mockData.Setup(d => d.ListAsync(
                It.Is<ProjectListFilter>(f => f.Status == ProjectStatus.Active),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(emptyResult);

        var business = new ProjectBusiness(mockData.Object);
        var request = new ListProjectsRequest
        {
            Status = ProjectStatusContract.Active,
            Page = 1,
            PageSize = 25,
        };

        // Act
        var result = await business.ListAsync(request, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        mockData.Verify(d => d.ListAsync(
            It.Is<ProjectListFilter>(f => f.Status == ProjectStatus.Active),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "ListAsync translates Priority contract to core priority")]
    public async Task ListAsync_PriorityProvided_TranslatesAndIncludesInFilter()
    {
        // Arrange
        var mockData = new Mock<IProjectData>();
        var emptyResult = new ProjectListResult { Items = [], TotalCount = 0 };
        mockData.Setup(d => d.ListAsync(
                It.Is<ProjectListFilter>(f => f.Priority == ProjectPriority.Critical),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(emptyResult);

        var business = new ProjectBusiness(mockData.Object);
        var request = new ListProjectsRequest
        {
            Priority = ProjectPriorityContract.Critical,
            Page = 1,
            PageSize = 25,
        };

        // Act
        var result = await business.ListAsync(request, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        mockData.Verify(d => d.ListAsync(
            It.Is<ProjectListFilter>(f => f.Priority == ProjectPriority.Critical),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "ListAsync includes OwnerUserId filter when provided")]
    public async Task ListAsync_OwnerUserIdProvided_IncludesInFilter()
    {
        // Arrange
        var mockData = new Mock<IProjectData>();
        var emptyResult = new ProjectListResult { Items = [], TotalCount = 0 };
        mockData.Setup(d => d.ListAsync(
                It.Is<ProjectListFilter>(f => f.OwnerUserId == "user123"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(emptyResult);

        var business = new ProjectBusiness(mockData.Object);
        var request = new ListProjectsRequest
        {
            OwnerUserId = "user123",
            Page = 1,
            PageSize = 25,
        };

        // Act
        var result = await business.ListAsync(request, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        mockData.Verify(d => d.ListAsync(
            It.Is<ProjectListFilter>(f => f.OwnerUserId == "user123"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "ListAsync translates SortBy and SortDirection")]
    public async Task ListAsync_SortProvided_TranslatesAndIncludesInFilter()
    {
        // Arrange
        var mockData = new Mock<IProjectData>();
        var emptyResult = new ProjectListResult { Items = [], TotalCount = 0 };
        mockData.Setup(d => d.ListAsync(
                It.Is<ProjectListFilter>(f =>
                    f.SortBy == ProjectListSortField.CreatedAtUtc &&
                    f.SortDirection == ProjectListSortDirection.Descending),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(emptyResult);

        var business = new ProjectBusiness(mockData.Object);
        var request = new ListProjectsRequest
        {
            SortBy = ProjectSortField.CreatedAtUtc,
            SortDirection = ProjectSortDirection.Descending,
            Page = 1,
            PageSize = 25,
        };

        // Act
        var result = await business.ListAsync(request, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        mockData.Verify(d => d.ListAsync(
            It.Is<ProjectListFilter>(f =>
                f.SortBy == ProjectListSortField.CreatedAtUtc &&
                f.SortDirection == ProjectListSortDirection.Descending),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "ListAsync maps repository result to PagedResponse")]
    public async Task ListAsync_MapsResult_ToPagedResponse()
    {
        // Arrange
        var client = CreateClient("Test Client");
        var projects = new[]
        {
            CreateProject(client.Id, "Project 1"),
            CreateProject(client.Id, "Project 2"),
            CreateProject(client.Id, "Project 3"),
        };

        var mockData = new Mock<IProjectData>();
        var repositoryResult = new ProjectListResult
        {
            Items = projects.Cast<Project>().ToList(),
            TotalCount = 5,
        };
        mockData.Setup(d => d.ListAsync(It.IsAny<ProjectListFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(repositoryResult);

        var business = new ProjectBusiness(mockData.Object);
        var request = new ListProjectsRequest
        {
            Page = 2,
            PageSize = 3,
        };

        // Act
        var result = await business.ListAsync(request, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Items.Count);
        Assert.Equal(5, result.TotalCount);
        Assert.Equal(2, result.Page);
        Assert.Equal(3, result.PageSize);
        Assert.Equal(2, result.TotalPages); // Ceiling(5 / 3) = 2
    }

    [Fact(DisplayName = "ListAsync calculates TotalPages correctly")]
    public async Task ListAsync_CalculatesTotalPages_Correctly()
    {
        // Arrange
        var mockData = new Mock<IProjectData>();
        var projects = Enumerable.Range(1, 10)
            .Select(_ => CreateProject(Guid.NewGuid(), "Project"))
            .ToList();

        var repositoryResult = new ProjectListResult
        {
            Items = projects.Cast<Project>().ToList(),
            TotalCount = 10,
        };
        mockData.Setup(d => d.ListAsync(It.IsAny<ProjectListFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(repositoryResult);

        var business = new ProjectBusiness(mockData.Object);

        // Act - TotalPages should be 2 when 10 items / 5 per page
        var result = await business.ListAsync(new ListProjectsRequest { Page = 1, PageSize = 5 }, CancellationToken.None);

        // Assert
        Assert.Equal(2, result.TotalPages);
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

    private static Project CreateProject(Guid clientId, string name) =>
        Project.Create(
            id: Guid.NewGuid(),
            clientId: clientId,
            name: name,
            status: ProjectStatus.Planned,
            priority: ProjectPriority.Normal,
            ownerUserId: "project-owner",
            createdBy: "test-user",
            createdAtUtc: DateTime.UtcNow);
}

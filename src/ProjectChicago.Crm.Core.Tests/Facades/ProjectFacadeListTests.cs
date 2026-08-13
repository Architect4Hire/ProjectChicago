using Moq;
using ProjectChicago.Crm.Contracts.Common;
using ProjectChicago.Crm.Contracts.Projects;
using ProjectChicago.Crm.Core.Business;
using ProjectChicago.Crm.Core.Facades;
using ProjectChicago.Shared.Correlation;
using Xunit;

namespace ProjectChicago.Crm.Core.Tests.Facades;

// Unit tests for ProjectFacade.ListAsync (PROJECT-020..023, SEC-012/013; onion-boundaries.md,
// backend.md Facade responsibilities). Tests authorization enforcement, request validation, and
// delegation to Business.
public class ProjectFacadeListTests
{
    [Fact(DisplayName = "ListAsync throws when actor is not authorized to list Projects")]
    public async Task ListAsync_NotAuthorized_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var mockBusiness = new Mock<IProjectBusiness>();
        var mockAuthorization = new Mock<IProjectAuthorization>();
        var mockCurrentContext = new Mock<ICurrentRequestContext>();
        var mockClock = new Mock<IClock>();

        var actor = new ActorContext { ActorId = "user1", ActorType = ActorType.User };
        var requestContext = new RequestContext
        {
            Actor = actor,
            TraceId = "trace123",
            CorrelationId = "corr123",
            CausationId = "caus123",
        };
        mockCurrentContext.Setup(c => c.Current).Returns(requestContext);

        mockAuthorization.Setup(a => a.CanListAsync(It.IsAny<ActorContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var facade = new ProjectFacade(mockBusiness.Object, mockAuthorization.Object, mockCurrentContext.Object, mockClock.Object);
        var request = new ListProjectsRequest { Page = 1, PageSize = 25 };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => facade.ListAsync(request, CancellationToken.None));
        Assert.Contains("Projects.Read", ex.Message);

        // Verify Business was never called
        mockBusiness.Verify(b => b.ListAsync(It.IsAny<ListProjectsRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "ListAsync delegates to Business when authorized")]
    public async Task ListAsync_Authorized_CallsBusiness()
    {
        // Arrange
        var mockBusiness = new Mock<IProjectBusiness>();
        var mockAuthorization = new Mock<IProjectAuthorization>();
        var mockCurrentContext = new Mock<ICurrentRequestContext>();
        var mockClock = new Mock<IClock>();

        var actor = new ActorContext { ActorId = "user1", ActorType = ActorType.User };
        var requestContext = new RequestContext
        {
            Actor = actor,
            TraceId = "trace123",
            CorrelationId = "corr123",
            CausationId = "caus123",
        };
        mockCurrentContext.Setup(c => c.Current).Returns(requestContext);

        mockAuthorization.Setup(a => a.CanListAsync(It.IsAny<ActorContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var businessResult = new PagedResponse<ProjectServiceModel>
        {
            Items = [],
            Page = 1,
            PageSize = 25,
            TotalCount = 0,
            TotalPages = 0,
        };
        mockBusiness.Setup(b => b.ListAsync(It.IsAny<ListProjectsRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(businessResult);

        var facade = new ProjectFacade(mockBusiness.Object, mockAuthorization.Object, mockCurrentContext.Object, mockClock.Object);
        var request = new ListProjectsRequest { Page = 1, PageSize = 25 };

        // Act
        var result = await facade.ListAsync(request, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(businessResult, result);
        mockBusiness.Verify(
            b => b.ListAsync(It.Is<ListProjectsRequest>(r => r.Page == 1 && r.PageSize == 25), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact(DisplayName = "ListAsync throws ValidationException when request validation fails")]
    public async Task ListAsync_InvalidRequest_ThrowsValidationException()
    {
        // Arrange
        var mockBusiness = new Mock<IProjectBusiness>();
        var mockAuthorization = new Mock<IProjectAuthorization>();
        var mockCurrentContext = new Mock<ICurrentRequestContext>();
        var mockClock = new Mock<IClock>();

        var actor = new ActorContext { ActorId = "user1", ActorType = ActorType.User };
        var requestContext = new RequestContext
        {
            Actor = actor,
            TraceId = "trace123",
            CorrelationId = "corr123",
            CausationId = "caus123",
        };
        mockCurrentContext.Setup(c => c.Current).Returns(requestContext);

        mockAuthorization.Setup(a => a.CanListAsync(It.IsAny<ActorContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var facade = new ProjectFacade(mockBusiness.Object, mockAuthorization.Object, mockCurrentContext.Object, mockClock.Object);

        // Request with invalid page (0)
        var request = new ListProjectsRequest { Page = 0, PageSize = 25 };

        // Act & Assert
        await Assert.ThrowsAsync<System.ComponentModel.DataAnnotations.ValidationException>(
            () => facade.ListAsync(request, CancellationToken.None));

        // Verify Business was never called
        mockBusiness.Verify(b => b.ListAsync(It.IsAny<ListProjectsRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "ListAsync returns Business result unchanged")]
    public async Task ListAsync_ReturnsBusinessResultUnchanged()
    {
        // Arrange
        var mockBusiness = new Mock<IProjectBusiness>();
        var mockAuthorization = new Mock<IProjectAuthorization>();
        var mockCurrentContext = new Mock<ICurrentRequestContext>();
        var mockClock = new Mock<IClock>();

        var actor = new ActorContext { ActorId = "user1", ActorType = ActorType.User };
        var requestContext = new RequestContext
        {
            Actor = actor,
            TraceId = "trace123",
            CorrelationId = "corr123",
            CausationId = "caus123",
        };
        mockCurrentContext.Setup(c => c.Current).Returns(requestContext);

        mockAuthorization.Setup(a => a.CanListAsync(It.IsAny<ActorContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var project = new ProjectServiceModel
        {
            Id = Guid.NewGuid(),
            ClientId = Guid.NewGuid(),
            Name = "Test Project",
            Status = ProjectStatusContract.Active,
            Priority = ProjectPriorityContract.High,
            OwnerUserId = "owner123",
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = "creator",
            LastModifiedAtUtc = DateTime.UtcNow,
            LastModifiedBy = "modifier",
            ConcurrencyToken = "token123",
        };

        var businessResult = new PagedResponse<ProjectServiceModel>
        {
            Items = [project],
            Page = 1,
            PageSize = 25,
            TotalCount = 1,
            TotalPages = 1,
        };
        mockBusiness.Setup(b => b.ListAsync(It.IsAny<ListProjectsRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(businessResult);

        var facade = new ProjectFacade(mockBusiness.Object, mockAuthorization.Object, mockCurrentContext.Object, mockClock.Object);
        var request = new ListProjectsRequest { Page = 1, PageSize = 25 };

        // Act
        var result = await facade.ListAsync(request, CancellationToken.None);

        // Assert
        Assert.Equal(businessResult, result);
        Assert.Single(result.Items);
        Assert.Equal(project.Id, result.Items[0].Id);
    }
}

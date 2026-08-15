using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProjectChicago.Crm.Contracts.Clients;
using ProjectChicago.Crm.Contracts.Common;
using ProjectChicago.Crm.Contracts.Projects;
using ProjectChicago.Crm.Core.Data;
using ProjectChicago.Crm.Core.Facades;
using ProjectStatusContract = ProjectChicago.Crm.Contracts.Projects.ProjectStatusContract;
using ProjectPriorityContract = ProjectChicago.Crm.Contracts.Projects.ProjectPriorityContract;
using Xunit;

namespace ProjectChicago.Crm.Api.Tests.Controllers;

// End-to-end HTTP tests for POST /api/clients/{clientId}/projects (PROJECT-001..002, API-001..007,
// SEC-010..013, ERROR-001..005) against the real Crm Program.cs composition root - proves
// ProjectsController's transport behavior (status codes, Location header, pass-through of the Facade's
// response body), not the request/response field mapping (that lives in ProjectContractMappingExtensions
// and is covered by ProjectChicago.Crm.Core.Tests). IProjectFacade is replaced with a hand-written fake
// per test (mirrors ProjectFacadeTests' fake style; no mocking library is used in this repository),
// since the production Facade->Business->Data->Repository->DbContext chain and its
// IProjectAuthorization/IClock adapters are not wired in Program.cs yet (composition-root work
// explicitly out of scope for this controller-only microstep).
public class ProjectsControllerTests
{
    private const string CrmDbConnectionStringEnvironmentVariable = "ConnectionStrings__CrmDb";
    private static readonly DateTime FixedUtcNow = new(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);

    private static CreateProjectViewModel ValidRequest(Guid? clientId = null) => new()
    {
        ClientId = clientId ?? Guid.NewGuid(),
        Name = "Website Redesign",
        OwnerUserId = "owner-1",
    };

    private static ProjectServiceModel BuildResponse(
        Guid? projectId = null,
        Guid? clientId = null,
        string name = "Website Redesign",
        string ownerUserId = "owner-1") => new()
    {
        Id = projectId ?? Guid.NewGuid(),
        ClientId = clientId ?? Guid.NewGuid(),
        Name = name,
        Status = ProjectStatusContract.Planned,
        Priority = ProjectPriorityContract.Normal,
        OwnerUserId = ownerUserId,
        CreatedAtUtc = FixedUtcNow,
        CreatedBy = "actor-1",
        LastModifiedAtUtc = FixedUtcNow,
        LastModifiedBy = "actor-1",
        ConcurrencyToken = Convert.ToBase64String([1, 2, 3, 4, 5, 6, 7, 8]),
    };

    private static ListProjectsRequest ValidListRequest(
        Guid? clientId = null,
        string? search = null,
        int? page = null,
        int? pageSize = null) => new()
    {
        ClientId = clientId,
        Search = search,
        Page = page ?? ProjectsApiContract.DefaultPage,
        PageSize = pageSize ?? ProjectsApiContract.DefaultPageSize,
    };

    // --- Success (PROJECT-001..002) ---

    [Fact]
    public async Task Create_WhenAuthenticatedAndValid_Returns201WithLocationAndTheFacadesResponseBody()
    {
        var expectedResponse = BuildResponse();
        var clientId = Guid.NewGuid();
        var request = ValidRequest(clientId: clientId) with { ClientId = clientId };
        var facade = new FakeProjectFacade { ResultToReturn = expectedResponse };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.PostAsJsonAsync(
            $"api/clients/{clientId}/projects", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal($"api/projects/{expectedResponse.Id}", response.Headers.Location?.ToString());

        var body = await response.Content.ReadFromJsonAsync<ProjectServiceModel>();
        Assert.NotNull(body);
        Assert.Equal(expectedResponse.Id, body!.Id);
        Assert.Equal(expectedResponse.Name, body.Name);
        Assert.Equal(expectedResponse.OwnerUserId, body.OwnerUserId);
        Assert.Equal(expectedResponse.Status, body.Status);
    }

    [Fact]
    public async Task Create_PassesTheBoundRequestFieldsToTheFacade()
    {
        var clientId = Guid.NewGuid();
        var request = ValidRequest(clientId: clientId);
        var facade = new FakeProjectFacade { ResultToReturn = BuildResponse() };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        await httpClient.PostAsJsonAsync($"api/clients/{clientId}/projects", request);

        Assert.True(facade.WasCalled);
        Assert.Equal(clientId, facade.ReceivedRequest?.ClientId);
        Assert.Equal("Website Redesign", facade.ReceivedRequest?.Name);
        Assert.Equal("owner-1", facade.ReceivedRequest?.OwnerUserId);
    }

    // --- Validation (SEC-022; automatic [ApiController] model-state 400) ---

    [Fact]
    public async Task Create_WhenNameIsMissing_Returns400ValidationProblemDetailsAndNeverCallsFacade()
    {
        var clientId = Guid.NewGuid();
        var facade = new FakeProjectFacade { ResultToReturn = BuildResponse() };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.PostAsJsonAsync(
            $"api/clients/{clientId}/projects",
            ValidRequest(clientId: clientId) with { Name = null! });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("validation_failed", root.GetProperty("errorCode").GetString());
        Assert.False(facade.WasCalled);
    }

    [Fact]
    public async Task Create_WhenOwnerUserIdIsMissing_Returns400ValidationProblemDetailsAndNeverCallsFacade()
    {
        var clientId = Guid.NewGuid();
        var facade = new FakeProjectFacade { ResultToReturn = BuildResponse() };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.PostAsJsonAsync(
            $"api/clients/{clientId}/projects",
            ValidRequest(clientId: clientId) with { OwnerUserId = null! });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("validation_failed", root.GetProperty("errorCode").GetString());
        Assert.False(facade.WasCalled);
    }

    // --- Missing Client (DATA-002) ---

    [Fact]
    public async Task Create_WhenFacadeThrowsProjectClientNotFoundException_Returns400BadRequest()
    {
        var clientId = Guid.NewGuid();
        var facade = new FakeProjectFacade
        {
            ExceptionToThrow = new ProjectClientNotFoundException(clientId),
        };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.PostAsJsonAsync(
            $"api/clients/{clientId}/projects", ValidRequest(clientId: clientId));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("validation_failed", root.GetProperty("errorCode").GetString());
    }

    // --- Unauthenticated (401 - coarse controller check, distinct from Facade's 403) ---

    [Fact]
    public async Task Create_WhenNoAuthenticatedActor_Returns401AndNeverCallsFacade()
    {
        var clientId = Guid.NewGuid();
        var facade = new FakeProjectFacade { ResultToReturn = BuildResponse() };
        using var factory = CreateFactory(facade, authenticated: false);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.PostAsJsonAsync(
            $"api/clients/{clientId}/projects", ValidRequest(clientId: clientId));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("authentication_required", root.GetProperty("errorCode").GetString());
        Assert.False(facade.WasCalled);
    }

    // --- Forbidden (403 - Facade/IProjectAuthorization policy rejection, SEC-012/013) ---

    [Fact]
    public async Task Create_WhenFacadeThrowsUnauthorizedAccessException_Returns403ProblemDetails()
    {
        var clientId = Guid.NewGuid();
        var facade = new FakeProjectFacade
        {
            ExceptionToThrow = new UnauthorizedAccessException("Not authorized."),
        };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.PostAsJsonAsync(
            $"api/clients/{clientId}/projects", ValidRequest(clientId: clientId));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("forbidden", root.GetProperty("errorCode").GetString());
    }

    // --- List Projects (PROJECT-020..023, API-005, SEC-012) ---

    [Fact]
    public async Task List_WhenAuthenticatedAndValid_Returns200WithPagedResponse()
    {
        var project1 = BuildResponse();
        var project2 = BuildResponse(name: "Mobile App");
        var expectedResponse = new PagedResponse<ProjectServiceModel>
        {
            Items = [project1, project2],
            Page = 1,
            PageSize = 25,
            TotalCount = 2,
            TotalPages = 1,
        };
        var facade = new FakeProjectFacade { ListResultToReturn = expectedResponse };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.GetAsync("api/projects");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PagedResponse<ProjectServiceModel>>();
        Assert.NotNull(body);
        Assert.Equal(2, body!.Items.Count);
        Assert.Equal(project1.Id, body.Items[0].Id);
        Assert.Equal(project2.Id, body.Items[1].Id);
        Assert.Equal(1, body.Page);
        Assert.Equal(25, body.PageSize);
        Assert.Equal(2, body.TotalCount);
        Assert.Equal(1, body.TotalPages);
    }

    [Fact]
    public async Task List_PassesTheBoundRequestFieldsToTheFacade()
    {
        var clientId = Guid.NewGuid();
        var request = ValidListRequest(clientId: clientId, search: "Website", page: 2, pageSize: 50);
        var facade = new FakeProjectFacade
        {
            ListResultToReturn = new PagedResponse<ProjectServiceModel>
            {
                Items = [],
                TotalCount = 0,
                Page = 2,
                PageSize = 50,
                TotalPages = 0,
            },
        };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        await httpClient.GetAsync("api/projects?clientId=" + clientId + "&search=Website&page=2&pageSize=50");

        Assert.True(facade.ListWasCalled);
        Assert.Equal(clientId, facade.ReceivedListRequest?.ClientId);
        Assert.Equal("Website", facade.ReceivedListRequest?.Search);
        Assert.Equal(2, facade.ReceivedListRequest?.Page);
        Assert.Equal(50, facade.ReceivedListRequest?.PageSize);
    }

    [Fact]
    public async Task List_WhenStatusFilterIsProvided_PassesItToTheFacade()
    {
        var facade = new FakeProjectFacade
        {
            ListResultToReturn = new PagedResponse<ProjectServiceModel>
            {
                Items = [],
                TotalCount = 0,
                Page = 1,
                PageSize = 25,
                TotalPages = 0,
            },
        };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        await httpClient.GetAsync("api/projects?status=Active");

        Assert.True(facade.ListWasCalled);
        Assert.Equal(ProjectStatusContract.Active, facade.ReceivedListRequest?.Status);
    }

    [Fact]
    public async Task List_WhenPageSizeIsInvalid_Returns400ValidationProblemDetails()
    {
        var facade = new FakeProjectFacade
        {
            ListResultToReturn = new PagedResponse<ProjectServiceModel>
            {
                Items = [],
                TotalCount = 0,
                Page = 1,
                PageSize = 25,
                TotalPages = 0,
            },
        };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.GetAsync("api/projects?pageSize=0");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("validation_failed", root.GetProperty("errorCode").GetString());
        Assert.False(facade.ListWasCalled);
    }

    [Fact]
    public async Task List_WhenPageSizeExceedsMaximum_Returns400ValidationProblemDetails()
    {
        var facade = new FakeProjectFacade
        {
            ListResultToReturn = new PagedResponse<ProjectServiceModel>
            {
                Items = [],
                TotalCount = 0,
                Page = 1,
                PageSize = 25,
                TotalPages = 0,
            },
        };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.GetAsync($"api/projects?pageSize={ProjectsApiContract.MaxPageSize + 1}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("validation_failed", root.GetProperty("errorCode").GetString());
        Assert.False(facade.ListWasCalled);
    }

    [Fact]
    public async Task List_WhenNoAuthenticatedActor_Returns401AndNeverCallsFacade()
    {
        var facade = new FakeProjectFacade
        {
            ListResultToReturn = new PagedResponse<ProjectServiceModel>
            {
                Items = [],
                TotalCount = 0,
                Page = 1,
                PageSize = 25,
                TotalPages = 0,
            },
        };
        using var factory = CreateFactory(facade, authenticated: false);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.GetAsync("api/projects");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("authentication_required", root.GetProperty("errorCode").GetString());
        Assert.False(facade.ListWasCalled);
    }

    [Fact]
    public async Task List_WhenFacadeThrowsUnauthorizedAccessException_Returns403ProblemDetails()
    {
        var facade = new FakeProjectFacade
        {
            ExceptionToThrow = new UnauthorizedAccessException("Not authorized to list."),
        };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.GetAsync("api/projects");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("forbidden", root.GetProperty("errorCode").GetString());
        Assert.False(facade.ListWasCalled);
    }

    // --- Detail Project (PROJECT-030..031, API-002, SEC-010..013) ---

    [Fact]
    public async Task GetDetail_WhenAuthenticatedAndProjectExists_Returns200WithDetailServiceModel()
    {
        var projectId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var expectedDetail = new ProjectDetailServiceModel
        {
            Project = BuildResponse(projectId: projectId, clientId: clientId),
            Client = new ClientSummary
            {
                Id = clientId,
                Name = "Test Client",
                LifecycleStatus = global::ProjectChicago.Crm.Contracts.Clients.ClientLifecycleStatusContract.Active,
                OwnerUserId = "client-owner",
            },
            OpenTasks = [],
            CompletedTasks = [],
            RecentActivityCount = 0,
        };
        var facade = new FakeProjectFacade { DetailResultToReturn = expectedDetail };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.GetAsync($"api/projects/{projectId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ProjectDetailServiceModel>();
        Assert.NotNull(body);
        Assert.Equal(expectedDetail.Project.Id, body!.Project.Id);
        Assert.Equal(expectedDetail.Client.Id, body.Client.Id);
        Assert.Equal(expectedDetail.Client.Name, body.Client.Name);
        Assert.Empty(body.OpenTasks);
        Assert.Empty(body.CompletedTasks);
        Assert.Equal(0, body.RecentActivityCount);
    }

    [Fact]
    public async Task GetDetail_PassesProjectIdToTheFacade()
    {
        var projectId = Guid.NewGuid();
        var detail = new ProjectDetailServiceModel
        {
            Project = BuildResponse(projectId: projectId),
            Client = new ClientSummary
            {
                Id = Guid.NewGuid(),
                Name = "Test",
                LifecycleStatus = global::ProjectChicago.Crm.Contracts.Clients.ClientLifecycleStatusContract.Active,
                OwnerUserId = "owner-1",
            },
            OpenTasks = [],
            CompletedTasks = [],
            RecentActivityCount = 0,
        };
        var facade = new FakeProjectFacade { DetailResultToReturn = detail };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        await httpClient.GetAsync($"api/projects/{projectId}");

        Assert.True(facade.DetailWasCalled);
        Assert.Equal(projectId, facade.ReceivedDetailProjectId);
    }

    [Fact]
    public async Task GetDetail_WhenProjectDoesNotExist_Returns404()
    {
        var projectId = Guid.NewGuid();
        var facade = new FakeProjectFacade { DetailResultToReturn = null };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.GetAsync($"api/projects/{projectId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetDetail_WhenNoAuthenticatedActor_Returns401AndNeverCallsFacade()
    {
        var projectId = Guid.NewGuid();
        var detail = new ProjectDetailServiceModel
        {
            Project = BuildResponse(),
            Client = new ClientSummary
            {
                Id = Guid.NewGuid(),
                Name = "Test",
                LifecycleStatus = global::ProjectChicago.Crm.Contracts.Clients.ClientLifecycleStatusContract.Active,
                OwnerUserId = "owner-1",
            },
            OpenTasks = [],
            CompletedTasks = [],
            RecentActivityCount = 0,
        };
        var facade = new FakeProjectFacade { DetailResultToReturn = detail };
        using var factory = CreateFactory(facade, authenticated: false);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.GetAsync($"api/projects/{projectId}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.False(facade.DetailWasCalled);
    }

    [Fact]
    public async Task GetDetail_WhenFacadeThrowsUnauthorizedAccessException_Returns403ProblemDetails()
    {
        var projectId = Guid.NewGuid();
        var facade = new FakeProjectFacade
        {
            ExceptionToThrow = new UnauthorizedAccessException("Not authorized."),
        };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.GetAsync($"api/projects/{projectId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    // --- Transition Project Status (PROJECT-010..014, API-001..007, SEC-012..013, DATA-008) ---

    [Fact]
    public async Task TransitionStatus_WhenAuthenticatedAndValid_Returns200WithUpdatedProjectServiceModel()
    {
        var projectId = Guid.NewGuid();
        var expectedResponse = BuildResponse(projectId: projectId) with
        {
            Status = ProjectStatusContract.Active,
        };
        var request = new ChangeProjectStatusViewModel
        {
            NewStatus = ProjectStatusContract.Active,
            ExpectedConcurrencyToken = Convert.ToBase64String([1, 2, 3, 4, 5, 6, 7, 8]),
            AcknowledgeOpenTasks = false,
        };
        var facade = new FakeProjectFacade { TransitionStatusResultToReturn = expectedResponse };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.PatchAsJsonAsync($"api/projects/{projectId}/status", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ProjectServiceModel>();
        Assert.NotNull(body);
        Assert.Equal(expectedResponse.Id, body!.Id);
        Assert.Equal(ProjectStatusContract.Active, body.Status);
    }

    [Fact]
    public async Task TransitionStatus_PassesTheBoundRequestFieldsToTheFacade()
    {
        var projectId = Guid.NewGuid();
        var facade = new FakeProjectFacade { TransitionStatusResultToReturn = BuildResponse() };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();
        var request = new ChangeProjectStatusViewModel
        {
            NewStatus = ProjectStatusContract.Active,
            ExpectedConcurrencyToken = "token123",
            AcknowledgeOpenTasks = true,
        };

        await httpClient.PatchAsJsonAsync($"api/projects/{projectId}/status", request);

        Assert.True(facade.TransitionStatusWasCalled);
        Assert.Equal(projectId, facade.ReceivedTransitionStatusProjectId);
        Assert.Equal(ProjectStatusContract.Active, facade.ReceivedTransitionStatusRequest?.NewStatus);
        Assert.Equal("token123", facade.ReceivedTransitionStatusRequest?.ExpectedConcurrencyToken);
        Assert.True(facade.ReceivedTransitionStatusRequest?.AcknowledgeOpenTasks);
    }

    [Fact]
    public async Task TransitionStatus_WhenNewStatusIsMissing_Returns400ValidationProblemDetails()
    {
        var projectId = Guid.NewGuid();
        var facade = new FakeProjectFacade { TransitionStatusResultToReturn = BuildResponse() };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.PatchAsJsonAsync(
            $"api/projects/{projectId}/status",
            new { expectedConcurrencyToken = "token123", acknowledgeOpenTasks = false });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.False(facade.TransitionStatusWasCalled);
    }

    [Fact]
    public async Task TransitionStatus_WhenExpectedConcurrencyTokenIsMissing_Returns400ValidationProblemDetails()
    {
        var projectId = Guid.NewGuid();
        var facade = new FakeProjectFacade { TransitionStatusResultToReturn = BuildResponse() };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.PatchAsJsonAsync(
            $"api/projects/{projectId}/status",
            new { newStatus = "Active", acknowledgeOpenTasks = false });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.False(facade.TransitionStatusWasCalled);
    }

    [Fact]
    public async Task TransitionStatus_WhenNoAuthenticatedActor_Returns401AndNeverCallsFacade()
    {
        var projectId = Guid.NewGuid();
        var facade = new FakeProjectFacade { TransitionStatusResultToReturn = BuildResponse() };
        using var factory = CreateFactory(facade, authenticated: false);
        using var httpClient = factory.CreateClient();
        var request = new ChangeProjectStatusViewModel
        {
            NewStatus = ProjectStatusContract.Active,
            ExpectedConcurrencyToken = "token123",
            AcknowledgeOpenTasks = false,
        };

        var response = await httpClient.PatchAsJsonAsync($"api/projects/{projectId}/status", request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.False(facade.TransitionStatusWasCalled);
    }

    [Fact]
    public async Task TransitionStatus_WhenFacadeThrowsUnauthorizedAccessException_Returns403ProblemDetails()
    {
        var projectId = Guid.NewGuid();
        var facade = new FakeProjectFacade
        {
            ExceptionToThrow = new UnauthorizedAccessException("Not authorized."),
        };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();
        var request = new ChangeProjectStatusViewModel
        {
            NewStatus = ProjectStatusContract.Active,
            ExpectedConcurrencyToken = "token123",
            AcknowledgeOpenTasks = false,
        };

        var response = await httpClient.PatchAsJsonAsync($"api/projects/{projectId}/status", request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task TransitionStatus_WhenProjectDoesNotExist_Returns404()
    {
        var projectId = Guid.NewGuid();
        var facade = new FakeProjectFacade { TransitionStatusResultToReturn = null };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();
        var request = new ChangeProjectStatusViewModel
        {
            NewStatus = ProjectStatusContract.Active,
            ExpectedConcurrencyToken = "token123",
            AcknowledgeOpenTasks = false,
        };

        var response = await httpClient.PatchAsJsonAsync($"api/projects/{projectId}/status", request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task TransitionStatus_WhenConcurrencyTokenIsStale_Returns409Conflict()
    {
        var projectId = Guid.NewGuid();
        var facade = new FakeProjectFacade
        {
            ExceptionToThrow = new ProjectConcurrencyConflictException(projectId),
        };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();
        var request = new ChangeProjectStatusViewModel
        {
            NewStatus = ProjectStatusContract.Active,
            ExpectedConcurrencyToken = "stale-token",
            AcknowledgeOpenTasks = false,
        };

        var response = await httpClient.PatchAsJsonAsync($"api/projects/{projectId}/status", request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task TransitionStatus_WhenInvalidStatusTransition_Returns400ValidationProblemDetails()
    {
        var projectId = Guid.NewGuid();
        var facade = new FakeProjectFacade
        {
            ExceptionToThrow = new InvalidOperationException(
                "Cannot transition Project status from Completed to Active."),
        };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();
        var request = new ChangeProjectStatusViewModel
        {
            NewStatus = ProjectStatusContract.Active,
            ExpectedConcurrencyToken = "token123",
            AcknowledgeOpenTasks = false,
        };

        var response = await httpClient.PatchAsJsonAsync($"api/projects/{projectId}/status", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task TransitionStatus_WhenOpenTasksNotAcknowledged_Returns400ValidationProblemDetails()
    {
        var projectId = Guid.NewGuid();
        var facade = new FakeProjectFacade
        {
            ExceptionToThrow = new InvalidOperationException(
                "Completing a Project requires explicit acknowledgement. Open Tasks may exist."),
        };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();
        var request = new ChangeProjectStatusViewModel
        {
            NewStatus = ProjectStatusContract.Completed,
            ExpectedConcurrencyToken = "token123",
            AcknowledgeOpenTasks = false,
        };

        var response = await httpClient.PatchAsJsonAsync($"api/projects/{projectId}/status", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("validation_failed", root.GetProperty("errorCode").GetString());
    }

    // --- Archive Project (PROJECT-014, API-001..007, SEC-012..013, DATA-008) ---

    [Fact]
    public async Task Archive_WhenAuthenticatedAndValid_Returns200WithArchivedProjectServiceModel()
    {
        var projectId = Guid.NewGuid();
        var expectedResponse = BuildResponse(projectId: projectId) with
        {
            Status = ProjectStatusContract.Archived,
        };
        var request = new ArchiveProjectViewModel
        {
            ExpectedConcurrencyToken = Convert.ToBase64String([1, 2, 3, 4, 5, 6, 7, 8]),
        };
        var facade = new FakeProjectFacade { ArchiveResultToReturn = expectedResponse };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var httpRequest = new HttpRequestMessage(HttpMethod.Delete, $"api/projects/{projectId}/archive")
        {
            Content = JsonContent.Create(request),
        };
        var response = await httpClient.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ProjectServiceModel>();
        Assert.NotNull(body);
        Assert.Equal(expectedResponse.Id, body!.Id);
        Assert.Equal(ProjectStatusContract.Archived, body.Status);
    }

    [Fact]
    public async Task Archive_PassesTheBoundRequestFieldsToTheFacade()
    {
        var projectId = Guid.NewGuid();
        var facade = new FakeProjectFacade { ArchiveResultToReturn = BuildResponse() };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();
        var token = Convert.ToBase64String([1, 2, 3, 4, 5, 6, 7, 8]);
        var request = new ArchiveProjectViewModel
        {
            ExpectedConcurrencyToken = token,
        };

        var httpRequest = new HttpRequestMessage(HttpMethod.Delete, $"api/projects/{projectId}/archive")
        {
            Content = JsonContent.Create(request),
        };
        await httpClient.SendAsync(httpRequest);

        Assert.True(facade.ArchiveWasCalled);
        Assert.Equal(projectId, facade.ReceivedArchiveProjectId);
        Assert.Equal(token, facade.ReceivedArchiveRequest?.ExpectedConcurrencyToken);
    }

    [Fact]
    public async Task Archive_WhenExpectedConcurrencyTokenIsMissing_Returns400ValidationProblemDetails()
    {
        var projectId = Guid.NewGuid();
        var facade = new FakeProjectFacade { ArchiveResultToReturn = BuildResponse() };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var httpRequest = new HttpRequestMessage(HttpMethod.Delete, $"api/projects/{projectId}/archive")
        {
            Content = JsonContent.Create(new { }),
        };
        var response = await httpClient.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.False(facade.ArchiveWasCalled);
    }

    [Fact]
    public async Task Archive_WhenProjectDoesNotExist_Returns404()
    {
        var projectId = Guid.NewGuid();
        var facade = new FakeProjectFacade { ArchiveResultToReturn = null };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();
        var request = new ArchiveProjectViewModel
        {
            ExpectedConcurrencyToken = Convert.ToBase64String([1, 2, 3, 4, 5, 6, 7, 8]),
        };

        var httpRequest = new HttpRequestMessage(HttpMethod.Delete, $"api/projects/{projectId}/archive")
        {
            Content = JsonContent.Create(request),
        };
        var response = await httpClient.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Archive_WhenNoAuthenticatedActor_Returns401AndNeverCallsFacade()
    {
        var projectId = Guid.NewGuid();
        var facade = new FakeProjectFacade { ArchiveResultToReturn = BuildResponse() };
        using var factory = CreateFactory(facade, authenticated: false);
        using var httpClient = factory.CreateClient();
        var request = new ArchiveProjectViewModel
        {
            ExpectedConcurrencyToken = "token123",
        };

        var httpRequest = new HttpRequestMessage(HttpMethod.Delete, $"api/projects/{projectId}/archive")
        {
            Content = JsonContent.Create(request),
        };
        var response = await httpClient.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.False(facade.ArchiveWasCalled);
    }

    [Fact]
    public async Task Archive_WhenFacadeThrowsUnauthorizedAccessException_Returns403ProblemDetails()
    {
        var projectId = Guid.NewGuid();
        var facade = new FakeProjectFacade
        {
            ExceptionToThrow = new UnauthorizedAccessException("Not authorized."),
        };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();
        var request = new ArchiveProjectViewModel
        {
            ExpectedConcurrencyToken = "token123",
        };

        var httpRequest = new HttpRequestMessage(HttpMethod.Delete, $"api/projects/{projectId}/archive")
        {
            Content = JsonContent.Create(request),
        };
        var response = await httpClient.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Archive_WhenConcurrencyTokenIsStale_Returns409Conflict()
    {
        var projectId = Guid.NewGuid();
        var facade = new FakeProjectFacade
        {
            ExceptionToThrow = new ProjectConcurrencyConflictException(projectId),
        };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();
        var request = new ArchiveProjectViewModel
        {
            ExpectedConcurrencyToken = "stale-token",
        };

        var httpRequest = new HttpRequestMessage(HttpMethod.Delete, $"api/projects/{projectId}/archive")
        {
            Content = JsonContent.Create(request),
        };
        var response = await httpClient.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Archive_WhenArchiveRejectedByBusiness_Returns409Conflict()
    {
        var projectId = Guid.NewGuid();
        var facade = new FakeProjectFacade
        {
            ExceptionToThrow = new InvalidOperationException("Cannot archive Project in current state."),
        };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();
        var request = new ArchiveProjectViewModel
        {
            ExpectedConcurrencyToken = "token123",
        };

        var httpRequest = new HttpRequestMessage(HttpMethod.Delete, $"api/projects/{projectId}/archive")
        {
            Content = JsonContent.Create(request),
        };
        var response = await httpClient.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    // --- Test setup ---

    private const string CrmDbConnectionStringEnvironmentVariableName = "ConnectionStrings__CrmDb";

    private static WebApplicationFactory<Program> CreateFactory(FakeProjectFacade facade, bool authenticated)
    {
        Environment.SetEnvironmentVariable(
            CrmDbConnectionStringEnvironmentVariableName,
            "Server=localhost;Database=CrmDbProjectsControllerTests;TrustServerCertificate=True;");

        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.AddScoped<IProjectFacade>(_ => facade);

                // Configure test authentication scheme for all tests (required for authorization middleware)
                services.AddAuthentication("TestScheme")
                    .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("TestScheme", _ => { });
                // Add startup filter that sets the test user with Manager role when authenticated is true
                services.AddSingleton<IStartupFilter>(new AuthenticatedActorStartupFilter(authenticated));
            }));
    }

    // Fake IProjectFacade (mirrors ProjectFacadeTests' hand-written fake style - no mocking library is
    // used in this repository).
    private sealed class FakeProjectFacade : IProjectFacade
    {
        public ProjectServiceModel ResultToReturn { get; init; } = null!;

        public PagedResponse<ProjectServiceModel>? ListResultToReturn { get; init; }

        public ProjectDetailServiceModel? DetailResultToReturn { get; init; }

        public ProjectServiceModel? TransitionStatusResultToReturn { get; init; }

        public ProjectServiceModel? ArchiveResultToReturn { get; init; }

        public Exception? ExceptionToThrow { get; init; }

        public bool WasCalled { get; private set; }

        public bool ListWasCalled { get; private set; }

        public bool DetailWasCalled { get; private set; }

        public bool TransitionStatusWasCalled { get; private set; }

        public bool ArchiveWasCalled { get; private set; }

        public CreateProjectViewModel? ReceivedRequest { get; private set; }

        public ListProjectsRequest? ReceivedListRequest { get; private set; }

        public Guid ReceivedDetailProjectId { get; private set; }

        public Guid ReceivedTransitionStatusProjectId { get; private set; }

        public ChangeProjectStatusViewModel? ReceivedTransitionStatusRequest { get; private set; }

        public Guid ReceivedArchiveProjectId { get; private set; }

        public ArchiveProjectViewModel? ReceivedArchiveRequest { get; private set; }

        public Task<ProjectServiceModel> CreateAsync(
            CreateProjectViewModel request, CancellationToken cancellationToken)
        {
            WasCalled = true;
            ReceivedRequest = request;

            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }

            return Task.FromResult(ResultToReturn);
        }

        public Task<PagedResponse<ProjectServiceModel>> ListAsync(
            ListProjectsRequest request, CancellationToken cancellationToken)
        {
            ListWasCalled = true;
            ReceivedListRequest = request;

            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }

            return Task.FromResult(ListResultToReturn ?? new PagedResponse<ProjectServiceModel>
            {
                Items = [],
                Page = 1,
                PageSize = 25,
                TotalCount = 0,
                TotalPages = 0,
            });
        }

        public Task<ProjectDetailServiceModel?> GetDetailAsync(
            Guid projectId, CancellationToken cancellationToken)
        {
            DetailWasCalled = true;
            ReceivedDetailProjectId = projectId;

            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }

            return Task.FromResult(DetailResultToReturn);
        }

        public Task<ProjectServiceModel?> TransitionStatusAsync(
            Guid projectId, ChangeProjectStatusViewModel request, CancellationToken cancellationToken)
        {
            TransitionStatusWasCalled = true;
            ReceivedTransitionStatusProjectId = projectId;
            ReceivedTransitionStatusRequest = request;

            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }

            return Task.FromResult(TransitionStatusResultToReturn);
        }

        public Task<ProjectServiceModel?> ArchiveAsync(
            Guid projectId, ArchiveProjectViewModel request, CancellationToken cancellationToken)
        {
            ArchiveWasCalled = true;
            ReceivedArchiveProjectId = projectId;
            ReceivedArchiveRequest = request;

            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }

            return Task.FromResult(ArchiveResultToReturn);
        }
    }

    // Sets HttpContext.User to a fake authenticated ClaimsPrincipal before routing, without wiring a
    // real ASP.NET Core authentication scheme into the production pipeline - the browser
    // authentication transport (cookie/JWT/etc.) remains an open decision (ADR-0018) that this
    // controller-only microstep must not silently make. Runs after the real host pipeline
    // (UseExceptionHandler/UseStatusCodePages/MapControllers) is already registered, the same pattern
    // ApiExceptionHandlingHostTests uses for its test-only throwing route.
    private sealed class AuthenticatedActorStartupFilter : IStartupFilter
    {
        private readonly bool _authenticated;

        public AuthenticatedActorStartupFilter(bool authenticated) => _authenticated = authenticated;

        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, nextMiddleware) =>
            {
                if (_authenticated)
                {
                    var identity = new ClaimsIdentity(
                        [
                            new Claim(ClaimTypes.NameIdentifier, "actor-1"),
                            new Claim(ClaimTypes.Role, "Manager"), // Grant Manager role so all tests pass policies
                        ],
                        authenticationType: "TestScheme");
                    context.User = new ClaimsPrincipal(identity);
                }
                // If not authenticated, leave context.User as null - authorization middleware will handle rejection

                return nextMiddleware();
            });

            next(app);
        };
    }

    // Test authentication handler for authorization policy testing
    private sealed class TestAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public TestAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            // Authentication is already set by AuthenticatedActorStartupFilter
            return Task.FromResult(AuthenticateResult.NoResult());
        }
    }
}

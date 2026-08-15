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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProjectChicago.Crm.Contracts.Common;
using ProjectChicago.Crm.Contracts.Tasks;
using ProjectChicago.Crm.Core.Facades;
using Xunit;

namespace ProjectChicago.Crm.Api.Tests.Controllers;

// End-to-end HTTP tests for POST /api/projects/{projectId}/tasks (TASK-001..002) and GET /api/tasks
// (TASK-020..022) against the real Crm Program.cs composition root - proves TasksController's transport
// behavior (status codes, response shapes, pass-through of Facade results), not request/response field
// mapping (that lives in TaskContractMappingExtensions and is covered by ProjectChicago.Crm.Core.Tests).
// ITaskFacade is replaced with a hand-written fake per test (mirrors ProjectFacadeTests' fake style;
// no mocking library is used in this repository), since the production Facade->Business->Data->Repository->
// DbContext chain and its ITaskAuthorization/IClock adapters are not wired in Program.cs yet
// (composition-root work explicitly out of scope for this controller-only microstep).
public class TasksControllerTests
{
    private const string CrmDbConnectionStringEnvironmentVariable = "ConnectionStrings__CrmDb";
    private static readonly DateTime FixedUtcNow = new(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);

    private static CreateTaskViewModel ValidRequest(Guid? projectId = null) => new()
    {
        ProjectId = projectId ?? Guid.NewGuid(),
        Title = "Implement Authentication",
        Status = TaskItemStatusContract.ToDo,
        Priority = TaskItemPriorityContract.High,
    };

    private static TaskServiceModel BuildResponse(
        Guid? taskId = null,
        Guid? projectId = null,
        string title = "Implement Authentication",
        TaskItemStatusContract? status = null,
        TaskItemPriorityContract? priority = null) => new()
    {
        Id = taskId ?? Guid.NewGuid(),
        ProjectId = projectId ?? Guid.NewGuid(),
        Title = title,
        Status = status ?? TaskItemStatusContract.ToDo,
        Priority = priority ?? TaskItemPriorityContract.High,
        CreatedAtUtc = FixedUtcNow,
        CreatedBy = "actor-1",
        LastModifiedAtUtc = FixedUtcNow,
        LastModifiedBy = "actor-1",
        ConcurrencyToken = Convert.ToBase64String([1, 2, 3, 4, 5, 6, 7, 8]),
    };

    // --- Success (TASK-001..002) ---

    [Fact]
    public async Task Create_WhenAuthenticatedAndValid_Returns201WithLocationAndTheFacadesResponseBody()
    {
        var expectedResponse = BuildResponse();
        var projectId = expectedResponse.ProjectId;
        var request = ValidRequest(projectId: projectId);
        var facade = new FakeTaskFacade { ResultToReturn = expectedResponse };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.PostAsJsonAsync(
            $"api/projects/{projectId}/tasks", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal($"api/tasks/{expectedResponse.Id}", response.Headers.Location?.ToString());

        var body = await response.Content.ReadFromJsonAsync<TaskServiceModel>();
        Assert.NotNull(body);
        Assert.Equal(expectedResponse.Id, body!.Id);
        Assert.Equal(expectedResponse.ProjectId, body.ProjectId);
        Assert.Equal(expectedResponse.Title, body.Title);
        Assert.Equal(expectedResponse.Status, body.Status);
        Assert.Equal(expectedResponse.Priority, body.Priority);
    }

    [Fact]
    public async Task Create_PassesTheBoundRequestFieldsToTheFacade()
    {
        var projectId = Guid.NewGuid();
        var request = ValidRequest(projectId: projectId);
        var facade = new FakeTaskFacade { ResultToReturn = BuildResponse(projectId: projectId) };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        await httpClient.PostAsJsonAsync($"api/projects/{projectId}/tasks", request);

        Assert.True(facade.WasCalled);
        Assert.Equal(projectId, facade.ReceivedRequest?.ProjectId);
        Assert.Equal("Implement Authentication", facade.ReceivedRequest?.Title);
        Assert.Equal(TaskItemStatusContract.ToDo, facade.ReceivedRequest?.Status);
        Assert.Equal(TaskItemPriorityContract.High, facade.ReceivedRequest?.Priority);
    }

    // --- Validation (SEC-022; automatic [ApiController] model-state 400) ---

    [Fact]
    public async Task Create_WhenTitleIsMissing_Returns400ValidationProblemDetailsAndNeverCallsFacade()
    {
        var projectId = Guid.NewGuid();
        var facade = new FakeTaskFacade { ResultToReturn = BuildResponse(projectId: projectId) };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.PostAsJsonAsync(
            $"api/projects/{projectId}/tasks",
            ValidRequest(projectId: projectId) with { Title = null! });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("validation_failed", root.GetProperty("errorCode").GetString());
        Assert.False(facade.WasCalled);
    }

    // --- Missing Project (DATA-003) ---

    [Fact]
    public async Task Create_WhenFacadeThrowsTaskProjectNotFoundException_Returns400BadRequest()
    {
        var projectId = Guid.NewGuid();
        var facade = new FakeTaskFacade
        {
            ExceptionToThrow = new ProjectChicago.Crm.Core.Data.TaskProjectNotFoundException(projectId),
        };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.PostAsJsonAsync(
            $"api/projects/{projectId}/tasks", ValidRequest(projectId: projectId));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("validation_failed", root.GetProperty("errorCode").GetString());
    }

    // --- Unauthenticated (401 - coarse controller check, distinct from Facade's 403) ---

    [Fact]
    public async Task Create_WhenNoAuthenticatedActor_Returns401AndNeverCallsFacade()
    {
        var projectId = Guid.NewGuid();
        var facade = new FakeTaskFacade { ResultToReturn = BuildResponse(projectId: projectId) };
        using var factory = CreateFactory(facade, authenticated: false);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.PostAsJsonAsync(
            $"api/projects/{projectId}/tasks", ValidRequest(projectId: projectId));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("authentication_required", root.GetProperty("errorCode").GetString());
        Assert.False(facade.WasCalled);
    }

    // --- Forbidden (403 - Facade/ITaskAuthorization policy rejection, SEC-012/013) ---

    [Fact]
    public async Task Create_WhenFacadeThrowsUnauthorizedAccessException_Returns403ProblemDetails()
    {
        var projectId = Guid.NewGuid();
        var facade = new FakeTaskFacade
        {
            ExceptionToThrow = new UnauthorizedAccessException("Not authorized."),
        };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.PostAsJsonAsync(
            $"api/projects/{projectId}/tasks", ValidRequest(projectId: projectId));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("forbidden", root.GetProperty("errorCode").GetString());
    }

    // --- Assign Success (TASK-013..014, DATA-008) ---

    [Fact]
    public async Task Assign_WhenAuthenticatedAndValid_Returns200WithUpdatedTaskServiceModel()
    {
        var taskId = Guid.NewGuid();
        var concurrencyToken = Convert.ToBase64String([1, 2, 3, 4, 5, 6, 7, 8]);
        var request = new AssignTaskViewModel
        {
            TaskId = taskId,
            AssignedUserId = "user-2",
            ConcurrencyToken = concurrencyToken,
        };
        var expectedResponse = BuildResponse(taskId: taskId);
        var facade = new FakeTaskFacade { ResultToReturn = expectedResponse };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.PatchAsJsonAsync($"api/tasks/{taskId}", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<TaskServiceModel>();
        Assert.NotNull(body);
        Assert.Equal(expectedResponse.Id, body!.Id);
        Assert.Equal(expectedResponse.ProjectId, body.ProjectId);
        Assert.Equal(expectedResponse.Title, body.Title);
    }

    [Fact]
    public async Task Assign_PassesTheBoundRequestFieldsToTheFacade()
    {
        var taskId = Guid.NewGuid();
        var concurrencyToken = Convert.ToBase64String([1, 2, 3, 4, 5, 6, 7, 8]);
        var request = new AssignTaskViewModel
        {
            TaskId = taskId,
            AssignedUserId = "user-2",
            ConcurrencyToken = concurrencyToken,
        };
        var facade = new FakeTaskFacade { ResultToReturn = BuildResponse(taskId: taskId) };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        await httpClient.PatchAsJsonAsync($"api/tasks/{taskId}", request);

        Assert.True(facade.WasCalled);
        Assert.Equal(taskId, facade.ReceivedAssignRequest?.TaskId);
        Assert.Equal("user-2", facade.ReceivedAssignRequest?.AssignedUserId);
        Assert.Equal(concurrencyToken, facade.ReceivedAssignRequest?.ConcurrencyToken);
    }

    // --- Assign Validation (SEC-022) ---

    [Fact]
    public async Task Assign_WhenTaskIdIsMissing_Returns400ValidationProblemDetails()
    {
        var taskId = Guid.NewGuid();
        var facade = new FakeTaskFacade { ResultToReturn = BuildResponse(taskId: taskId) };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.PatchAsJsonAsync(
            $"api/tasks/{taskId}",
            new { AssignedUserId = "user-2", ConcurrencyToken = "token" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("validation_failed", root.GetProperty("errorCode").GetString());
        Assert.False(facade.WasCalled);
    }

    [Fact]
    public async Task Assign_WhenConcurrencyTokenIsMissing_Returns400ValidationProblemDetails()
    {
        var taskId = Guid.NewGuid();
        var facade = new FakeTaskFacade { ResultToReturn = BuildResponse(taskId: taskId) };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.PatchAsJsonAsync(
            $"api/tasks/{taskId}",
            new { TaskId = taskId, AssignedUserId = "user-2" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("validation_failed", root.GetProperty("errorCode").GetString());
        Assert.False(facade.WasCalled);
    }

    // --- Assign Not Found (DATA-008) ---

    [Fact]
    public async Task Assign_WhenFacadeThrowsArgumentException_Returns400BadRequest()
    {
        var taskId = Guid.NewGuid();
        var facade = new FakeTaskFacade
        {
            ExceptionToThrow = new ArgumentException($"Task with ID '{taskId}' does not exist.", nameof(taskId)),
        };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.PatchAsJsonAsync(
            $"api/tasks/{taskId}",
            new AssignTaskViewModel
            {
                TaskId = taskId,
                AssignedUserId = "user-2",
                ConcurrencyToken = Convert.ToBase64String([1, 2, 3, 4, 5, 6, 7, 8]),
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("validation_failed", root.GetProperty("errorCode").GetString());
    }

    // --- Assign Concurrency Conflict (DATA-008) ---

    [Fact]
    public async Task Assign_WhenFacadeThrowsDbUpdateConcurrencyException_Returns409Conflict()
    {
        var taskId = Guid.NewGuid();
        var facade = new FakeTaskFacade
        {
            ExceptionToThrow = new Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException("Concurrency conflict", []),
        };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.PatchAsJsonAsync(
            $"api/tasks/{taskId}",
            new AssignTaskViewModel
            {
                TaskId = taskId,
                AssignedUserId = "user-2",
                ConcurrencyToken = Convert.ToBase64String([1, 2, 3, 4, 5, 6, 7, 8]),
            });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("concurrency_conflict", root.GetProperty("errorCode").GetString());
    }

    // --- Assign Unauthenticated (401) ---

    [Fact]
    public async Task Assign_WhenNoAuthenticatedActor_Returns401AndNeverCallsFacade()
    {
        var taskId = Guid.NewGuid();
        var facade = new FakeTaskFacade { ResultToReturn = BuildResponse(taskId: taskId) };
        using var factory = CreateFactory(facade, authenticated: false);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.PatchAsJsonAsync(
            $"api/tasks/{taskId}",
            new AssignTaskViewModel
            {
                TaskId = taskId,
                AssignedUserId = "user-2",
                ConcurrencyToken = Convert.ToBase64String([1, 2, 3, 4, 5, 6, 7, 8]),
            });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("authentication_required", root.GetProperty("errorCode").GetString());
        Assert.False(facade.WasCalled);
    }

    // --- Assign Forbidden (403) ---

    [Fact]
    public async Task Assign_WhenFacadeThrowsUnauthorizedAccessException_Returns403ProblemDetails()
    {
        var taskId = Guid.NewGuid();
        var facade = new FakeTaskFacade
        {
            ExceptionToThrow = new UnauthorizedAccessException("Not authorized."),
        };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.PatchAsJsonAsync(
            $"api/tasks/{taskId}",
            new AssignTaskViewModel
            {
                TaskId = taskId,
                AssignedUserId = "user-2",
                ConcurrencyToken = Convert.ToBase64String([1, 2, 3, 4, 5, 6, 7, 8]),
            });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("forbidden", root.GetProperty("errorCode").GetString());
    }

    // --- ChangePriority Success (TASK-015, DATA-008) ---

    [Fact]
    public async Task ChangePriority_WhenAuthenticatedAndValid_Returns200WithUpdatedTaskServiceModel()
    {
        var taskId = Guid.NewGuid();
        var concurrencyToken = Convert.ToBase64String([1, 2, 3, 4, 5, 6, 7, 8]);
        var request = new ChangeTaskPriorityViewModel
        {
            TaskId = taskId,
            Priority = TaskItemPriorityContract.Critical,
            ConcurrencyToken = concurrencyToken,
        };
        var expectedResponse = BuildResponse(taskId: taskId, priority: TaskItemPriorityContract.Critical);
        var facade = new FakeTaskFacade { ResultToReturn = expectedResponse };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.PatchAsJsonAsync($"api/tasks/{taskId}/priority", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<TaskServiceModel>();
        Assert.NotNull(body);
        Assert.Equal(expectedResponse.Id, body!.Id);
        Assert.Equal(expectedResponse.Priority, body.Priority);
    }

    [Fact]
    public async Task ChangePriority_PassesTheBoundRequestFieldsToTheFacade()
    {
        var taskId = Guid.NewGuid();
        var concurrencyToken = Convert.ToBase64String([1, 2, 3, 4, 5, 6, 7, 8]);
        var request = new ChangeTaskPriorityViewModel
        {
            TaskId = taskId,
            Priority = TaskItemPriorityContract.Critical,
            ConcurrencyToken = concurrencyToken,
        };
        var facade = new FakeTaskFacade { ResultToReturn = BuildResponse(taskId: taskId) };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        await httpClient.PatchAsJsonAsync($"api/tasks/{taskId}/priority", request);

        Assert.True(facade.WasCalled);
        Assert.Equal(taskId, facade.ReceivedChangePriorityRequest?.TaskId);
        Assert.Equal(TaskItemPriorityContract.Critical, facade.ReceivedChangePriorityRequest?.Priority);
        Assert.Equal(concurrencyToken, facade.ReceivedChangePriorityRequest?.ConcurrencyToken);
    }

    [Fact]
    public async Task ChangePriority_WhenPriorityIsLow_PassesLowToTheFacade()
    {
        var taskId = Guid.NewGuid();
        var concurrencyToken = Convert.ToBase64String([1, 2, 3, 4, 5, 6, 7, 8]);
        var request = new ChangeTaskPriorityViewModel
        {
            TaskId = taskId,
            Priority = TaskItemPriorityContract.Low,
            ConcurrencyToken = concurrencyToken,
        };
        var facade = new FakeTaskFacade { ResultToReturn = BuildResponse(taskId: taskId, priority: TaskItemPriorityContract.Low) };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        await httpClient.PatchAsJsonAsync($"api/tasks/{taskId}/priority", request);

        Assert.Equal(TaskItemPriorityContract.Low, facade.ReceivedChangePriorityRequest?.Priority);
    }

    [Fact]
    public async Task ChangePriority_WhenPriorityIsNormal_PassesNormalToTheFacade()
    {
        var taskId = Guid.NewGuid();
        var concurrencyToken = Convert.ToBase64String([1, 2, 3, 4, 5, 6, 7, 8]);
        var request = new ChangeTaskPriorityViewModel
        {
            TaskId = taskId,
            Priority = TaskItemPriorityContract.Normal,
            ConcurrencyToken = concurrencyToken,
        };
        var facade = new FakeTaskFacade { ResultToReturn = BuildResponse(taskId: taskId, priority: TaskItemPriorityContract.Normal) };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        await httpClient.PatchAsJsonAsync($"api/tasks/{taskId}/priority", request);

        Assert.Equal(TaskItemPriorityContract.Normal, facade.ReceivedChangePriorityRequest?.Priority);
    }

    [Fact]
    public async Task ChangePriority_WhenPriorityIsHigh_PassesHighToTheFacade()
    {
        var taskId = Guid.NewGuid();
        var concurrencyToken = Convert.ToBase64String([1, 2, 3, 4, 5, 6, 7, 8]);
        var request = new ChangeTaskPriorityViewModel
        {
            TaskId = taskId,
            Priority = TaskItemPriorityContract.High,
            ConcurrencyToken = concurrencyToken,
        };
        var facade = new FakeTaskFacade { ResultToReturn = BuildResponse(taskId: taskId, priority: TaskItemPriorityContract.High) };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        await httpClient.PatchAsJsonAsync($"api/tasks/{taskId}/priority", request);

        Assert.Equal(TaskItemPriorityContract.High, facade.ReceivedChangePriorityRequest?.Priority);
    }

    // --- ChangePriority Validation (SEC-022, TASK-015) ---

    [Fact]
    public async Task ChangePriority_WhenTaskIdIsMissing_Returns400ValidationProblemDetails()
    {
        var taskId = Guid.NewGuid();
        var facade = new FakeTaskFacade { ResultToReturn = BuildResponse(taskId: taskId) };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.PatchAsJsonAsync(
            $"api/tasks/{taskId}/priority",
            new { Priority = "Critical", ConcurrencyToken = "token" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("validation_failed", root.GetProperty("errorCode").GetString());
        Assert.False(facade.WasCalled);
    }

    [Fact]
    public async Task ChangePriority_WhenPriorityIsMissing_Returns400ValidationProblemDetails()
    {
        var taskId = Guid.NewGuid();
        var facade = new FakeTaskFacade { ResultToReturn = BuildResponse(taskId: taskId) };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.PatchAsJsonAsync(
            $"api/tasks/{taskId}/priority",
            new { TaskId = taskId, ConcurrencyToken = "token" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("validation_failed", root.GetProperty("errorCode").GetString());
        Assert.False(facade.WasCalled);
    }

    [Fact]
    public async Task ChangePriority_WhenConcurrencyTokenIsMissing_Returns400ValidationProblemDetails()
    {
        var taskId = Guid.NewGuid();
        var facade = new FakeTaskFacade { ResultToReturn = BuildResponse(taskId: taskId) };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.PatchAsJsonAsync(
            $"api/tasks/{taskId}/priority",
            new { TaskId = taskId, Priority = "Critical" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("validation_failed", root.GetProperty("errorCode").GetString());
        Assert.False(facade.WasCalled);
    }

    // --- ChangePriority Not Found ---

    [Fact]
    public async Task ChangePriority_WhenFacadeThrowsArgumentException_Returns400BadRequest()
    {
        var taskId = Guid.NewGuid();
        var facade = new FakeTaskFacade
        {
            ExceptionToThrow = new ArgumentException($"Task with ID '{taskId}' does not exist.", nameof(taskId)),
        };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.PatchAsJsonAsync(
            $"api/tasks/{taskId}/priority",
            new ChangeTaskPriorityViewModel
            {
                TaskId = taskId,
                Priority = TaskItemPriorityContract.Critical,
                ConcurrencyToken = Convert.ToBase64String([1, 2, 3, 4, 5, 6, 7, 8]),
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("validation_failed", root.GetProperty("errorCode").GetString());
    }

    // --- ChangePriority Concurrency Conflict (DATA-008) ---

    [Fact]
    public async Task ChangePriority_WhenFacadeThrowsDbUpdateConcurrencyException_Returns409Conflict()
    {
        var taskId = Guid.NewGuid();
        var facade = new FakeTaskFacade
        {
            ExceptionToThrow = new Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException("Concurrency conflict", []),
        };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.PatchAsJsonAsync(
            $"api/tasks/{taskId}/priority",
            new ChangeTaskPriorityViewModel
            {
                TaskId = taskId,
                Priority = TaskItemPriorityContract.Critical,
                ConcurrencyToken = Convert.ToBase64String([1, 2, 3, 4, 5, 6, 7, 8]),
            });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("concurrency_conflict", root.GetProperty("errorCode").GetString());
    }

    // --- ChangePriority Unauthenticated (401) ---

    [Fact]
    public async Task ChangePriority_WhenNoAuthenticatedActor_Returns401AndNeverCallsFacade()
    {
        var taskId = Guid.NewGuid();
        var facade = new FakeTaskFacade { ResultToReturn = BuildResponse(taskId: taskId) };
        using var factory = CreateFactory(facade, authenticated: false);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.PatchAsJsonAsync(
            $"api/tasks/{taskId}/priority",
            new ChangeTaskPriorityViewModel
            {
                TaskId = taskId,
                Priority = TaskItemPriorityContract.Critical,
                ConcurrencyToken = Convert.ToBase64String([1, 2, 3, 4, 5, 6, 7, 8]),
            });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("authentication_required", root.GetProperty("errorCode").GetString());
        Assert.False(facade.WasCalled);
    }

    // --- ChangePriority Forbidden (403) ---

    [Fact]
    public async Task ChangePriority_WhenFacadeThrowsUnauthorizedAccessException_Returns403ProblemDetails()
    {
        var taskId = Guid.NewGuid();
        var facade = new FakeTaskFacade
        {
            ExceptionToThrow = new UnauthorizedAccessException("Not authorized."),
        };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.PatchAsJsonAsync(
            $"api/tasks/{taskId}/priority",
            new ChangeTaskPriorityViewModel
            {
                TaskId = taskId,
                Priority = TaskItemPriorityContract.Critical,
                ConcurrencyToken = Convert.ToBase64String([1, 2, 3, 4, 5, 6, 7, 8]),
            });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("forbidden", root.GetProperty("errorCode").GetString());
    }

    // --- List Success (TASK-020..022, API-005) ---

    [Fact]
    public async Task List_WhenAuthenticatedAndValid_Returns200WithPagedResponse()
    {
        var task1 = BuildResponse();
        var task2 = BuildResponse();
        var expectedResponse = new PagedResponse<TaskServiceModel>
        {
            Items = [task1, task2],
            Page = 1,
            PageSize = 25,
            TotalCount = 2,
            TotalPages = 1,
        };

        var facade = new FakeTaskFacade { ListResultToReturn = expectedResponse };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.GetAsync("api/tasks");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PagedResponse<TaskServiceModel>>();
        Assert.NotNull(body);
        Assert.Equal(2, body!.Items.Count);
        Assert.Equal(task1.Id, body.Items[0].Id);
        Assert.Equal(task2.Id, body.Items[1].Id);
        Assert.Equal(1, body.Page);
        Assert.Equal(25, body.PageSize);
        Assert.Equal(2, body.TotalCount);
        Assert.Equal(1, body.TotalPages);
    }

    [Fact]
    public async Task List_PassesTheQueryParametersToTheFacade()
    {
        var projectId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var dueDateBefore = new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc);
        var dueDateAfter = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var facade = new FakeTaskFacade
        {
            ListResultToReturn = new PagedResponse<TaskServiceModel>
            {
                Items = [],
                Page = 1,
                PageSize = 25,
                TotalCount = 0,
                TotalPages = 0,
            },
        };

        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var query = new Dictionary<string, string>
        {
            ["statuses"] = "ToDo,InProgress",
            ["priorities"] = "High,Critical",
            ["assignedUserId"] = "user-1",
            ["projectId"] = projectId.ToString(),
            ["clientId"] = clientId.ToString(),
            ["dueDateBefore"] = dueDateBefore.ToString("o"),
            ["dueDateAfter"] = dueDateAfter.ToString("o"),
            ["sortBy"] = "DueDateUtc",
            ["sortDirection"] = "Ascending",
            ["page"] = "2",
            ["pageSize"] = "50",
        };

        var queryString = "?" + string.Join("&", query.Select(kvp => $"{kvp.Key}={Uri.EscapeDataString(kvp.Value)}"));
        await httpClient.GetAsync($"api/tasks{queryString}");

        Assert.True(facade.WasCalled);
        Assert.NotNull(facade.ReceivedListRequest);
        Assert.Equal("ToDo,InProgress", facade.ReceivedListRequest!.Statuses);
        Assert.Equal("High,Critical", facade.ReceivedListRequest.Priorities);
        Assert.Equal("user-1", facade.ReceivedListRequest.AssignedUserId);
        Assert.Equal(projectId, facade.ReceivedListRequest.ProjectId);
        Assert.Equal(clientId, facade.ReceivedListRequest.ClientId);
        Assert.Equal(dueDateBefore, facade.ReceivedListRequest.DueDateBefore);
        Assert.Equal(dueDateAfter, facade.ReceivedListRequest.DueDateAfter);
        Assert.Equal(TaskSortField.DueDateUtc, facade.ReceivedListRequest.SortBy);
        Assert.Equal(TaskSortDirection.Ascending, facade.ReceivedListRequest.SortDirection);
        Assert.Equal(2, facade.ReceivedListRequest.Page);
        Assert.Equal(50, facade.ReceivedListRequest.PageSize);
    }

    [Fact]
    public async Task List_WithPaginationDefaults_UsesDefaultPageAndPageSize()
    {
        var facade = new FakeTaskFacade
        {
            ListResultToReturn = new PagedResponse<TaskServiceModel>
            {
                Items = [],
                Page = TasksApiContract.DefaultPage,
                PageSize = TasksApiContract.DefaultPageSize,
                TotalCount = 0,
                TotalPages = 0,
            },
        };

        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        await httpClient.GetAsync("api/tasks");

        Assert.True(facade.WasCalled);
        Assert.Equal(TasksApiContract.DefaultPage, facade.ReceivedListRequest!.Page);
        Assert.Equal(TasksApiContract.DefaultPageSize, facade.ReceivedListRequest.PageSize);
    }

    // --- List Validation (SEC-022) ---

    [Fact]
    public async Task List_WhenPageIsLessThanOne_Returns400ValidationProblemDetails()
    {
        var facade = new FakeTaskFacade
        {
            ListResultToReturn = new PagedResponse<TaskServiceModel>
            {
                Items = [],
                Page = 1,
                PageSize = 25,
                TotalCount = 0,
                TotalPages = 0,
            },
        };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.GetAsync("api/tasks?page=0");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("validation_failed", root.GetProperty("errorCode").GetString());
        Assert.False(facade.WasCalled);
    }

    [Fact]
    public async Task List_WhenPageSizeExceedsMaximum_Returns400ValidationProblemDetails()
    {
        var facade = new FakeTaskFacade
        {
            ListResultToReturn = new PagedResponse<TaskServiceModel>
            {
                Items = [],
                Page = 1,
                PageSize = 25,
                TotalCount = 0,
                TotalPages = 0,
            },
        };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.GetAsync($"api/tasks?pageSize={TasksApiContract.MaxPageSize + 1}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("validation_failed", root.GetProperty("errorCode").GetString());
        Assert.False(facade.WasCalled);
    }

    [Fact]
    public async Task List_WhenInvalidSortByEnum_Returns400ValidationProblemDetails()
    {
        var facade = new FakeTaskFacade
        {
            ListResultToReturn = new PagedResponse<TaskServiceModel>
            {
                Items = [],
                Page = 1,
                PageSize = 25,
                TotalCount = 0,
                TotalPages = 0,
            },
        };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.GetAsync("api/tasks?sortBy=InvalidField");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("validation_failed", root.GetProperty("errorCode").GetString());
        Assert.False(facade.WasCalled);
    }

    // --- List Unauthenticated (401) ---

    [Fact]
    public async Task List_WhenNoAuthenticatedActor_Returns401AndNeverCallsFacade()
    {
        var facade = new FakeTaskFacade
        {
            ListResultToReturn = new PagedResponse<TaskServiceModel>
            {
                Items = [],
                Page = 1,
                PageSize = 25,
                TotalCount = 0,
                TotalPages = 0,
            },
        };
        using var factory = CreateFactory(facade, authenticated: false);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.GetAsync("api/tasks");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("authentication_required", root.GetProperty("errorCode").GetString());
        Assert.False(facade.WasCalled);
    }

    // --- List Forbidden (403) ---

    [Fact]
    public async Task List_WhenFacadeThrowsUnauthorizedAccessException_Returns403ProblemDetails()
    {
        var facade = new FakeTaskFacade
        {
            ExceptionToThrow = new UnauthorizedAccessException("Not authorized."),
        };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.GetAsync("api/tasks");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("forbidden", root.GetProperty("errorCode").GetString());
    }

    // --- ChangeStatus Success (TASK-010..012, DATA-008) ---

    [Fact]
    public async Task ChangeStatus_WhenAuthenticatedAndValid_Returns200WithUpdatedTaskServiceModel()
    {
        var taskId = Guid.NewGuid();
        var concurrencyToken = Convert.ToBase64String([1, 2, 3, 4, 5, 6, 7, 8]);
        var request = new ChangeTaskStatusViewModel
        {
            TaskId = taskId,
            Status = TaskItemStatusContract.InProgress,
            ConcurrencyToken = concurrencyToken,
        };
        var expectedResponse = BuildResponse(taskId: taskId, status: TaskItemStatusContract.InProgress);
        var facade = new FakeTaskFacade { ResultToReturn = expectedResponse };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.PatchAsJsonAsync($"api/tasks/{taskId}/status", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<TaskServiceModel>();
        Assert.NotNull(body);
        Assert.Equal(expectedResponse.Id, body!.Id);
        Assert.Equal(expectedResponse.Status, body.Status);
    }

    [Fact]
    public async Task ChangeStatus_PassesTheBoundRequestFieldsToTheFacade()
    {
        var taskId = Guid.NewGuid();
        var concurrencyToken = Convert.ToBase64String([1, 2, 3, 4, 5, 6, 7, 8]);
        var request = new ChangeTaskStatusViewModel
        {
            TaskId = taskId,
            Status = TaskItemStatusContract.Completed,
            ConcurrencyToken = concurrencyToken,
        };
        var facade = new FakeTaskFacade { ResultToReturn = BuildResponse(taskId: taskId, status: TaskItemStatusContract.Completed) };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        await httpClient.PatchAsJsonAsync($"api/tasks/{taskId}/status", request);

        Assert.True(facade.WasCalled);
        Assert.Equal(taskId, facade.ReceivedChangeStatusRequest?.TaskId);
        Assert.Equal(TaskItemStatusContract.Completed, facade.ReceivedChangeStatusRequest?.Status);
        Assert.Equal(concurrencyToken, facade.ReceivedChangeStatusRequest?.ConcurrencyToken);
    }

    [Fact]
    public async Task ChangeStatus_WhenTransitioningToCompleted_ExposesCompletionTimestamp()
    {
        var taskId = Guid.NewGuid();
        var concurrencyToken = Convert.ToBase64String([1, 2, 3, 4, 5, 6, 7, 8]);
        var completedAtUtc = new DateTime(2026, 1, 15, 12, 30, 0, DateTimeKind.Utc);
        var request = new ChangeTaskStatusViewModel
        {
            TaskId = taskId,
            Status = TaskItemStatusContract.Completed,
            ConcurrencyToken = concurrencyToken,
        };
        var expectedResponse = new TaskServiceModel
        {
            Id = taskId,
            ProjectId = Guid.NewGuid(),
            Title = "Test Task",
            Status = TaskItemStatusContract.Completed,
            Priority = TaskItemPriorityContract.Normal,
            CreatedAtUtc = FixedUtcNow,
            CreatedBy = "actor-1",
            LastModifiedAtUtc = FixedUtcNow,
            LastModifiedBy = "actor-1",
            ConcurrencyToken = concurrencyToken,
            CompletedAtUtc = completedAtUtc,
        };
        var facade = new FakeTaskFacade { ResultToReturn = expectedResponse };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.PatchAsJsonAsync($"api/tasks/{taskId}/status", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<TaskServiceModel>();
        Assert.NotNull(body);
        Assert.Equal(TaskItemStatusContract.Completed, body!.Status);
        Assert.Equal(completedAtUtc, body.CompletedAtUtc);
    }

    // --- ChangeStatus Validation (SEC-022) ---

    [Fact]
    public async Task ChangeStatus_WhenTaskIdIsMissing_Returns400ValidationProblemDetails()
    {
        var taskId = Guid.NewGuid();
        var facade = new FakeTaskFacade { ResultToReturn = BuildResponse(taskId: taskId) };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.PatchAsJsonAsync(
            $"api/tasks/{taskId}/status",
            new { Status = "InProgress", ConcurrencyToken = "token" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("validation_failed", root.GetProperty("errorCode").GetString());
        Assert.False(facade.WasCalled);
    }

    [Fact]
    public async Task ChangeStatus_WhenStatusIsMissing_Returns400ValidationProblemDetails()
    {
        var taskId = Guid.NewGuid();
        var facade = new FakeTaskFacade { ResultToReturn = BuildResponse(taskId: taskId) };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.PatchAsJsonAsync(
            $"api/tasks/{taskId}/status",
            new { TaskId = taskId, ConcurrencyToken = "token" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("validation_failed", root.GetProperty("errorCode").GetString());
        Assert.False(facade.WasCalled);
    }

    [Fact]
    public async Task ChangeStatus_WhenConcurrencyTokenIsMissing_Returns400ValidationProblemDetails()
    {
        var taskId = Guid.NewGuid();
        var facade = new FakeTaskFacade { ResultToReturn = BuildResponse(taskId: taskId) };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.PatchAsJsonAsync(
            $"api/tasks/{taskId}/status",
            new { TaskId = taskId, Status = "InProgress" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("validation_failed", root.GetProperty("errorCode").GetString());
        Assert.False(facade.WasCalled);
    }

    // --- ChangeStatus Not Found ---

    [Fact]
    public async Task ChangeStatus_WhenFacadeThrowsArgumentException_Returns400BadRequest()
    {
        var taskId = Guid.NewGuid();
        var facade = new FakeTaskFacade
        {
            ExceptionToThrow = new ArgumentException($"Task with ID '{taskId}' does not exist.", nameof(taskId)),
        };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.PatchAsJsonAsync(
            $"api/tasks/{taskId}/status",
            new ChangeTaskStatusViewModel
            {
                TaskId = taskId,
                Status = TaskItemStatusContract.InProgress,
                ConcurrencyToken = Convert.ToBase64String([1, 2, 3, 4, 5, 6, 7, 8]),
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("validation_failed", root.GetProperty("errorCode").GetString());
    }

    // --- ChangeStatus Rejected Transitions ---

    [Fact]
    public async Task ChangeStatus_WhenFacadeThrowsInvalidOperationException_Returns400BadRequest()
    {
        var taskId = Guid.NewGuid();
        var facade = new FakeTaskFacade
        {
            ExceptionToThrow = new InvalidOperationException("A Completed Task cannot transition to another status via SetStatus; use Reopen instead (TASK-012)."),
        };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.PatchAsJsonAsync(
            $"api/tasks/{taskId}/status",
            new ChangeTaskStatusViewModel
            {
                TaskId = taskId,
                Status = TaskItemStatusContract.InProgress,
                ConcurrencyToken = Convert.ToBase64String([1, 2, 3, 4, 5, 6, 7, 8]),
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("validation_failed", root.GetProperty("errorCode").GetString());
    }

    // --- ChangeStatus Concurrency Conflict (DATA-008) ---

    [Fact]
    public async Task ChangeStatus_WhenFacadeThrowsDbUpdateConcurrencyException_Returns409Conflict()
    {
        var taskId = Guid.NewGuid();
        var facade = new FakeTaskFacade
        {
            ExceptionToThrow = new Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException("Concurrency conflict", []),
        };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.PatchAsJsonAsync(
            $"api/tasks/{taskId}/status",
            new ChangeTaskStatusViewModel
            {
                TaskId = taskId,
                Status = TaskItemStatusContract.InProgress,
                ConcurrencyToken = Convert.ToBase64String([1, 2, 3, 4, 5, 6, 7, 8]),
            });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("concurrency_conflict", root.GetProperty("errorCode").GetString());
    }

    // --- ChangeStatus Unauthenticated (401) ---

    [Fact]
    public async Task ChangeStatus_WhenNoAuthenticatedActor_Returns401AndNeverCallsFacade()
    {
        var taskId = Guid.NewGuid();
        var facade = new FakeTaskFacade { ResultToReturn = BuildResponse(taskId: taskId) };
        using var factory = CreateFactory(facade, authenticated: false);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.PatchAsJsonAsync(
            $"api/tasks/{taskId}/status",
            new ChangeTaskStatusViewModel
            {
                TaskId = taskId,
                Status = TaskItemStatusContract.InProgress,
                ConcurrencyToken = Convert.ToBase64String([1, 2, 3, 4, 5, 6, 7, 8]),
            });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("authentication_required", root.GetProperty("errorCode").GetString());
        Assert.False(facade.WasCalled);
    }

    // --- ChangeStatus Forbidden (403) ---

    [Fact]
    public async Task ChangeStatus_WhenFacadeThrowsUnauthorizedAccessException_Returns403ProblemDetails()
    {
        var taskId = Guid.NewGuid();
        var facade = new FakeTaskFacade
        {
            ExceptionToThrow = new UnauthorizedAccessException("Not authorized."),
        };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.PatchAsJsonAsync(
            $"api/tasks/{taskId}/status",
            new ChangeTaskStatusViewModel
            {
                TaskId = taskId,
                Status = TaskItemStatusContract.InProgress,
                ConcurrencyToken = Convert.ToBase64String([1, 2, 3, 4, 5, 6, 7, 8]),
            });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("forbidden", root.GetProperty("errorCode").GetString());
    }

    // --- Reopen Success (TASK-012, DATA-008) ---

    [Fact]
    public async Task Reopen_WhenAuthenticatedAndValid_Returns200WithUpdatedTaskServiceModel()
    {
        var taskId = Guid.NewGuid();
        var concurrencyToken = Convert.ToBase64String([1, 2, 3, 4, 5, 6, 7, 8]);
        var request = new ReopenTaskViewModel
        {
            TaskId = taskId,
            ReopenToStatus = TaskItemStatusContract.ToDo,
            ConcurrencyToken = concurrencyToken,
        };
        var expectedResponse = BuildResponse(taskId: taskId, status: TaskItemStatusContract.ToDo);
        var facade = new FakeTaskFacade { ResultToReturn = expectedResponse };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.PatchAsJsonAsync($"api/tasks/{taskId}/reopen", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<TaskServiceModel>();
        Assert.NotNull(body);
        Assert.Equal(expectedResponse.Id, body!.Id);
        Assert.Equal(expectedResponse.Status, body.Status);
        Assert.Null(body.CompletedAtUtc);
    }

    [Fact]
    public async Task Reopen_PassesTheBoundRequestFieldsToTheFacade()
    {
        var taskId = Guid.NewGuid();
        var concurrencyToken = Convert.ToBase64String([1, 2, 3, 4, 5, 6, 7, 8]);
        var request = new ReopenTaskViewModel
        {
            TaskId = taskId,
            ReopenToStatus = TaskItemStatusContract.InProgress,
            ConcurrencyToken = concurrencyToken,
        };
        var facade = new FakeTaskFacade { ResultToReturn = BuildResponse(taskId: taskId, status: TaskItemStatusContract.InProgress) };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        await httpClient.PatchAsJsonAsync($"api/tasks/{taskId}/reopen", request);

        Assert.True(facade.WasCalled);
        Assert.Equal(taskId, facade.ReceivedReopenRequest?.TaskId);
        Assert.Equal(TaskItemStatusContract.InProgress, facade.ReceivedReopenRequest?.ReopenToStatus);
        Assert.Equal(concurrencyToken, facade.ReceivedReopenRequest?.ConcurrencyToken);
    }

    // --- Reopen Validation (SEC-022) ---

    [Fact]
    public async Task Reopen_WhenTaskIdIsMissing_Returns400ValidationProblemDetails()
    {
        var taskId = Guid.NewGuid();
        var facade = new FakeTaskFacade { ResultToReturn = BuildResponse(taskId: taskId) };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.PatchAsJsonAsync(
            $"api/tasks/{taskId}/reopen",
            new { ReopenToStatus = "ToDo", ConcurrencyToken = "token" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("validation_failed", root.GetProperty("errorCode").GetString());
        Assert.False(facade.WasCalled);
    }

    [Fact]
    public async Task Reopen_WhenReopenToStatusIsMissing_Returns400ValidationProblemDetails()
    {
        var taskId = Guid.NewGuid();
        var facade = new FakeTaskFacade { ResultToReturn = BuildResponse(taskId: taskId) };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.PatchAsJsonAsync(
            $"api/tasks/{taskId}/reopen",
            new { TaskId = taskId, ConcurrencyToken = "token" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("validation_failed", root.GetProperty("errorCode").GetString());
        Assert.False(facade.WasCalled);
    }

    [Fact]
    public async Task Reopen_WhenConcurrencyTokenIsMissing_Returns400ValidationProblemDetails()
    {
        var taskId = Guid.NewGuid();
        var facade = new FakeTaskFacade { ResultToReturn = BuildResponse(taskId: taskId) };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.PatchAsJsonAsync(
            $"api/tasks/{taskId}/reopen",
            new { TaskId = taskId, ReopenToStatus = "ToDo" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("validation_failed", root.GetProperty("errorCode").GetString());
        Assert.False(facade.WasCalled);
    }

    // --- Reopen Not Found ---

    [Fact]
    public async Task Reopen_WhenFacadeThrowsArgumentException_Returns400BadRequest()
    {
        var taskId = Guid.NewGuid();
        var facade = new FakeTaskFacade
        {
            ExceptionToThrow = new ArgumentException($"Task with ID '{taskId}' does not exist.", nameof(taskId)),
        };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.PatchAsJsonAsync(
            $"api/tasks/{taskId}/reopen",
            new ReopenTaskViewModel
            {
                TaskId = taskId,
                ReopenToStatus = TaskItemStatusContract.ToDo,
                ConcurrencyToken = Convert.ToBase64String([1, 2, 3, 4, 5, 6, 7, 8]),
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("validation_failed", root.GetProperty("errorCode").GetString());
    }

    // --- Reopen Invalid State ---

    [Fact]
    public async Task Reopen_WhenTaskIsNotCompleted_Returns400BadRequest()
    {
        var taskId = Guid.NewGuid();
        var facade = new FakeTaskFacade
        {
            ExceptionToThrow = new InvalidOperationException("Only a Completed Task can be reopened (TASK-012)."),
        };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.PatchAsJsonAsync(
            $"api/tasks/{taskId}/reopen",
            new ReopenTaskViewModel
            {
                TaskId = taskId,
                ReopenToStatus = TaskItemStatusContract.ToDo,
                ConcurrencyToken = Convert.ToBase64String([1, 2, 3, 4, 5, 6, 7, 8]),
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("validation_failed", root.GetProperty("errorCode").GetString());
    }

    // --- Reopen Concurrency Conflict (DATA-008) ---

    [Fact]
    public async Task Reopen_WhenFacadeThrowsDbUpdateConcurrencyException_Returns409Conflict()
    {
        var taskId = Guid.NewGuid();
        var facade = new FakeTaskFacade
        {
            ExceptionToThrow = new Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException("Concurrency conflict", []),
        };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.PatchAsJsonAsync(
            $"api/tasks/{taskId}/reopen",
            new ReopenTaskViewModel
            {
                TaskId = taskId,
                ReopenToStatus = TaskItemStatusContract.ToDo,
                ConcurrencyToken = Convert.ToBase64String([1, 2, 3, 4, 5, 6, 7, 8]),
            });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("concurrency_conflict", root.GetProperty("errorCode").GetString());
    }

    // --- Reopen Unauthenticated (401) ---

    [Fact]
    public async Task Reopen_WhenNoAuthenticatedActor_Returns401AndNeverCallsFacade()
    {
        var taskId = Guid.NewGuid();
        var facade = new FakeTaskFacade { ResultToReturn = BuildResponse(taskId: taskId) };
        using var factory = CreateFactory(facade, authenticated: false);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.PatchAsJsonAsync(
            $"api/tasks/{taskId}/reopen",
            new ReopenTaskViewModel
            {
                TaskId = taskId,
                ReopenToStatus = TaskItemStatusContract.ToDo,
                ConcurrencyToken = Convert.ToBase64String([1, 2, 3, 4, 5, 6, 7, 8]),
            });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("authentication_required", root.GetProperty("errorCode").GetString());
        Assert.False(facade.WasCalled);
    }

    // --- Reopen Forbidden (403) ---

    [Fact]
    public async Task Reopen_WhenFacadeThrowsUnauthorizedAccessException_Returns403ProblemDetails()
    {
        var taskId = Guid.NewGuid();
        var facade = new FakeTaskFacade
        {
            ExceptionToThrow = new UnauthorizedAccessException("Not authorized."),
        };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.PatchAsJsonAsync(
            $"api/tasks/{taskId}/reopen",
            new ReopenTaskViewModel
            {
                TaskId = taskId,
                ReopenToStatus = TaskItemStatusContract.ToDo,
                ConcurrencyToken = Convert.ToBase64String([1, 2, 3, 4, 5, 6, 7, 8]),
            });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("forbidden", root.GetProperty("errorCode").GetString());
    }

    // --- Edit Success (TASK-002) ---

    [Fact]
    public async Task Edit_WhenAuthenticatedAndValid_Returns200WithUpdatedTaskServiceModel()
    {
        var taskId = Guid.NewGuid();
        var concurrencyToken = Convert.ToBase64String([1, 2, 3, 4, 5, 6, 7, 8]);
        var request = new EditTaskViewModel
        {
            TaskId = taskId,
            Title = "Updated Title",
            Description = "Updated Description",
            StartDateUtc = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
            DueDateUtc = new DateTime(2026, 2, 28, 23, 59, 59, DateTimeKind.Utc),
            Notes = "Updated Notes",
            ConcurrencyToken = concurrencyToken,
        };
        var expectedResponse = BuildResponse(taskId: taskId, title: "Updated Title");
        var facade = new FakeTaskFacade { ResultToReturn = expectedResponse };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.PatchAsJsonAsync($"api/tasks/{taskId}/details", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<TaskServiceModel>();
        Assert.NotNull(body);
        Assert.Equal(expectedResponse.Id, body!.Id);
        Assert.Equal("Updated Title", body.Title);
    }

    [Fact]
    public async Task Edit_PassesTheBoundRequestFieldsToTheFacade()
    {
        var taskId = Guid.NewGuid();
        var concurrencyToken = Convert.ToBase64String([1, 2, 3, 4, 5, 6, 7, 8]);
        var request = new EditTaskViewModel
        {
            TaskId = taskId,
            Title = "New Title",
            ConcurrencyToken = concurrencyToken,
        };
        var facade = new FakeTaskFacade { ResultToReturn = BuildResponse(taskId: taskId, title: "New Title") };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        await httpClient.PatchAsJsonAsync($"api/tasks/{taskId}/details", request);

        Assert.True(facade.WasCalled);
        Assert.Equal(taskId, facade.ReceivedEditRequest?.TaskId);
        Assert.Equal("New Title", facade.ReceivedEditRequest?.Title);
        Assert.Equal(concurrencyToken, facade.ReceivedEditRequest?.ConcurrencyToken);
    }

    // --- Edit Validation (SEC-022) ---

    [Fact]
    public async Task Edit_WhenTaskIdIsMissing_Returns400ValidationProblemDetails()
    {
        var taskId = Guid.NewGuid();
        var facade = new FakeTaskFacade { ResultToReturn = BuildResponse(taskId: taskId) };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.PatchAsJsonAsync(
            $"api/tasks/{taskId}/details",
            new { Title = "New Title", ConcurrencyToken = "token" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("validation_failed", root.GetProperty("errorCode").GetString());
        Assert.False(facade.WasCalled);
    }

    [Fact]
    public async Task Edit_WhenConcurrencyTokenIsMissing_Returns400ValidationProblemDetails()
    {
        var taskId = Guid.NewGuid();
        var facade = new FakeTaskFacade { ResultToReturn = BuildResponse(taskId: taskId) };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.PatchAsJsonAsync(
            $"api/tasks/{taskId}/details",
            new { TaskId = taskId, Title = "New Title" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("validation_failed", root.GetProperty("errorCode").GetString());
        Assert.False(facade.WasCalled);
    }

    // --- Edit Business Logic Failures ---

    [Fact]
    public async Task Edit_WhenTaskDoesNotExist_Returns400BadRequest()
    {
        var taskId = Guid.NewGuid();
        var facade = new FakeTaskFacade
        {
            ExceptionToThrow = new ArgumentException($"Task with ID '{taskId}' does not exist."),
        };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.PatchAsJsonAsync(
            $"api/tasks/{taskId}/details",
            new EditTaskViewModel
            {
                TaskId = taskId,
                Title = "Updated Title",
                ConcurrencyToken = Convert.ToBase64String([1, 2, 3, 4, 5, 6, 7, 8]),
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Edit_WhenNoFieldsChanged_Returns400BadRequest()
    {
        var taskId = Guid.NewGuid();
        var facade = new FakeTaskFacade
        {
            ExceptionToThrow = new InvalidOperationException("No fields were changed; the Task is already in the requested state."),
        };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.PatchAsJsonAsync(
            $"api/tasks/{taskId}/details",
            new EditTaskViewModel
            {
                TaskId = taskId,
                ConcurrencyToken = Convert.ToBase64String([1, 2, 3, 4, 5, 6, 7, 8]),
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    // --- Edit Concurrency Conflict (409) ---

    [Fact]
    public async Task Edit_WhenConcurrencyTokenHasChanged_Returns409Conflict()
    {
        var taskId = Guid.NewGuid();
        using var factory = CreateFactory(
            new FakeTaskFacade { ExceptionToThrow = new DbUpdateConcurrencyException("", []) },
            authenticated: true);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.PatchAsJsonAsync(
            $"api/tasks/{taskId}/details",
            new EditTaskViewModel
            {
                TaskId = taskId,
                Title = "Updated Title",
                ConcurrencyToken = Convert.ToBase64String([1, 2, 3, 4, 5, 6, 7, 8]),
            });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("concurrency_conflict", root.GetProperty("errorCode").GetString());
    }

    // --- Edit Unauthenticated (401) ---

    [Fact]
    public async Task Edit_WhenNoAuthenticatedActor_Returns401AndNeverCallsFacade()
    {
        var taskId = Guid.NewGuid();
        var facade = new FakeTaskFacade { ResultToReturn = BuildResponse(taskId: taskId) };
        using var factory = CreateFactory(facade, authenticated: false);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.PatchAsJsonAsync(
            $"api/tasks/{taskId}/details",
            new EditTaskViewModel
            {
                TaskId = taskId,
                Title = "Updated Title",
                ConcurrencyToken = Convert.ToBase64String([1, 2, 3, 4, 5, 6, 7, 8]),
            });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("authentication_required", root.GetProperty("errorCode").GetString());
        Assert.False(facade.WasCalled);
    }

    // --- Edit Forbidden (403) ---

    [Fact]
    public async Task Edit_WhenFacadeThrowsUnauthorizedAccessException_Returns403ProblemDetails()
    {
        var taskId = Guid.NewGuid();
        var facade = new FakeTaskFacade
        {
            ExceptionToThrow = new UnauthorizedAccessException("Not authorized."),
        };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.PatchAsJsonAsync(
            $"api/tasks/{taskId}/details",
            new EditTaskViewModel
            {
                TaskId = taskId,
                Title = "Updated Title",
                ConcurrencyToken = Convert.ToBase64String([1, 2, 3, 4, 5, 6, 7, 8]),
            });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("forbidden", root.GetProperty("errorCode").GetString());
    }

    private static WebApplicationFactory<Program> CreateFactory(FakeTaskFacade facade, bool authenticated)
    {
        Environment.SetEnvironmentVariable(
            CrmDbConnectionStringEnvironmentVariable,
            "Server=localhost;Database=CrmDbTasksControllerTests;TrustServerCertificate=True;");

        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.AddScoped<ITaskFacade>(_ => facade);

                // Configure test authentication scheme for all tests (required for authorization middleware)
                services.AddAuthentication("TestScheme")
                    .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("TestScheme", _ => { });
                // Add startup filter that sets the test user with Manager role when authenticated is true
                services.AddSingleton<IStartupFilter>(new AuthenticatedActorStartupFilter(authenticated));
            }));
    }

    // Fake ITaskFacade (mirrors FakeProjectFacade's hand-written fake style - no mocking library is
    // used in this repository).
    private sealed class FakeTaskFacade : ITaskFacade
    {
        public TaskServiceModel ResultToReturn { get; init; } = null!;

        public PagedResponse<TaskServiceModel> ListResultToReturn { get; init; } = null!;

        public Exception? ExceptionToThrow { get; init; }

        public bool WasCalled { get; private set; }

        public CreateTaskViewModel? ReceivedRequest { get; private set; }

        public AssignTaskViewModel? ReceivedAssignRequest { get; private set; }

        public ChangeTaskPriorityViewModel? ReceivedChangePriorityRequest { get; private set; }

        public ChangeTaskStatusViewModel? ReceivedChangeStatusRequest { get; private set; }

        public ReopenTaskViewModel? ReceivedReopenRequest { get; private set; }

        public EditTaskViewModel? ReceivedEditRequest { get; private set; }

        public ListTasksRequest? ReceivedListRequest { get; private set; }

        public Task<TaskServiceModel> CreateAsync(
            CreateTaskViewModel request, CancellationToken cancellationToken)
        {
            WasCalled = true;
            ReceivedRequest = request;

            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }

            return Task.FromResult(ResultToReturn);
        }

        public Task<TaskServiceModel> AssignAsync(
            AssignTaskViewModel request, CancellationToken cancellationToken)
        {
            WasCalled = true;
            ReceivedAssignRequest = request;

            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }

            return Task.FromResult(ResultToReturn);
        }

        public Task<TaskServiceModel> ChangePriorityAsync(
            ChangeTaskPriorityViewModel request, CancellationToken cancellationToken)
        {
            WasCalled = true;
            ReceivedChangePriorityRequest = request;

            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }

            return Task.FromResult(ResultToReturn);
        }

        public Task<PagedResponse<TaskServiceModel>> ListAsync(
            ListTasksRequest request, CancellationToken cancellationToken)
        {
            WasCalled = true;
            ReceivedListRequest = request;

            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }

            return Task.FromResult(ListResultToReturn);
        }

        public Task<TaskServiceModel> ChangeStatusAsync(
            ChangeTaskStatusViewModel request, CancellationToken cancellationToken)
        {
            WasCalled = true;
            ReceivedChangeStatusRequest = request;

            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }

            return Task.FromResult(ResultToReturn);
        }

        public Task<TaskServiceModel> ReopenAsync(
            ReopenTaskViewModel request, CancellationToken cancellationToken)
        {
            WasCalled = true;
            ReceivedReopenRequest = request;

            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }

            return Task.FromResult(ResultToReturn);
        }

        public Task<TaskServiceModel> EditAsync(
            EditTaskViewModel request, CancellationToken cancellationToken)
        {
            WasCalled = true;
            ReceivedEditRequest = request;

            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }

            return Task.FromResult(ResultToReturn);
        }
    }

    // Sets HttpContext.User to a fake authenticated ClaimsPrincipal before routing, without wiring a
    // real ASP.NET Core authentication scheme into the production pipeline - the browser
    // authentication transport (cookie/JWT/etc.) remains an open decision (ADR-0018) that this
    // controller-only microstep must not silently make. Runs after the real host pipeline
    // (UseExceptionHandler/UseStatusCodePages/MapControllers) is already registered, the same pattern
    // ProjectsControllerTests and ApiExceptionHandlingHostTests use.
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
                    var claims = new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, "test-user-1"),
                        new Claim(ClaimTypes.Name, "Test User"),
                        new Claim(ClaimTypes.Role, "Manager"), // Grant Manager role so all tests pass policies
                    };

                    var identity = new ClaimsIdentity(claims, "TestScheme");
                    context.User = new ClaimsPrincipal(identity);
                }
                // If not authenticated, leave context.User as null - authorization middleware will handle rejection

                return nextMiddleware(context);
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

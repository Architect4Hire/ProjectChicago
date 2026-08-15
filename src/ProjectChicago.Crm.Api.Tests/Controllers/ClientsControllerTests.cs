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
using ProjectChicago.Crm.Core.Data;
using ProjectChicago.Crm.Core.Facades;
using Xunit;

namespace ProjectChicago.Crm.Api.Tests.Controllers;

// End-to-end HTTP tests for POST /api/clients (CLIENT-001..004, API-001..007, SEC-010..013,
// ERROR-001..005) against the real Crm Program.cs composition root - proves ClientsController's
// transport behavior (status codes, Location header, pass-through of the Facade's response body),
// not the request/response field mapping (that lives in ClientContractMappingExtensions and is
// covered by ProjectChicago.Crm.Core.Tests). IClientFacade is replaced with a hand-written fake per
// test (mirrors ClientFacadeTests' fake style; no mocking library is used in this repository), since
// the production Facade->Business->Data->Repository->DbContext chain and its
// IClientAuthorization/IClock adapters are not wired in Program.cs yet (composition-root work
// explicitly out of scope for this controller-only microstep).
public class ClientsControllerTests
{
    private const string CrmDbConnectionStringEnvironmentVariable = "ConnectionStrings__CrmDb";
    private static readonly DateTime FixedUtcNow = new(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);

    private static CreateClientViewModel ValidRequest() => new()
    {
        Name = "Acme Corporation",
        OwnerUserId = "owner-1",
        PrimaryEmail = "jane@acme.example",
    };

    private static ClientServiceModel BuildResponse(
        string name = "Acme Corporation",
        string ownerUserId = "owner-1",
        IReadOnlyList<ClientDuplicateWarning>? possibleDuplicates = null) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        OwnerUserId = ownerUserId,
        LifecycleStatus = ClientLifecycleStatusContract.Lead,
        CreatedAtUtc = FixedUtcNow,
        CreatedBy = "actor-1",
        LastModifiedAtUtc = FixedUtcNow,
        LastModifiedBy = "actor-1",
        ConcurrencyToken = Convert.ToBase64String([1, 2, 3, 4, 5, 6, 7, 8]),
        PossibleDuplicates = possibleDuplicates ?? [],
    };

    // --- Success (CLIENT-001..003) ---

    [Fact]
    public async Task Create_WhenAuthenticatedAndValid_Returns201WithLocationAndTheFacadesResponseBody()
    {
        var expectedResponse = BuildResponse();
        var facade = new FakeClientFacade { ResultToReturn = expectedResponse };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.PostAsJsonAsync(ClientsApiContract.Route, ValidRequest());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal($"{ClientsApiContract.Route}/{expectedResponse.Id}", response.Headers.Location?.ToString());

        var body = await response.Content.ReadFromJsonAsync<ClientServiceModel>();
        Assert.NotNull(body);
        Assert.Equal(expectedResponse.Id, body!.Id);
        Assert.Equal(expectedResponse.Name, body.Name);
        Assert.Equal(expectedResponse.OwnerUserId, body.OwnerUserId);
        Assert.Equal(expectedResponse.LifecycleStatus, body.LifecycleStatus);
        Assert.Empty(body.PossibleDuplicates);
    }

    [Fact]
    public async Task Create_PassesTheBoundRequestFieldsToTheFacade()
    {
        var facade = new FakeClientFacade { ResultToReturn = BuildResponse() };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        await httpClient.PostAsJsonAsync(ClientsApiContract.Route, ValidRequest() with { Name = "Contoso Ltd" });

        Assert.True(facade.WasCalled);
        Assert.Equal("Contoso Ltd", facade.ReceivedRequest?.Name);
        Assert.Equal("owner-1", facade.ReceivedRequest?.OwnerUserId);
    }

    // --- Duplicate-policy result (CLIENT-004: warns, never blocks) ---

    [Fact]
    public async Task Create_WhenFacadeReturnsPossibleDuplicates_Returns201WithDuplicatesInBody()
    {
        var facade = new FakeClientFacade
        {
            ResultToReturn = BuildResponse(possibleDuplicates:
            [
                new ClientDuplicateWarning
                {
                    ClientId = Guid.NewGuid(),
                    Name = "Acme Corp",
                    MatchedOn = [ClientDuplicateMatchField.Name, ClientDuplicateMatchField.PrimaryEmail],
                },
            ]),
        };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.PostAsJsonAsync(ClientsApiContract.Route, ValidRequest());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ClientServiceModel>();
        var duplicate = Assert.Single(body!.PossibleDuplicates);
        Assert.Equal("Acme Corp", duplicate.Name);
        Assert.Contains(ClientDuplicateMatchField.Name, duplicate.MatchedOn);
        Assert.Contains(ClientDuplicateMatchField.PrimaryEmail, duplicate.MatchedOn);
    }

    // --- Validation (SEC-022; automatic [ApiController] model-state 400) ---

    [Fact]
    public async Task Create_WhenNameIsMissing_Returns400ValidationProblemDetailsAndNeverCallsFacade()
    {
        var facade = new FakeClientFacade { ResultToReturn = BuildResponse() };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.PostAsJsonAsync(ClientsApiContract.Route, ValidRequest() with { Name = null! });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("validation_failed", root.GetProperty("errorCode").GetString());
        Assert.False(facade.WasCalled);
    }

    [Fact]
    public async Task Create_WhenPrimaryEmailIsMalformed_Returns400ValidationProblemDetails()
    {
        var facade = new FakeClientFacade { ResultToReturn = BuildResponse() };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.PostAsJsonAsync(ClientsApiContract.Route, ValidRequest() with { PrimaryEmail = "not-an-email" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(facade.WasCalled);
    }

    // --- Unauthenticated (401 - coarse controller check, distinct from Facade's 403) ---

    [Fact]
    public async Task Create_WhenNoAuthenticatedActor_Returns401AndNeverCallsFacade()
    {
        var facade = new FakeClientFacade { ResultToReturn = BuildResponse() };
        using var factory = CreateFactory(facade, authenticated: false);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.PostAsJsonAsync(ClientsApiContract.Route, ValidRequest());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("authentication_required", root.GetProperty("errorCode").GetString());
        Assert.False(facade.WasCalled);
    }

    // --- Forbidden (403 - Facade/IClientAuthorization policy rejection, SEC-012/013) ---

    [Fact]
    public async Task Create_WhenFacadeThrowsUnauthorizedAccessException_Returns403ProblemDetails()
    {
        var facade = new FakeClientFacade { ExceptionToThrow = new UnauthorizedAccessException("Not authorized.") };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.PostAsJsonAsync(ClientsApiContract.Route, ValidRequest());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("forbidden", root.GetProperty("errorCode").GetString());
    }

    // --- List/search (CLIENT-020..024, API-005) ---

    private static ClientServiceModel BuildListItem(string name, ClientLifecycleStatusContract status = ClientLifecycleStatusContract.Lead) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        OwnerUserId = "owner-1",
        LifecycleStatus = status,
        CreatedAtUtc = FixedUtcNow,
        CreatedBy = "actor-1",
        LastModifiedAtUtc = FixedUtcNow,
        LastModifiedBy = "actor-1",
        ConcurrencyToken = Convert.ToBase64String([1, 2, 3, 4, 5, 6, 7, 8]),
        PossibleDuplicates = [],
    };

    [Fact]
    public async Task List_WhenAuthenticatedWithNoQuery_Returns200WithFacadesDefaultPagedResponse()
    {
        var expectedResponse = new PagedResponse<ClientServiceModel>
        {
            Items = [BuildListItem("Acme Corporation"), BuildListItem("Contoso Ltd")],
            Page = ClientsApiContract.DefaultPage,
            PageSize = ClientsApiContract.DefaultPageSize,
            TotalCount = 2,
            TotalPages = 1,
        };
        var facade = new FakeClientFacade { ListResultToReturn = expectedResponse };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.GetAsync(ClientsApiContract.Route);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PagedResponse<ClientServiceModel>>();
        Assert.NotNull(body);
        Assert.Equal(2, body!.Items.Count);
        Assert.Equal(ClientsApiContract.DefaultPage, body.Page);
        Assert.Equal(ClientsApiContract.DefaultPageSize, body.PageSize);
        Assert.True(facade.ListWasCalled);
        Assert.Equal(ClientsApiContract.DefaultPage, facade.ReceivedListRequest?.Page);
        Assert.Equal(ClientsApiContract.DefaultPageSize, facade.ReceivedListRequest?.PageSize);
    }

    [Fact]
    public async Task List_WhenSearchFilterAndSortSupplied_PassesTheBoundQueryFieldsToTheFacade()
    {
        var facade = new FakeClientFacade
        {
            ListResultToReturn = new PagedResponse<ClientServiceModel>
            {
                Items = [BuildListItem("Acme Corporation", ClientLifecycleStatusContract.Active)],
                Page = 2,
                PageSize = 10,
                TotalCount = 11,
                TotalPages = 2,
            },
        };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var query =
            "?Search=acme" +
            "&LifecycleStatus=Active" +
            "&OwnerUserId=owner-1" +
            "&IsActive=true" +
            "&SortBy=CreatedAtUtc" +
            "&SortDirection=Descending" +
            "&Page=2" +
            "&PageSize=10";

        var response = await httpClient.GetAsync($"{ClientsApiContract.Route}{query}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(facade.ListWasCalled);
        var received = facade.ReceivedListRequest;
        Assert.NotNull(received);
        Assert.Equal("acme", received!.Search);
        Assert.Equal(ClientLifecycleStatusContract.Active, received.LifecycleStatus);
        Assert.Equal("owner-1", received.OwnerUserId);
        Assert.True(received.IsActive);
        Assert.Equal(ClientSortField.CreatedAtUtc, received.SortBy);
        Assert.Equal(ClientSortDirection.Descending, received.SortDirection);
        Assert.Equal(2, received.Page);
        Assert.Equal(10, received.PageSize);
    }

    [Fact]
    public async Task List_WhenPageSizeExceedsMax_Returns400ValidationProblemDetailsAndNeverCallsFacade()
    {
        var facade = new FakeClientFacade { ListResultToReturn = new PagedResponse<ClientServiceModel> { Items = [], Page = 1, PageSize = 1, TotalCount = 0, TotalPages = 0 } };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.GetAsync($"{ClientsApiContract.Route}?PageSize={ClientsApiContract.MaxPageSize + 1}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("validation_failed", root.GetProperty("errorCode").GetString());
        Assert.False(facade.ListWasCalled);
    }

    [Fact]
    public async Task List_WhenSortByIsUndefined_Returns400ValidationProblemDetailsAndNeverCallsFacade()
    {
        var facade = new FakeClientFacade { ListResultToReturn = new PagedResponse<ClientServiceModel> { Items = [], Page = 1, PageSize = 1, TotalCount = 0, TotalPages = 0 } };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.GetAsync($"{ClientsApiContract.Route}?SortBy=NotARealSortField");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(facade.ListWasCalled);
    }

    [Fact]
    public async Task List_WhenNoAuthenticatedActor_Returns401AndNeverCallsFacade()
    {
        var facade = new FakeClientFacade { ListResultToReturn = new PagedResponse<ClientServiceModel> { Items = [], Page = 1, PageSize = 1, TotalCount = 0, TotalPages = 0 } };
        using var factory = CreateFactory(facade, authenticated: false);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.GetAsync(ClientsApiContract.Route);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("authentication_required", root.GetProperty("errorCode").GetString());
        Assert.False(facade.ListWasCalled);
    }

    [Fact]
    public async Task List_WhenFacadeThrowsUnauthorizedAccessException_Returns403ProblemDetails()
    {
        var facade = new FakeClientFacade { ListExceptionToThrow = new UnauthorizedAccessException("Not authorized.") };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.GetAsync(ClientsApiContract.Route);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("forbidden", root.GetProperty("errorCode").GetString());
    }

    // --- Detail (CLIENT-030..032) ---

    private static ClientDetailServiceModel BuildDetailResponse() => new()
    {
        Client = BuildResponse(),
        ActiveProjects = [],
        HistoricalProjects = [],
        OpenTasks = [],
        RecentlyCompletedTasks = [],
    };

    [Fact]
    public async Task GetDetail_WhenAuthenticatedAndFound_Returns200WithTheFacadesResponseBody()
    {
        var expectedResponse = BuildDetailResponse();
        var facade = new FakeClientFacade { DetailResultToReturn = expectedResponse };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.GetAsync($"{ClientsApiContract.Route}/{expectedResponse.Client.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ClientDetailServiceModel>();
        Assert.NotNull(body);
        Assert.Equal(expectedResponse.Client.Id, body!.Client.Id);
        Assert.True(facade.DetailWasCalled);
        Assert.Equal(expectedResponse.Client.Id, facade.ReceivedDetailClientId);
    }

    [Fact]
    public async Task GetDetail_WhenFacadeReturnsNull_Returns404ProblemDetails()
    {
        var facade = new FakeClientFacade { DetailResultToReturn = null };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.GetAsync($"{ClientsApiContract.Route}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("resource_not_found", root.GetProperty("errorCode").GetString());
        Assert.True(facade.DetailWasCalled);
    }

    [Fact]
    public async Task GetDetail_WhenNoAuthenticatedActor_Returns401AndNeverCallsFacade()
    {
        var facade = new FakeClientFacade { DetailResultToReturn = BuildDetailResponse() };
        using var factory = CreateFactory(facade, authenticated: false);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.GetAsync($"{ClientsApiContract.Route}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("authentication_required", root.GetProperty("errorCode").GetString());
        Assert.False(facade.DetailWasCalled);
    }

    [Fact]
    public async Task GetDetail_WhenFacadeThrowsUnauthorizedAccessException_Returns403ProblemDetails()
    {
        var facade = new FakeClientFacade { DetailExceptionToThrow = new UnauthorizedAccessException("Not authorized.") };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.GetAsync($"{ClientsApiContract.Route}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("forbidden", root.GetProperty("errorCode").GetString());
    }

    // --- ChangeLifecycleStatus (CLIENT-010..015, API-001..007, SEC-012..013, DATA-008) ---

    private static ChangeClientLifecycleStatusViewModel ValidLifecycleRequest(
        ClientLifecycleStatusContract newStatus = ClientLifecycleStatusContract.Active,
        string expectedConcurrencyToken = "dGVzdA==") => new()
        {
            NewStatus = newStatus,
            ExpectedConcurrencyToken = expectedConcurrencyToken,
        };

    private static string LifecycleRoute(Guid clientId) => $"{ClientsApiContract.Route}/{clientId}/lifecycle-status";

    [Fact]
    public async Task ChangeLifecycleStatus_WhenAuthenticatedAndValid_Returns200WithTheFacadesResponseBody()
    {
        var expectedResponse = BuildResponse() with { LifecycleStatus = ClientLifecycleStatusContract.Active };
        var facade = new FakeClientFacade { LifecycleResultToReturn = expectedResponse };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.PatchAsJsonAsync(LifecycleRoute(expectedResponse.Id), ValidLifecycleRequest());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ClientServiceModel>();
        Assert.NotNull(body);
        Assert.Equal(expectedResponse.Id, body!.Id);
        Assert.Equal(ClientLifecycleStatusContract.Active, body.LifecycleStatus);
        Assert.True(facade.LifecycleWasCalled);
        Assert.Equal(ClientLifecycleStatusContract.Active, facade.ReceivedLifecycleRequest?.NewStatus);
        Assert.Equal("dGVzdA==", facade.ReceivedLifecycleRequest?.ExpectedConcurrencyToken);
    }

    [Fact]
    public async Task ChangeLifecycleStatus_WhenFacadeReturnsNull_Returns404ProblemDetails()
    {
        var facade = new FakeClientFacade { LifecycleResultToReturn = null };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.PatchAsJsonAsync(LifecycleRoute(Guid.NewGuid()), ValidLifecycleRequest());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("resource_not_found", root.GetProperty("errorCode").GetString());
        Assert.True(facade.LifecycleWasCalled);
    }

    // --- Invalid transition (CLIENT-010..015 - ClientLifecycleTransitionRules rejection) ---

    [Fact]
    public async Task ChangeLifecycleStatus_WhenFacadeThrowsInvalidOperationException_Returns400ValidationProblemDetails()
    {
        var facade = new FakeClientFacade
        {
            LifecycleExceptionToThrow = new InvalidOperationException(
                "Client lifecycle status cannot transition from 'Archived' to 'Active'."),
        };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.PatchAsJsonAsync(LifecycleRoute(Guid.NewGuid()), ValidLifecycleRequest());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("validation_failed", root.GetProperty("errorCode").GetString());
        Assert.True(root.GetProperty("errors").TryGetProperty("NewStatus", out _));
    }

    // --- Stale version conflict (DATA-008) ---

    [Fact]
    public async Task ChangeLifecycleStatus_WhenFacadeThrowsClientConcurrencyConflictException_Returns409ProblemDetails()
    {
        var facade = new FakeClientFacade
        {
            LifecycleExceptionToThrow = new ClientConcurrencyConflictException(Guid.NewGuid()),
        };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.PatchAsJsonAsync(LifecycleRoute(Guid.NewGuid()), ValidLifecycleRequest());

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("concurrency_conflict", root.GetProperty("errorCode").GetString());
    }

    // --- Unauthenticated (401) ---

    [Fact]
    public async Task ChangeLifecycleStatus_WhenNoAuthenticatedActor_Returns401AndNeverCallsFacade()
    {
        var facade = new FakeClientFacade { LifecycleResultToReturn = BuildResponse() };
        using var factory = CreateFactory(facade, authenticated: false);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.PatchAsJsonAsync(LifecycleRoute(Guid.NewGuid()), ValidLifecycleRequest());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("authentication_required", root.GetProperty("errorCode").GetString());
        Assert.False(facade.LifecycleWasCalled);
    }

    // --- Forbidden (403 - Facade/IClientAuthorization policy rejection, SEC-012/013) ---

    [Fact]
    public async Task ChangeLifecycleStatus_WhenFacadeThrowsUnauthorizedAccessException_Returns403ProblemDetails()
    {
        var facade = new FakeClientFacade { LifecycleExceptionToThrow = new UnauthorizedAccessException("Not authorized.") };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.PatchAsJsonAsync(LifecycleRoute(Guid.NewGuid()), ValidLifecycleRequest());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("forbidden", root.GetProperty("errorCode").GetString());
    }

    // --- Validation (SEC-022; automatic [ApiController] model-state 400) ---

    [Fact]
    public async Task ChangeLifecycleStatus_WhenExpectedConcurrencyTokenIsMissing_Returns400ValidationProblemDetailsAndNeverCallsFacade()
    {
        var facade = new FakeClientFacade { LifecycleResultToReturn = BuildResponse() };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.PatchAsJsonAsync(
            LifecycleRoute(Guid.NewGuid()), ValidLifecycleRequest() with { ExpectedConcurrencyToken = null! });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("validation_failed", root.GetProperty("errorCode").GetString());
        Assert.False(facade.LifecycleWasCalled);
    }

    // --- Archive (CLIENT-013..015, API-001..007, SEC-012..013, DATA-008) ---

    private static ArchiveClientViewModel ValidArchiveRequest(
        string expectedConcurrencyToken = "dGVzdA==") => new()
        {
            ExpectedConcurrencyToken = expectedConcurrencyToken,
        };

    private static string ArchiveRoute(Guid clientId) => $"{ClientsApiContract.Route}/{clientId}/archive";

    [Fact]
    public async Task Archive_WhenAuthenticatedAndValid_Returns200WithTheFacadesResponseBody()
    {
        var expectedResponse = BuildResponse() with { LifecycleStatus = ClientLifecycleStatusContract.Archived };
        var facade = new FakeClientFacade { ArchiveResultToReturn = expectedResponse };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.PostAsJsonAsync(ArchiveRoute(expectedResponse.Id), ValidArchiveRequest());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ClientServiceModel>();
        Assert.NotNull(body);
        Assert.Equal(expectedResponse.Id, body!.Id);
        Assert.Equal(ClientLifecycleStatusContract.Archived, body.LifecycleStatus);
        Assert.True(facade.ArchiveWasCalled);
        Assert.Equal("dGVzdA==", facade.ReceivedArchiveRequest?.ExpectedConcurrencyToken);
    }

    [Fact]
    public async Task Archive_WhenFacadeReturnsNull_Returns404ProblemDetails()
    {
        var facade = new FakeClientFacade { ArchiveResultToReturn = null };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.PostAsJsonAsync(ArchiveRoute(Guid.NewGuid()), ValidArchiveRequest());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("resource_not_found", root.GetProperty("errorCode").GetString());
        Assert.True(facade.ArchiveWasCalled);
    }

    // --- Blocked archive (CLIENT-015: has active Projects) ---

    [Fact]
    public async Task Archive_WhenFacadeThrowsInvalidOperationException_Returns409Conflict()
    {
        var facade = new FakeClientFacade
        {
            ArchiveExceptionToThrow = new InvalidOperationException(
                "Cannot archive a Client with active Projects."),
        };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.PostAsJsonAsync(ArchiveRoute(Guid.NewGuid()), ValidArchiveRequest());

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("concurrency_conflict", root.GetProperty("errorCode").GetString());
    }

    // --- Stale version conflict (DATA-008) ---

    [Fact]
    public async Task Archive_WhenFacadeThrowsClientConcurrencyConflictException_Returns409ProblemDetails()
    {
        var facade = new FakeClientFacade
        {
            ArchiveExceptionToThrow = new ClientConcurrencyConflictException(Guid.NewGuid()),
        };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.PostAsJsonAsync(ArchiveRoute(Guid.NewGuid()), ValidArchiveRequest());

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("concurrency_conflict", root.GetProperty("errorCode").GetString());
    }

    // --- Unauthenticated (401) ---

    [Fact]
    public async Task Archive_WhenNoAuthenticatedActor_Returns401AndNeverCallsFacade()
    {
        var facade = new FakeClientFacade { ArchiveResultToReturn = BuildResponse() };
        using var factory = CreateFactory(facade, authenticated: false);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.PostAsJsonAsync(ArchiveRoute(Guid.NewGuid()), ValidArchiveRequest());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("authentication_required", root.GetProperty("errorCode").GetString());
        Assert.False(facade.ArchiveWasCalled);
    }

    // --- Forbidden (403 - Facade/IClientAuthorization policy rejection, SEC-012..013) ---

    [Fact]
    public async Task Archive_WhenFacadeThrowsUnauthorizedAccessException_Returns403ProblemDetails()
    {
        var facade = new FakeClientFacade { ArchiveExceptionToThrow = new UnauthorizedAccessException("Not authorized.") };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.PostAsJsonAsync(ArchiveRoute(Guid.NewGuid()), ValidArchiveRequest());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("forbidden", root.GetProperty("errorCode").GetString());
    }

    // --- Validation (SEC-022; automatic [ApiController] model-state 400) ---

    [Fact]
    public async Task Archive_WhenExpectedConcurrencyTokenIsMissing_Returns400ValidationProblemDetailsAndNeverCallsFacade()
    {
        var facade = new FakeClientFacade { ArchiveResultToReturn = BuildResponse() };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.PostAsJsonAsync(
            ArchiveRoute(Guid.NewGuid()), ValidArchiveRequest() with { ExpectedConcurrencyToken = null! });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("validation_failed", root.GetProperty("errorCode").GetString());
        Assert.False(facade.ArchiveWasCalled);
    }

    // --- Restore (CLIENT-013..014, API-001..007, SEC-012..013, DATA-008) ---

    private static RestoreClientViewModel ValidRestoreRequest(
        ClientLifecycleStatusContract restoredStatus = ClientLifecycleStatusContract.Active,
        string expectedConcurrencyToken = "dGVzdA==") => new()
        {
            RestoredStatus = restoredStatus,
            ExpectedConcurrencyToken = expectedConcurrencyToken,
        };

    private static string RestoreRoute(Guid clientId) => $"{ClientsApiContract.Route}/{clientId}/restore";

    [Fact]
    public async Task Restore_WhenAuthenticatedAndValid_Returns200WithTheFacadesResponseBody()
    {
        var expectedResponse = BuildResponse() with { LifecycleStatus = ClientLifecycleStatusContract.Active };
        var facade = new FakeClientFacade { RestoreResultToReturn = expectedResponse };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.PostAsJsonAsync(RestoreRoute(expectedResponse.Id), ValidRestoreRequest());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ClientServiceModel>();
        Assert.NotNull(body);
        Assert.Equal(expectedResponse.Id, body!.Id);
        Assert.Equal(ClientLifecycleStatusContract.Active, body.LifecycleStatus);
        Assert.True(facade.RestoreWasCalled);
        Assert.Equal(ClientLifecycleStatusContract.Active, facade.ReceivedRestoreRequest?.RestoredStatus);
        Assert.Equal("dGVzdA==", facade.ReceivedRestoreRequest?.ExpectedConcurrencyToken);
    }

    [Fact]
    public async Task Restore_WhenFacadeReturnsNull_Returns404ProblemDetails()
    {
        var facade = new FakeClientFacade { RestoreResultToReturn = null };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.PostAsJsonAsync(RestoreRoute(Guid.NewGuid()), ValidRestoreRequest());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("resource_not_found", root.GetProperty("errorCode").GetString());
        Assert.True(facade.RestoreWasCalled);
    }

    // --- Invalid restore (CLIENT-014: not currently Archived) ---

    [Fact]
    public async Task Restore_WhenFacadeThrowsInvalidOperationException_Returns400ValidationProblemDetails()
    {
        var facade = new FakeClientFacade
        {
            RestoreExceptionToThrow = new InvalidOperationException(
                "Client is not currently Archived."),
        };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.PostAsJsonAsync(RestoreRoute(Guid.NewGuid()), ValidRestoreRequest());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("validation_failed", root.GetProperty("errorCode").GetString());
        Assert.True(root.GetProperty("errors").TryGetProperty("RestoredStatus", out _));
    }

    // --- Stale version conflict (DATA-008) ---

    [Fact]
    public async Task Restore_WhenFacadeThrowsClientConcurrencyConflictException_Returns409ProblemDetails()
    {
        var facade = new FakeClientFacade
        {
            RestoreExceptionToThrow = new ClientConcurrencyConflictException(Guid.NewGuid()),
        };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.PostAsJsonAsync(RestoreRoute(Guid.NewGuid()), ValidRestoreRequest());

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("concurrency_conflict", root.GetProperty("errorCode").GetString());
    }

    // --- Unauthenticated (401) ---

    [Fact]
    public async Task Restore_WhenNoAuthenticatedActor_Returns401AndNeverCallsFacade()
    {
        var facade = new FakeClientFacade { RestoreResultToReturn = BuildResponse() };
        using var factory = CreateFactory(facade, authenticated: false);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.PostAsJsonAsync(RestoreRoute(Guid.NewGuid()), ValidRestoreRequest());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("authentication_required", root.GetProperty("errorCode").GetString());
        Assert.False(facade.RestoreWasCalled);
    }

    // --- Forbidden (403 - Facade/IClientAuthorization policy rejection, SEC-012..013) ---

    [Fact]
    public async Task Restore_WhenFacadeThrowsUnauthorizedAccessException_Returns403ProblemDetails()
    {
        var facade = new FakeClientFacade { RestoreExceptionToThrow = new UnauthorizedAccessException("Not authorized.") };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.PostAsJsonAsync(RestoreRoute(Guid.NewGuid()), ValidRestoreRequest());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("forbidden", root.GetProperty("errorCode").GetString());
    }

    // --- Validation (SEC-022; automatic [ApiController] model-state 400) ---

    [Fact]
    public async Task Restore_WhenExpectedConcurrencyTokenIsMissing_Returns400ValidationProblemDetailsAndNeverCallsFacade()
    {
        var facade = new FakeClientFacade { RestoreResultToReturn = BuildResponse() };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.PostAsJsonAsync(
            RestoreRoute(Guid.NewGuid()), ValidRestoreRequest() with { ExpectedConcurrencyToken = null! });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("validation_failed", root.GetProperty("errorCode").GetString());
        Assert.False(facade.RestoreWasCalled);
    }

    [Fact]
    public async Task Restore_WhenRestoredStatusIsMissing_Returns400ValidationProblemDetailsAndNeverCallsFacade()
    {
        var facade = new FakeClientFacade { RestoreResultToReturn = BuildResponse() };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        // Sending a request with RestoredStatus missing will fail JSON deserialization in [ApiController]
        var request = new { expectedConcurrencyToken = "dGVzdA==" };
        var response = await httpClient.PostAsJsonAsync(RestoreRoute(Guid.NewGuid()), request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("validation_failed", root.GetProperty("errorCode").GetString());
        Assert.False(facade.RestoreWasCalled);
    }

    // --- Update (CLIENT-002, API-001..007, SEC-012..013, DATA-008) ---

    private static UpdateClientViewModel ValidUpdateRequest(string? expectedConcurrencyToken = null) => new()
    {
        Name = "Updated Name",
        PrimaryEmail = "updated@example.com",
        ExpectedConcurrencyToken = expectedConcurrencyToken ?? "dGVzdA==",
    };

    private static string UpdateRoute(Guid clientId) => $"{ClientsApiContract.Route}/{clientId}";

    [Fact]
    public async Task Update_WhenAuthenticatedAndValid_Returns200WithTheFacadesResponseBody()
    {
        var clientId = Guid.NewGuid();
        var expectedResponse = BuildResponse() with { Name = "Updated Name" };
        var facade = new FakeClientFacade { UpdateResultToReturn = expectedResponse };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.PatchAsJsonAsync(UpdateRoute(clientId), ValidUpdateRequest());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ClientServiceModel>();
        Assert.NotNull(body);
        Assert.Equal("Updated Name", body!.Name);
        Assert.True(facade.UpdateWasCalled);
        Assert.Equal(clientId, facade.ReceivedUpdateClientId);
    }

    [Fact]
    public async Task Update_PassesTheBoundRequestFieldsToTheFacade()
    {
        var clientId = Guid.NewGuid();
        var facade = new FakeClientFacade { UpdateResultToReturn = BuildResponse() };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var request = new UpdateClientViewModel
        {
            Name = "New Name",
            PrimaryEmail = "new@example.com",
            City = "New City",
            ExpectedConcurrencyToken = "token123",
        };
        await httpClient.PatchAsJsonAsync(UpdateRoute(clientId), request);

        Assert.True(facade.UpdateWasCalled);
        Assert.Equal("New Name", facade.ReceivedUpdateRequest?.Name);
        Assert.Equal("new@example.com", facade.ReceivedUpdateRequest?.PrimaryEmail);
        Assert.Equal("New City", facade.ReceivedUpdateRequest?.City);
        Assert.Equal("token123", facade.ReceivedUpdateRequest?.ExpectedConcurrencyToken);
    }

    [Fact]
    public async Task Update_WithPartialFieldUpdate_PassesOnlyProvidedFieldsToTheFacade()
    {
        var clientId = Guid.NewGuid();
        var facade = new FakeClientFacade { UpdateResultToReturn = BuildResponse() };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var request = new UpdateClientViewModel
        {
            Name = "Only Update Name",
            ExpectedConcurrencyToken = "dGVzdA==",
        };
        await httpClient.PatchAsJsonAsync(UpdateRoute(clientId), request);

        Assert.True(facade.UpdateWasCalled);
        Assert.Equal("Only Update Name", facade.ReceivedUpdateRequest?.Name);
        Assert.Null(facade.ReceivedUpdateRequest?.City);
        Assert.Null(facade.ReceivedUpdateRequest?.PrimaryPhone);
    }

    [Fact]
    public async Task Update_WhenFacadeReturnsNull_Returns404ProblemDetails()
    {
        var clientId = Guid.NewGuid();
        var facade = new FakeClientFacade { UpdateResultToReturn = null };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.PatchAsJsonAsync(UpdateRoute(clientId), ValidUpdateRequest());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("not_found", root.GetProperty("errorCode").GetString());
        Assert.True(facade.UpdateWasCalled);
    }

    [Fact]
    public async Task Update_WhenFacadeThrowsClientConcurrencyConflictException_Returns409ProblemDetails()
    {
        var clientId = Guid.NewGuid();
        var facade = new FakeClientFacade
        {
            UpdateExceptionToThrow = new ClientConcurrencyConflictException(clientId),
        };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.PatchAsJsonAsync(UpdateRoute(clientId), ValidUpdateRequest());

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("concurrency_conflict", root.GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task Update_WhenNoAuthenticatedActor_Returns401AndNeverCallsFacade()
    {
        var clientId = Guid.NewGuid();
        var facade = new FakeClientFacade { UpdateResultToReturn = BuildResponse() };
        using var factory = CreateFactory(facade, authenticated: false);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.PatchAsJsonAsync(UpdateRoute(clientId), ValidUpdateRequest());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("authentication_required", root.GetProperty("errorCode").GetString());
        Assert.False(facade.UpdateWasCalled);
    }

    [Fact]
    public async Task Update_WhenFacadeThrowsUnauthorizedAccessException_Returns403ProblemDetails()
    {
        var clientId = Guid.NewGuid();
        var facade = new FakeClientFacade { UpdateExceptionToThrow = new UnauthorizedAccessException("Not authorized.") };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.PatchAsJsonAsync(UpdateRoute(clientId), ValidUpdateRequest());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("forbidden", root.GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task Update_WhenEmailIsMalformed_Returns400ValidationProblemDetails()
    {
        var clientId = Guid.NewGuid();
        var facade = new FakeClientFacade { UpdateResultToReturn = BuildResponse() };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var request = ValidUpdateRequest() with { PrimaryEmail = "not-an-email" };
        var response = await httpClient.PatchAsJsonAsync(UpdateRoute(clientId), request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(facade.UpdateWasCalled);
    }

    [Fact]
    public async Task Update_WhenNameIsTooLong_Returns400ValidationProblemDetails()
    {
        var clientId = Guid.NewGuid();
        var facade = new FakeClientFacade { UpdateResultToReturn = BuildResponse() };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var longName = new string('a', 201);
        var request = ValidUpdateRequest() with { Name = longName };
        var response = await httpClient.PatchAsJsonAsync(UpdateRoute(clientId), request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(facade.UpdateWasCalled);
    }

    [Fact]
    public async Task Update_WhenExpectedConcurrencyTokenIsMissing_Returns400ValidationProblemDetailsAndNeverCallsFacade()
    {
        var clientId = Guid.NewGuid();
        var facade = new FakeClientFacade { UpdateResultToReturn = BuildResponse() };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.PatchAsJsonAsync(
            UpdateRoute(clientId), ValidUpdateRequest() with { ExpectedConcurrencyToken = null! });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("validation_failed", root.GetProperty("errorCode").GetString());
        Assert.False(facade.UpdateWasCalled);
    }

    private static WebApplicationFactory<Program> CreateFactory(FakeClientFacade facade, bool authenticated)
    {
        Environment.SetEnvironmentVariable(
            CrmDbConnectionStringEnvironmentVariable,
            "Server=localhost;Database=CrmDbClientsControllerTests;TrustServerCertificate=True;");

        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.AddScoped<IClientFacade>(_ => facade);

                // Configure test authentication scheme for all tests (required for authorization middleware)
                services.AddAuthentication("TestScheme")
                    .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("TestScheme", _ => { });
                // Add startup filter that sets the test user with Manager role when authenticated is true
                services.AddSingleton<IStartupFilter>(new AuthenticatedActorStartupFilter(authenticated));
            }));
    }

    // Fake IClientFacade (mirrors ClientFacadeTests' hand-written fake style - no mocking library is
    // used in this repository).
    private sealed class FakeClientFacade : IClientFacade
    {
        public ClientServiceModel ResultToReturn { get; init; } = null!;

        public Exception? ExceptionToThrow { get; init; }

        public bool WasCalled { get; private set; }

        public CreateClientViewModel? ReceivedRequest { get; private set; }

        public Task<ClientServiceModel> CreateAsync(CreateClientViewModel request, CancellationToken cancellationToken)
        {
            WasCalled = true;
            ReceivedRequest = request;

            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }

            return Task.FromResult(ResultToReturn);
        }

        public PagedResponse<ClientServiceModel> ListResultToReturn { get; init; } = null!;

        public Exception? ListExceptionToThrow { get; init; }

        public bool ListWasCalled { get; private set; }

        public ListClientsRequest? ReceivedListRequest { get; private set; }

        public Task<PagedResponse<ClientServiceModel>> ListAsync(
            ListClientsRequest request, CancellationToken cancellationToken)
        {
            ListWasCalled = true;
            ReceivedListRequest = request;

            if (ListExceptionToThrow is not null)
            {
                throw ListExceptionToThrow;
            }

            return Task.FromResult(ListResultToReturn);
        }

        public ClientDetailServiceModel? DetailResultToReturn { get; init; }

        public Exception? DetailExceptionToThrow { get; init; }

        public bool DetailWasCalled { get; private set; }

        public Guid? ReceivedDetailClientId { get; private set; }

        public Task<ClientDetailServiceModel?> GetDetailAsync(Guid clientId, CancellationToken cancellationToken)
        {
            DetailWasCalled = true;
            ReceivedDetailClientId = clientId;

            if (DetailExceptionToThrow is not null)
            {
                throw DetailExceptionToThrow;
            }

            return Task.FromResult(DetailResultToReturn);
        }

        public ClientServiceModel? LifecycleResultToReturn { get; init; }

        public Exception? LifecycleExceptionToThrow { get; init; }

        public bool LifecycleWasCalled { get; private set; }

        public Guid? ReceivedLifecycleClientId { get; private set; }

        public ChangeClientLifecycleStatusViewModel? ReceivedLifecycleRequest { get; private set; }

        public Task<ClientServiceModel?> ChangeLifecycleStatusAsync(
            Guid clientId, ChangeClientLifecycleStatusViewModel request, CancellationToken cancellationToken)
        {
            LifecycleWasCalled = true;
            ReceivedLifecycleClientId = clientId;
            ReceivedLifecycleRequest = request;

            if (LifecycleExceptionToThrow is not null)
            {
                throw LifecycleExceptionToThrow;
            }

            return Task.FromResult(LifecycleResultToReturn);
        }

        public ClientServiceModel? ArchiveResultToReturn { get; init; }

        public Exception? ArchiveExceptionToThrow { get; init; }

        public bool ArchiveWasCalled { get; private set; }

        public Guid? ReceivedArchiveClientId { get; private set; }

        public ArchiveClientViewModel? ReceivedArchiveRequest { get; private set; }

        public Task<ClientServiceModel?> ArchiveAsync(
            Guid clientId, ArchiveClientViewModel request, CancellationToken cancellationToken)
        {
            ArchiveWasCalled = true;
            ReceivedArchiveClientId = clientId;
            ReceivedArchiveRequest = request;

            if (ArchiveExceptionToThrow is not null)
            {
                throw ArchiveExceptionToThrow;
            }

            return Task.FromResult(ArchiveResultToReturn);
        }

        public ClientServiceModel? RestoreResultToReturn { get; init; }

        public Exception? RestoreExceptionToThrow { get; init; }

        public bool RestoreWasCalled { get; private set; }

        public Guid? ReceivedRestoreClientId { get; private set; }

        public RestoreClientViewModel? ReceivedRestoreRequest { get; private set; }

        public Task<ClientServiceModel?> RestoreAsync(
            Guid clientId, RestoreClientViewModel request, CancellationToken cancellationToken)
        {
            RestoreWasCalled = true;
            ReceivedRestoreClientId = clientId;
            ReceivedRestoreRequest = request;

            if (RestoreExceptionToThrow is not null)
            {
                throw RestoreExceptionToThrow;
            }

            return Task.FromResult(RestoreResultToReturn);
        }

        public ClientServiceModel? UpdateResultToReturn { get; init; }

        public Exception? UpdateExceptionToThrow { get; init; }

        public bool UpdateWasCalled { get; private set; }

        public Guid? ReceivedUpdateClientId { get; private set; }

        public UpdateClientViewModel? ReceivedUpdateRequest { get; private set; }

        public Task<ClientServiceModel?> UpdateAsync(
            Guid clientId, UpdateClientViewModel request, CancellationToken cancellationToken)
        {
            UpdateWasCalled = true;
            ReceivedUpdateClientId = clientId;
            ReceivedUpdateRequest = request;

            if (UpdateExceptionToThrow is not null)
            {
                throw UpdateExceptionToThrow;
            }

            return Task.FromResult(UpdateResultToReturn);
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

using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using ProjectChicago.Crm.Contracts.Clients;
using ProjectChicago.Crm.Core.Facades;
using ProjectChicago.Crm.Core.Models.DataModels.Entities;
using Xunit;
using CreateClientRequestCore = ProjectChicago.Crm.Core.Models.ServiceModels.CreateClientRequest;
using CoreClientCreationResult = ProjectChicago.Crm.Core.Models.ServiceModels.ClientCreationResult;
using CoreDuplicateCandidate = ProjectChicago.Crm.Core.Models.ServiceModels.ClientDuplicateCandidate;
using CoreDuplicateMatchField = ProjectChicago.Crm.Core.Models.ServiceModels.ClientDuplicateMatchField;

namespace ProjectChicago.Crm.Api.Tests.Controllers;

// End-to-end HTTP tests for POST /api/clients (CLIENT-001..004, API-001..007, SEC-010..013,
// ERROR-001..005) against the real Crm Program.cs composition root - proves ClientsController's
// transport mapping/status-code behavior, not Facade/Business/Data (those are covered by
// ProjectChicago.Crm.Core.Tests). IClientFacade is replaced with a hand-written fake per test
// (mirrors ClientFacadeTests' fake style; no mocking library is used in this repository), since the
// production Facade->Business->Data->Repository->DbContext chain and its IClientAuthorization/IClock
// adapters are not wired in Program.cs yet (composition-root work explicitly out of scope for this
// controller-only microstep).
public class ClientsControllerTests
{
    private const string CrmDbConnectionStringEnvironmentVariable = "ConnectionStrings__CrmDb";
    private static readonly DateTime FixedUtcNow = new(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);

    private static CreateClientRequest ValidRequest() => new()
    {
        Name = "Acme Corporation",
        OwnerUserId = "owner-1",
        PrimaryEmail = "jane@acme.example",
    };

    private static Client BuildPersistedClient(string name = "Acme Corporation", string ownerUserId = "owner-1") =>
        Client.Create(
            id: Guid.NewGuid(),
            name: name,
            lifecycleStatus: ClientLifecycleStatus.Lead,
            ownerUserId: ownerUserId,
            createdBy: "actor-1",
            createdAtUtc: FixedUtcNow);

    // --- Success (CLIENT-001..003) ---

    [Fact]
    public async Task Create_WhenAuthenticatedAndValid_Returns201WithLocationAndMappedBody()
    {
        var client = BuildPersistedClient();
        var facade = new FakeClientFacade
        {
            ResultToReturn = new CoreClientCreationResult { Client = client },
        };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.PostAsJsonAsync(ClientsApiContract.Route, ValidRequest());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal($"{ClientsApiContract.Route}/{client.Id}", response.Headers.Location?.ToString());

        var body = await response.Content.ReadFromJsonAsync<ClientResponse>();
        Assert.NotNull(body);
        Assert.Equal(client.Id, body!.Id);
        Assert.Equal(client.Name, body.Name);
        Assert.Equal(client.OwnerUserId, body.OwnerUserId);
        Assert.Equal(ClientLifecycleStatusContract.Lead, body.LifecycleStatus);
        Assert.Empty(body.PossibleDuplicates);
    }

    [Fact]
    public async Task Create_PassesTheBoundRequestFieldsToTheFacade()
    {
        var facade = new FakeClientFacade
        {
            ResultToReturn = new CoreClientCreationResult { Client = BuildPersistedClient() },
        };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        await httpClient.PostAsJsonAsync(ClientsApiContract.Route, ValidRequest() with { Name = "Contoso Ltd" });

        Assert.True(facade.WasCalled);
        Assert.Equal("Contoso Ltd", facade.ReceivedRequest?.Name);
        Assert.Equal("owner-1", facade.ReceivedRequest?.OwnerUserId);
    }

    // --- Duplicate-policy result (CLIENT-004: warns, never blocks) ---

    [Fact]
    public async Task Create_WhenBusinessReturnsPossibleDuplicates_Returns201WithDuplicatesInBody()
    {
        var client = BuildPersistedClient();
        var facade = new FakeClientFacade
        {
            ResultToReturn = new CoreClientCreationResult
            {
                Client = client,
                PossibleDuplicates =
                [
                    new CoreDuplicateCandidate
                    {
                        ClientId = Guid.NewGuid(),
                        Name = "Acme Corp",
                        MatchedOn = [CoreDuplicateMatchField.Name, CoreDuplicateMatchField.PrimaryEmail],
                    },
                ],
            },
        };
        using var factory = CreateFactory(facade, authenticated: true);
        using var httpClient = factory.CreateClient();

        var response = await httpClient.PostAsJsonAsync(ClientsApiContract.Route, ValidRequest());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ClientResponse>();
        var duplicate = Assert.Single(body!.PossibleDuplicates);
        Assert.Equal("Acme Corp", duplicate.Name);
        Assert.Contains(ClientDuplicateMatchField.Name, duplicate.MatchedOn);
        Assert.Contains(ClientDuplicateMatchField.PrimaryEmail, duplicate.MatchedOn);
    }

    // --- Validation (SEC-022; automatic [ApiController] model-state 400) ---

    [Fact]
    public async Task Create_WhenNameIsMissing_Returns400ValidationProblemDetailsAndNeverCallsFacade()
    {
        var facade = new FakeClientFacade { ResultToReturn = new CoreClientCreationResult { Client = BuildPersistedClient() } };
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
        var facade = new FakeClientFacade { ResultToReturn = new CoreClientCreationResult { Client = BuildPersistedClient() } };
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
        var facade = new FakeClientFacade { ResultToReturn = new CoreClientCreationResult { Client = BuildPersistedClient() } };
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

    private static WebApplicationFactory<Program> CreateFactory(FakeClientFacade facade, bool authenticated)
    {
        Environment.SetEnvironmentVariable(
            CrmDbConnectionStringEnvironmentVariable,
            "Server=localhost;Database=CrmDbClientsControllerTests;TrustServerCertificate=True;");

        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.AddScoped<IClientFacade>(_ => facade);

                if (authenticated)
                {
                    services.AddSingleton<IStartupFilter>(new AuthenticatedActorStartupFilter());
                }
            }));
    }

    // Fake IClientFacade (mirrors ClientFacadeTests' hand-written fake style - no mocking library is
    // used in this repository).
    private sealed class FakeClientFacade : IClientFacade
    {
        public CoreClientCreationResult ResultToReturn { get; init; } = null!;

        public Exception? ExceptionToThrow { get; init; }

        public bool WasCalled { get; private set; }

        public CreateClientRequestCore? ReceivedRequest { get; private set; }

        public Task<CoreClientCreationResult> CreateAsync(CreateClientRequestCore request, CancellationToken cancellationToken)
        {
            WasCalled = true;
            ReceivedRequest = request;

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
    // ApiExceptionHandlingHostTests uses for its test-only throwing route.
    private sealed class AuthenticatedActorStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, nextMiddleware) =>
            {
                var identity = new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, "actor-1")],
                    authenticationType: "Test");
                context.User = new ClaimsPrincipal(identity);
                return nextMiddleware();
            });

            next(app);
        };
    }
}

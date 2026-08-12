using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ProjectChicago.Crm.Api.Tests;

// ERROR-001..005/TRACE-001..007/LOG-001..006: proves the real Crm Program.cs composition root -
// not just the underlying ServiceDefaults building blocks in isolation - actually wires
// AddApiExceptionHandling/AddHttpRequestContext/UseExceptionHandler/UseStatusCodePages into one
// working pipeline. Every response the host returns for a failure (whether from an unhandled
// exception or a plain status code) must carry the shared safe Problem Details shape.
public class ApiExceptionHandlingHostTests
{
    private const string CrmDbConnectionStringEnvironmentVariable = "ConnectionStrings__CrmDb";
    private const string ThrowingTestRoute = "/__test/throw";

    [Fact]
    public async Task UnhandledException_ReachingTheRealHostPipeline_ReturnsSafeProblemDetailsWithTraceReference()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(ThrowingTestRoute);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadAsStringAsync();
        var root = JsonDocument.Parse(body).RootElement;

        Assert.Equal("internal_error", root.GetProperty("errorCode").GetString());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("traceId").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("supportReferenceId").GetString()));
        Assert.DoesNotContain("at ProjectChicago", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SqlServerConnectionSecretMarker", body);
    }

    [Fact]
    public async Task UnhandledException_PropagatedCorrelationId_IsEchoedBackAsSupportReferenceId()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Correlation-Id", "test-correlation-99");

        var response = await client.GetAsync(ThrowingTestRoute);

        var body = await response.Content.ReadAsStringAsync();
        var root = JsonDocument.Parse(body).RootElement;
        Assert.Equal("test-correlation-99", root.GetProperty("supportReferenceId").GetString());
    }

    [Fact]
    public async Task UnmatchedRoute_ReturnsProblemDetailsShapedNotFound_NotABareEmptyBody404()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/this-route-does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        var root = JsonDocument.Parse(body).RootElement;
        Assert.Equal("resource_not_found", root.GetProperty("errorCode").GetString());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("traceId").GetString()));
    }

    private static WebApplicationFactory<Program> CreateFactory()
    {
        Environment.SetEnvironmentVariable(
            CrmDbConnectionStringEnvironmentVariable,
            "Server=localhost;Database=CrmDbApiExceptionHandlingHostTests;TrustServerCertificate=True;");

        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.AddSingleton<IStartupFilter, ThrowingRouteStartupFilter>()));
    }

    // Appends a test-only terminal endpoint after the host's own pipeline (UseExceptionHandler,
    // UseStatusCodePages, MapDefaultEndpoints) is already registered, so the thrown exception is
    // still caught by the real ApiExceptionHandler exactly as it would be for any future
    // controller action - without adding a route to production Program.cs (backend.md:
    // "do not add minimal API routes").
    private sealed class ThrowingRouteStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            next(app);

            app.Map(ThrowingTestRoute, branch => branch.Run(_ =>
                throw new InvalidOperationException(
                    "Simulated unexpected failure - SqlServerConnectionSecretMarker should never reach the client")));
        };
    }
}

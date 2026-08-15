using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ProjectChicago.Audit.Api.Tests;

/// <summary>
/// Integration test fixture for Audit API endpoints (AUDIT-001..008, SEC-012, TEST-002).
/// Hosts the ProjectChicago.Audit ASP.NET Core application in memory and provides factory methods
/// for creating HttpClient instances with various authentication/authorization states for testing
/// 401 (unauthenticated) and 403 (forbidden) scenarios.
/// </summary>
public sealed class AuditTestFixture : IAsyncLifetime
{
    private WebApplicationFactory<Program>? _factory;
    private WebApplication? _app;

    public async Task InitializeAsync()
    {
        // Build and start the in-memory test server hosting the Audit API.
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                // Configure the test server to use an in-memory database.
                // This allows audit tests to run without external SQL Server dependency,
                // though SQL integration tests would use MsSqlContainerFixture.
                builder.ConfigureServices(services =>
                {
                    // Optional: swap real database for in-memory for faster unit-style integration tests.
                    // For now, this fixture assumes a real database is available or uses Testcontainers
                    // in parent test classes if needed.
                });

                builder.ConfigureTestServices(services =>
                {
                    // Test-specific service overrides (e.g., mock clock for time-dependent tests)
                    // can be added here.
                });
            });

        // The factory is fully initialized and ready to create test clients.
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        if (_factory != null)
        {
            await _factory.DisposeAsync();
        }

        if (_app != null)
        {
            await _app.DisposeAsync();
        }
    }

    /// <summary>
    /// Create an HttpClient with no authentication claims (simulates unauthenticated request).
    /// User.Identity.IsAuthenticated will be false; the controller returns 401.
    /// </summary>
    public HttpClient CreateUnauthenticatedClient()
    {
        if (_factory == null)
        {
            throw new InvalidOperationException("Fixture not initialized. Call InitializeAsync first.");
        }

        return _factory.CreateClient();
    }

    /// <summary>
    /// Create an HttpClient with a test user having the specified role(s).
    /// The client includes an Authorization header with claims for the given role.
    /// Simulates an authenticated request from a user in that role.
    /// </summary>
    public HttpClient CreateClientWithRole(string role)
    {
        if (_factory == null)
        {
            throw new InvalidOperationException("Fixture not initialized. Call InitializeAsync first.");
        }

        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                // Override authentication to use test scheme with specified role.
                // This adds a test authentication handler that automatically authenticates
                // every request with the given role claim.
                services.AddAuthentication("TestScheme")
                    .AddScheme<TestAuthenticationSchemeOptions, TestAuthenticationHandler>(
                        "TestScheme",
                        options => options.Role = role);
            });
        }).CreateClient();

        // Add Authorization header to trigger TestAuthenticationHandler authentication.
        client.DefaultRequestHeaders.Add("Authorization", "Bearer test-token");

        return client;
    }

    /// <summary>
    /// Test authentication scheme options.
    /// </summary>
    private class TestAuthenticationSchemeOptions : Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions
    {
        public string Role { get; set; } = string.Empty;
    }

    /// <summary>
    /// Test authentication handler that creates a test user with the specified role.
    /// </summary>
    private class TestAuthenticationHandler : Microsoft.AspNetCore.Authentication.AuthenticationHandler<TestAuthenticationSchemeOptions>
    {
        public TestAuthenticationHandler(
            Microsoft.Extensions.Options.IOptionsMonitor<TestAuthenticationSchemeOptions> options,
            Microsoft.Extensions.Logging.ILoggerFactory logger,
            System.Text.Encodings.Web.UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<Microsoft.AspNetCore.Authentication.AuthenticateResult> HandleAuthenticateAsync()
        {
            // Create a principal with the test role.
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.Name, "TestUser"),
                new Claim(ClaimTypes.Role, Options.Role)
            };

            var identity = new ClaimsIdentity(claims, "TestScheme");
            var principal = new ClaimsPrincipal(identity);
            var ticket = new Microsoft.AspNetCore.Authentication.AuthenticationTicket(principal, "TestScheme");

            return Task.FromResult(Microsoft.AspNetCore.Authentication.AuthenticateResult.Success(ticket));
        }
    }
}

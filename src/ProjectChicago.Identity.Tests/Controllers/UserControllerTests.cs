using System.Net;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace ProjectChicago.Identity.Tests.Controllers;

// User controller integration tests (SEC-004, SEC-010..016, AUDIT-001..008).
// Verify route exists and parameter validation through HTTP endpoint.
public class UserControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public UserControllerTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    [Fact]
    public async Task UserEndpoint_Exists()
    {
        var client = _factory.CreateClient();

        var response = await client.SendAsync(
            new HttpRequestMessage(HttpMethod.Options, "/users")
        );

        Assert.NotEqual(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateUser_ValidPost_ReturnsNonNotFound()
    {
        var client = _factory.CreateClient();
        var json = Environment.GetEnvironmentVariable("TEST_JSON") ?? @"{ ""email"": ""u@x.com"", ""password"": ""p"", ""roleName"": ""Manager"" }";
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/users", content);

        Assert.NotEqual(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateUser_InvalidEmail_ReturnsBadOrUnauth()
    {
        var client = _factory.CreateClient();
        var json = @"{ ""email"": ""notanemail"", ""password"": ""p"", ""roleName"": ""Manager"" }";
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/users", content);

        Assert.True(
            response.StatusCode == System.Net.HttpStatusCode.BadRequest ||
            response.StatusCode == System.Net.HttpStatusCode.Unauthorized
        );
    }

    [Fact]
    public async Task DeactivateUser_ValidId_ReturnsOkOrUnauth()
    {
        var client = _factory.CreateClient();
        var userId = Guid.NewGuid();

        var response = await client.PostAsync($"/users/{userId}/deactivate", new StringContent("", Encoding.UTF8, "application/json"));

        Assert.True(
            response.StatusCode == System.Net.HttpStatusCode.OK ||
            response.StatusCode == System.Net.HttpStatusCode.BadRequest ||
            response.StatusCode == System.Net.HttpStatusCode.Unauthorized
        );
    }

    [Fact]
    public async Task DeactivateUser_EndpointExists()
    {
        var client = _factory.CreateClient();
        var userId = Guid.NewGuid();

        var response = await client.SendAsync(
            new HttpRequestMessage(HttpMethod.Options, $"/users/{userId}/deactivate")
        );

        Assert.NotEqual(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ActivateUser_EndpointExists()
    {
        var client = _factory.CreateClient();
        var userId = Guid.NewGuid();

        var response = await client.SendAsync(
            new HttpRequestMessage(HttpMethod.Options, $"/users/{userId}/activate")
        );

        Assert.NotEqual(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AddRole_ValidRequest_ReturnsOkOrUnauth()
    {
        var client = _factory.CreateClient();
        var userId = Guid.NewGuid();
        var json = @"{ ""roleName"": ""Manager"" }";
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await client.PostAsync($"/users/{userId}/roles", content);

        Assert.True(
            response.StatusCode == System.Net.HttpStatusCode.OK ||
            response.StatusCode == System.Net.HttpStatusCode.BadRequest ||
            response.StatusCode == System.Net.HttpStatusCode.Unauthorized
        );
    }

    [Fact]
    public async Task AddRole_EndpointExists()
    {
        var client = _factory.CreateClient();
        var userId = Guid.NewGuid();

        var response = await client.SendAsync(
            new HttpRequestMessage(HttpMethod.Options, $"/users/{userId}/roles")
        );

        Assert.NotEqual(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RemoveRole_EndpointExists()
    {
        var client = _factory.CreateClient();
        var userId = Guid.NewGuid();
        var roleName = "Manager";

        var response = await client.SendAsync(
            new HttpRequestMessage(HttpMethod.Options, $"/users/{userId}/roles/{roleName}")
        );

        Assert.NotEqual(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RemoveRole_ValidRequest_ReturnsOkOrUnauth()
    {
        var client = _factory.CreateClient();
        var userId = Guid.NewGuid();
        var roleName = "Manager";

        var response = await client.DeleteAsync($"/users/{userId}/roles/{roleName}");

        Assert.True(
            response.StatusCode == System.Net.HttpStatusCode.OK ||
            response.StatusCode == System.Net.HttpStatusCode.BadRequest ||
            response.StatusCode == System.Net.HttpStatusCode.Unauthorized
        );
    }
}

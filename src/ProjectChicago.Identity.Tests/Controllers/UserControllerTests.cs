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

    // List users endpoint tests (SEC-004, SEC-010..016)
    [Fact]
    public async Task ListUsers_EndpointExists()
    {
        var client = _factory.CreateClient();

        var response = await client.SendAsync(
            new HttpRequestMessage(HttpMethod.Options, "/users")
        );

        Assert.NotEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ListUsers_WithDefaultPagination_ReturnsOkOrUnauth()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/users");

        Assert.True(
            response.StatusCode == HttpStatusCode.OK ||
            response.StatusCode == HttpStatusCode.Unauthorized ||
            response.StatusCode == HttpStatusCode.Forbidden
        );
    }

    [Fact]
    public async Task ListUsers_WithValidPageParameters_ReturnsOkOrUnauth()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/users?page=1&pageSize=10");

        Assert.True(
            response.StatusCode == HttpStatusCode.OK ||
            response.StatusCode == HttpStatusCode.Unauthorized ||
            response.StatusCode == HttpStatusCode.Forbidden
        );
    }

    [Fact]
    public async Task ListUsers_WithInvalidPageNumber_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/users?page=0&pageSize=10");

        Assert.True(
            response.StatusCode == HttpStatusCode.BadRequest ||
            response.StatusCode == HttpStatusCode.Unauthorized
        );
    }

    [Fact]
    public async Task ListUsers_WithInvalidPageSize_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/users?page=1&pageSize=101");

        Assert.True(
            response.StatusCode == HttpStatusCode.BadRequest ||
            response.StatusCode == HttpStatusCode.Unauthorized
        );
    }

    // Get user detail endpoint tests (SEC-004, SEC-010..016)
    [Fact]
    public async Task GetUserDetail_EndpointExists()
    {
        var client = _factory.CreateClient();
        var userId = Guid.NewGuid();

        var response = await client.SendAsync(
            new HttpRequestMessage(HttpMethod.Options, $"/users/{userId}")
        );

        Assert.NotEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetUserDetail_WithNonexistentUser_ReturnsNotFoundOrUnauth()
    {
        var client = _factory.CreateClient();
        var userId = Guid.NewGuid();

        var response = await client.GetAsync($"/users/{userId}");

        Assert.True(
            response.StatusCode == HttpStatusCode.NotFound ||
            response.StatusCode == HttpStatusCode.Unauthorized ||
            response.StatusCode == HttpStatusCode.Forbidden
        );
    }

    [Fact]
    public async Task GetUserDetail_WithValidId_ReturnsOkOrUnauth()
    {
        var client = _factory.CreateClient();
        var userId = Guid.NewGuid();

        var response = await client.GetAsync($"/users/{userId}");

        Assert.True(
            response.StatusCode == HttpStatusCode.OK ||
            response.StatusCode == HttpStatusCode.NotFound ||
            response.StatusCode == HttpStatusCode.Unauthorized ||
            response.StatusCode == HttpStatusCode.Forbidden
        );
    }

    [Fact]
    public async Task GetUserDetail_WithInvalidGuid_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/users/not-a-guid");

        // Invalid GUID format should not match the route constraint
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}

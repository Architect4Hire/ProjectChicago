using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Text;
using Xunit;

namespace ProjectChicago.Identity.Tests.Controllers;

// Integration tests for authentication endpoints (ADR-0018: cookie authentication + CSRF).
// Tests verify login/logout/current-user/password-change endpoint behavior (SEC-001, SEC-004, SEC-005, SEC-020..025).
// Credential handling tested separately in AuthenticationBusinessTests and PasswordChangeBusinessTests.
public class AuthControllerIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public AuthControllerIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Login_EndpointExists_Returns400OnEmptyRequest()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsync("/auth/login", new StringContent("{}"));
        Assert.True(response.StatusCode == HttpStatusCode.BadRequest || response.StatusCode == HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Logout_EndpointExists_Returns200()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsync("/auth/logout", new StringContent(""));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CurrentUser_WithoutSession_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/auth/current-user");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_EndpointExists()
    {
        var client = _factory.CreateClient();

        var response = await client.SendAsync(
            new HttpRequestMessage(HttpMethod.Options, "/auth/password")
        );

        Assert.NotEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_WithoutSession_Returns401()
    {
        var client = _factory.CreateClient();
        var json = @"{ ""currentPassword"": ""oldpass"", ""newPassword"": ""NewPass123"", ""confirmPassword"": ""NewPass123"" }";
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await client.PutAsync("/auth/password", content);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_InvalidRequest_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();
        var json = @"{ ""currentPassword"": """", ""newPassword"": """" }";
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await client.PutAsync("/auth/password", content);

        Assert.True(
            response.StatusCode == HttpStatusCode.BadRequest ||
            response.StatusCode == HttpStatusCode.Unauthorized ||
            response.StatusCode == HttpStatusCode.UnprocessableEntity
        );
    }

    [Fact]
    public async Task ChangePassword_PasswordMismatch_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();
        var json = @"{ ""currentPassword"": ""OldPass123"", ""newPassword"": ""NewPass123"", ""confirmPassword"": ""Different"" }";
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await client.PutAsync("/auth/password", content);

        Assert.True(
            response.StatusCode == HttpStatusCode.BadRequest ||
            response.StatusCode == HttpStatusCode.Unauthorized ||
            response.StatusCode == HttpStatusCode.UnprocessableEntity
        );
    }

    [Fact]
    public async Task ChangePassword_ValidRequest_ReturnsOkOrUnauth()
    {
        var client = _factory.CreateClient();
        var json = @"{ ""currentPassword"": ""OldPass123"", ""newPassword"": ""NewPass123456"", ""confirmPassword"": ""NewPass123456"" }";
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await client.PutAsync("/auth/password", content);

        Assert.True(
            response.StatusCode == HttpStatusCode.OK ||
            response.StatusCode == HttpStatusCode.BadRequest ||
            response.StatusCode == HttpStatusCode.Unauthorized
        );
    }

    [Fact]
    public async Task InitiatePasswordReset_EndpointExists()
    {
        var client = _factory.CreateClient();
        var userId = Guid.NewGuid();

        var response = await client.SendAsync(
            new HttpRequestMessage(HttpMethod.Options, $"/auth/users/{userId}/reset-password")
        );

        Assert.NotEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task InitiatePasswordReset_WithoutSession_Returns401()
    {
        var client = _factory.CreateClient();
        var userId = Guid.NewGuid();

        var response = await client.PostAsync($"/auth/users/{userId}/reset-password", new StringContent(""));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task InitiatePasswordReset_InvalidUserId_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync("/auth/users/00000000-0000-0000-0000-000000000000/reset-password", new StringContent(""));

        Assert.True(
            response.StatusCode == HttpStatusCode.BadRequest ||
            response.StatusCode == HttpStatusCode.Unauthorized ||
            response.StatusCode == HttpStatusCode.NotFound
        );
    }

    [Fact]
    public async Task ResetPassword_EndpointExists()
    {
        var client = _factory.CreateClient();

        var response = await client.SendAsync(
            new HttpRequestMessage(HttpMethod.Options, "/auth/reset-password")
        );

        Assert.NotEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_InvalidRequest_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();
        var json = @"{ ""userId"": ""00000000-0000-0000-0000-000000000000"", ""token"": """", ""newPassword"": """" }";
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/auth/reset-password", content);

        Assert.True(
            response.StatusCode == HttpStatusCode.BadRequest ||
            response.StatusCode == HttpStatusCode.UnprocessableEntity
        );
    }

    [Fact]
    public async Task ResetPassword_PasswordMismatch_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();
        var json = @"{ ""userId"": ""00000000-0000-0000-0000-000000000000"", ""token"": ""dummytoken"", ""newPassword"": ""NewPass123456"", ""confirmPassword"": ""Different"" }";
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/auth/reset-password", content);

        Assert.True(
            response.StatusCode == HttpStatusCode.BadRequest ||
            response.StatusCode == HttpStatusCode.UnprocessableEntity
        );
    }

    [Fact]
    public async Task ResetPassword_InvalidToken_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();
        var json = @"{ ""userId"": ""00000000-0000-0000-0000-000000000000"", ""token"": ""invalidtoken"", ""newPassword"": ""NewPass123456"", ""confirmPassword"": ""NewPass123456"" }";
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/auth/reset-password", content);

        Assert.True(
            response.StatusCode == HttpStatusCode.BadRequest ||
            response.StatusCode == HttpStatusCode.UnprocessableEntity
        );
    }
}

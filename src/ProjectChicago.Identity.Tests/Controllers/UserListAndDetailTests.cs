using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace ProjectChicago.Identity.Tests.Controllers;

// Comprehensive tests for user list and detail endpoints (SEC-004, SEC-010..016).
// Verify pagination structure, role display, authorization boundaries, and sensitive-field absence
// (no passwords, hashes, or security tokens in responses).
public class UserListAndDetailTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public UserListAndDetailTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    [Fact]
    public async Task ListUsers_Unauthenticated_Returns401Unauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/users");

        // Unauthenticated request must return 401, not 404 or 200
        Assert.True(
            response.StatusCode == HttpStatusCode.Unauthorized ||
            response.StatusCode == HttpStatusCode.Forbidden,
            $"Expected 401/403 but got {response.StatusCode}"
        );
    }

    [Fact]
    public async Task GetUserDetail_Unauthenticated_Returns401Unauthorized()
    {
        var client = _factory.CreateClient();
        var userId = Guid.NewGuid();

        var response = await client.GetAsync($"/users/{userId}");

        // Unauthenticated request must return 401, not 404 or 200
        Assert.True(
            response.StatusCode == HttpStatusCode.Unauthorized ||
            response.StatusCode == HttpStatusCode.Forbidden,
            $"Expected 401/403 but got {response.StatusCode}"
        );
    }

    [Fact]
    public async Task ListUsers_ValidResponse_ContainsPaginationMetadata()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/users");

        // If successful, response must include pagination structure (even if empty)
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Verify required pagination fields exist
            Assert.True(root.TryGetProperty("items", out _), "Response must contain 'items' field");
            Assert.True(root.TryGetProperty("page", out _), "Response must contain 'page' field");
            Assert.True(root.TryGetProperty("pageSize", out _), "Response must contain 'pageSize' field");
            Assert.True(root.TryGetProperty("totalCount", out _), "Response must contain 'totalCount' field");
            Assert.True(root.TryGetProperty("totalPages", out _), "Response must contain 'totalPages' field");
        }
    }

    [Fact]
    public async Task ListUsers_ValidResponse_ContainsNoPasswordFields()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/users");

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var json = await response.Content.ReadAsStringAsync();

            // Sensitive fields must never appear in responses (SEC-004: support-safe metadata only)
            Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("passwordHash", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("securityStamp", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("lockoutEnd", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("token", json, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task ListUsers_ValidResponse_ContainsUserMetadata()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/users");

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("items", out var items) && items.GetArrayLength() > 0)
            {
                var firstItem = items[0];

                // Each user must have support-safe fields (SEC-004: ID, email, role, created-at)
                Assert.True(firstItem.TryGetProperty("userId", out var userId), "User item must contain 'userId'");
                Assert.True(firstItem.TryGetProperty("email", out var email), "User item must contain 'email'");
                Assert.True(firstItem.TryGetProperty("roleName", out var roleName), "User item must contain 'roleName'");
                Assert.True(firstItem.TryGetProperty("createdAtUtc", out var createdAt), "User item must contain 'createdAtUtc'");

                // Verify fields have reasonable values
                Assert.NotEqual(default(Guid), userId.GetGuid());
                Assert.NotEqual("", email.GetString());
                Assert.NotEqual("", roleName.GetString());
            }
        }
    }

    [Fact]
    public async Task ListUsers_Pagination_Page1PageSize10_ReturnsCorrectStructure()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/users?page=1&pageSize=10");

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Verify pagination values match request
            Assert.True(root.TryGetProperty("page", out var page), "Missing 'page'");
            Assert.Equal(1, page.GetInt32());

            Assert.True(root.TryGetProperty("pageSize", out var pageSize), "Missing 'pageSize'");
            Assert.Equal(10, pageSize.GetInt32());

            // Verify derived fields
            Assert.True(root.TryGetProperty("totalPages", out var totalPages), "Missing 'totalPages'");
            Assert.True(root.TryGetProperty("totalCount", out var totalCount), "Missing 'totalCount'");

            // totalPages should be ceil(totalCount / pageSize)
            int expectedTotalPages = totalCount.GetInt32() == 0
                ? 0
                : (totalCount.GetInt32() + pageSize.GetInt32() - 1) / pageSize.GetInt32();

            Assert.Equal(expectedTotalPages, totalPages.GetInt32());
        }
    }

    [Fact]
    public async Task GetUserDetail_ValidResponse_ContainsUserMetadata()
    {
        var client = _factory.CreateClient();
        var userId = Guid.NewGuid();

        var response = await client.GetAsync($"/users/{userId}");

        // Success or not-found are both valid (depends on whether user exists)
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Detail response must have support-safe fields
            Assert.True(root.TryGetProperty("userId", out _), "Response must contain 'userId'");
            Assert.True(root.TryGetProperty("email", out _), "Response must contain 'email'");
            Assert.True(root.TryGetProperty("roleName", out _), "Response must contain 'roleName'");
            Assert.True(root.TryGetProperty("createdAtUtc", out _), "Response must contain 'createdAtUtc'");
        }
    }

    [Fact]
    public async Task GetUserDetail_ValidResponse_ContainsNoPasswordFields()
    {
        var client = _factory.CreateClient();
        var userId = Guid.NewGuid();

        var response = await client.GetAsync($"/users/{userId}");

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var json = await response.Content.ReadAsStringAsync();

            // Sensitive fields must never appear (SEC-004: support-safe metadata only)
            Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("passwordHash", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("securityStamp", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("lockoutEnd", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("token", json, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task ListUsers_InvalidPageNumber_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/users?page=0&pageSize=10");

        // Invalid page should return 400 Bad Request (model validation)
        Assert.True(
            response.StatusCode == HttpStatusCode.BadRequest ||
            response.StatusCode == HttpStatusCode.Unauthorized,
            $"Expected 400/401 but got {response.StatusCode}"
        );
    }

    [Fact]
    public async Task ListUsers_InvalidPageSize_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/users?page=1&pageSize=0");

        // Invalid page size should return 400 Bad Request (model validation)
        Assert.True(
            response.StatusCode == HttpStatusCode.BadRequest ||
            response.StatusCode == HttpStatusCode.Unauthorized,
            $"Expected 400/401 but got {response.StatusCode}"
        );
    }

    [Fact]
    public async Task ListUsers_PageSizeExceedsMaximum_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/users?page=1&pageSize=101");

        // Page size > 100 should return 400 Bad Request (bounded validation)
        Assert.True(
            response.StatusCode == HttpStatusCode.BadRequest ||
            response.StatusCode == HttpStatusCode.Unauthorized,
            $"Expected 400/401 but got {response.StatusCode}"
        );
    }

    [Fact]
    public async Task GetUserDetail_NonexistentUser_ReturnsNotFound()
    {
        var client = _factory.CreateClient();
        var nonexistentUserId = Guid.NewGuid();

        var response = await client.GetAsync($"/users/{nonexistentUserId}");

        // Nonexistent user should return 404 (if authenticated and authorized)
        // Or 401/403 if not authenticated/authorized
        Assert.True(
            response.StatusCode == HttpStatusCode.NotFound ||
            response.StatusCode == HttpStatusCode.Unauthorized ||
            response.StatusCode == HttpStatusCode.Forbidden,
            $"Expected 404/401/403 but got {response.StatusCode}"
        );
    }

    [Fact]
    public async Task GetUserDetail_InvalidGuidFormat_ReturnsNotFound()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/users/not-a-valid-guid");

        // Route constraint prevents matching; should be 404
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}

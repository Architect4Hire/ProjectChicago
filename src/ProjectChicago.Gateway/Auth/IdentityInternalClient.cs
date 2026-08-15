using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProjectChicago.Gateway.Auth;

/// <summary>
/// Local copy of Identity's login/refresh response contract (no shared assembly reference, per bounded-context isolation).
/// This mirrors Identity.Core.Authorization.Contracts.LoginServiceModel but is NOT a cross-service import.
/// </summary>
public record IdentityLoginResponse
{
    [JsonPropertyName("user")]
    public required IdentityUser User { get; init; }

    [JsonPropertyName("accessToken")]
    public required string AccessToken { get; init; }

    [JsonPropertyName("accessTokenExpiresAtUtc")]
    public required DateTime AccessTokenExpiresAtUtc { get; init; }

    [JsonPropertyName("refreshToken")]
    public required string RefreshToken { get; init; }

    [JsonPropertyName("refreshTokenExpiresAtUtc")]
    public required DateTime RefreshTokenExpiresAtUtc { get; init; }
}

/// <summary>Local copy of Identity's user info contract.</summary>
public record IdentityUser
{
    [JsonPropertyName("userId")]
    public required Guid UserId { get; init; }

    [JsonPropertyName("email")]
    public required string Email { get; init; }

    [JsonPropertyName("userName")]
    public required string UserName { get; init; }

    [JsonPropertyName("roles")]
    public required List<string> Roles { get; init; }
}

/// <summary>
/// Typed HTTP client for calling Identity's authentication endpoints (ADR-0018-superseding BFF).
/// Uses a named HttpClient with service discovery and resilience from ServiceDefaults.
/// </summary>
public class IdentityInternalClient
{
    private readonly HttpClient _httpClient;

    public IdentityInternalClient(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        _httpClient = httpClient;
    }

    /// <summary>Call Identity's /auth/login endpoint to authenticate a user.</summary>
    public async Task<IdentityLoginResponse> LoginAsync(
        string email,
        string pwd,
        CancellationToken cancellationToken = default)
    {
        var request = new { Email = email, Password = pwd };
        var content = JsonContent.Create(request);

        var response = await _httpClient.PostAsync("/auth/login", content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Identity /auth/login failed: {response.StatusCode}",
                null,
                response.StatusCode);
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var result = JsonSerializer.Deserialize<IdentityLoginResponse>(json);
        if (result is null)
            throw new InvalidOperationException("Identity /auth/login returned null response");

        return result;
    }

    /// <summary>Call Identity's /auth/refresh endpoint to refresh an expired access token.</summary>
    public async Task<IdentityLoginResponse> RefreshAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        var request = new { RefreshToken = refreshToken };
        var content = JsonContent.Create(request);

        var response = await _httpClient.PostAsync("/auth/refresh", content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Identity /auth/refresh failed: {response.StatusCode}",
                null,
                response.StatusCode);
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var result = JsonSerializer.Deserialize<IdentityLoginResponse>(json);
        if (result is null)
            throw new InvalidOperationException("Identity /auth/refresh returned null response");

        return result;
    }
}

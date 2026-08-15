using System.Text.Json.Serialization;

namespace ProjectChicago.Gateway.Sessions;

/// <summary>
/// Server-side session record held in Redis (ADR-0018-superseding BFF design).
/// Gateway stores this opaque object keyed by sessionId; the browser never sees the tokens directly.
/// </summary>
public record GatewaySession
{
    /// <summary>User ID from the Identity service.</summary>
    [JsonPropertyName("userId")]
    public required Guid UserId { get; init; }

    /// <summary>User's email address.</summary>
    [JsonPropertyName("email")]
    public required string Email { get; init; }

    /// <summary>User's username.</summary>
    [JsonPropertyName("userName")]
    public required string UserName { get; init; }

    /// <summary>List of roles assigned to this user.</summary>
    [JsonPropertyName("roles")]
    public required List<string> Roles { get; init; }

    /// <summary>JWT access token (short-lived, ~10 minutes).</summary>
    [JsonPropertyName("accessToken")]
    public required string AccessToken { get; init; }

    /// <summary>UTC timestamp when the access token expires.</summary>
    [JsonPropertyName("accessTokenExpiresAtUtc")]
    public required DateTime AccessTokenExpiresAtUtc { get; init; }

    /// <summary>JWT refresh token (long-lived, ~14 days); used only by Gateway to refresh the access token.</summary>
    [JsonPropertyName("refreshToken")]
    public required string RefreshToken { get; init; }

    /// <summary>UTC timestamp when the refresh token expires; session TTL in Redis is set to this.</summary>
    [JsonPropertyName("refreshTokenExpiresAtUtc")]
    public required DateTime RefreshTokenExpiresAtUtc { get; init; }
}

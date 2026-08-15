namespace ProjectChicago.Identity.Core.Authorization.Contracts;

/// <summary>
/// Refresh token request contract for the /auth/refresh endpoint (ADR-0018-superseding BFF design).
/// Called by the Gateway when an access token is expired or near-expiry to rotate the token pair.
/// </summary>
public class RefreshTokenViewModel
{
    public required string RefreshToken { get; set; }
}

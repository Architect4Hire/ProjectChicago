namespace ProjectChicago.Identity.Core.Authorization.Contracts;

/// <summary>
/// Login response contract (ADR-0018-superseding BFF design).
/// This is an internal Identity ↔ Gateway contract only — never forwarded to the browser.
/// The Gateway intercepts login, receives this shape, stores the tokens server-side in Redis,
/// and returns only { user } to the React client (along with a CSRF token via response header).
/// </summary>
public class LoginServiceModel
{
    public required UserServiceModel User { get; set; }

    public required string AccessToken { get; set; }

    public required DateTime AccessTokenExpiresAtUtc { get; set; }

    public required string RefreshToken { get; set; }

    public required DateTime RefreshTokenExpiresAtUtc { get; set; }
}

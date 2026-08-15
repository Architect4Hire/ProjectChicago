using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using ProjectChicago.Identity.Core.Authorization.Contracts;
using ProjectChicago.Identity.Core.Models.DataModels.Entities;

namespace ProjectChicago.Identity.Core.Authorization.Business;

/// <summary>
/// Authentication business logic (SEC-001, SEC-020..025, ADR-0018-superseding).
/// Credential validation via SignInManager, lockout enforcement, and JWT token minting.
/// Never logs password material or sensitive tokens. SignInManager handles password hashing verification,
/// security stamp validation, and lockout mechanics (SEC-020..024). Passwords are never materialized
/// beyond the wire format for hashing comparison. Returns structured LoginResult (not throwing exceptions)
/// so Facade can record appropriate audit events.
/// </summary>
public class AuthenticationBusiness
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IAuthenticationSchemeProvider _authenticationSchemeProvider;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly JwtTokenService _jwtTokenService;

    public AuthenticationBusiness(
        SignInManager<ApplicationUser> signInManager,
        IAuthenticationSchemeProvider authenticationSchemeProvider,
        UserManager<ApplicationUser> userManager,
        JwtTokenService jwtTokenService)
    {
        ArgumentNullException.ThrowIfNull(signInManager);
        ArgumentNullException.ThrowIfNull(authenticationSchemeProvider);
        ArgumentNullException.ThrowIfNull(userManager);
        ArgumentNullException.ThrowIfNull(jwtTokenService);
        _signInManager = signInManager;
        _authenticationSchemeProvider = authenticationSchemeProvider;
        _userManager = userManager;
        _jwtTokenService = jwtTokenService;
    }

    /// <summary>
    /// Attempt login via credential validation and return structured result for audit event determination
    /// (SEC-001..025, AUDIT-001, ADR-0018-superseding). No longer issues a cookie; mints JWT tokens instead.
    /// </summary>
    public async Task<LoginResult> LoginAsync(
        LoginViewModel request,
        DateTime _,  // expiresAtUtc parameter no longer used (token lifetimes come from JwtTokenService/config)
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        ApplicationUser? user = null;

        // Attempt to find user to determine if we can emit audit event with valid user ID (vs Anonymous for unknown user).
        // This lookup does not leak user-existence information to the client (we still return generic "invalid credentials" error).
        try
        {
            user = await _userManager.FindByNameAsync(normalizedEmail).ConfigureAwait(false);
        }
        catch
        {
            // User lookup failure; proceed with credential attempt and audit as Anonymous.
        }

        // If user not found, short-circuit to invalid credentials (don't call CheckPasswordSignInAsync on null).
        if (user is null)
        {
            return new LoginResult
            {
                Outcome = LoginOutcome.FailedInvalidCredentials,
                AttemptedUsername = normalizedEmail,
                ErrorMessage = "Invalid email or password.",
            };
        }

        // SignInManager.CheckPasswordSignInAsync handles:
        // 1. Password hash verification (never exposes plaintext password)
        // 2. Lockout check and enforcement
        // 3. Security stamp validation
        // Note: Does NOT issue an authentication cookie (unlike PasswordSignInAsync).
        var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true)
            .ConfigureAwait(false);

        if (result.IsLockedOut)
        {
            // Account locked after N failed attempts (SEC-020: lockout enforcement).
            return new LoginResult
            {
                Outcome = LoginOutcome.FailedAccountLocked,
                User = user,
                AttemptedUsername = normalizedEmail,
                ErrorMessage = "Account is locked due to too many failed login attempts.",
            };
        }

        if (result.RequiresTwoFactor)
        {
            // 2FA not yet implemented (ADR-0018-superseding: future evolution).
            return new LoginResult
            {
                Outcome = LoginOutcome.FailedTwoFactorRequired,
                User = user,
                AttemptedUsername = normalizedEmail,
                ErrorMessage = "Two-factor authentication is not yet enabled.",
            };
        }

        if (!result.Succeeded)
        {
            // Invalid credentials - do NOT expose whether user exists or password is wrong (SEC-024).
            // Audit recorded as Anonymous (not tied to a specific user ID).
            return new LoginResult
            {
                Outcome = LoginOutcome.FailedInvalidCredentials,
                AttemptedUsername = normalizedEmail,
                ErrorMessage = "Invalid email or password.",
            };
        }

        // Successful login. Get user roles and mint JWT tokens.
        var roles = await _userManager.GetRolesAsync(user).ConfigureAwait(false);
        var (accessToken, accessTokenExpiresAtUtc) = _jwtTokenService.MintAccessToken(user, roles.ToList());
        var (refreshToken, refreshTokenExpiresAtUtc) = _jwtTokenService.MintRefreshToken(user);

        var userServiceModel = new UserServiceModel
        {
            UserId = user.Id,
            Email = user.Email ?? "",
            UserName = user.UserName ?? "",
            Roles = roles.ToList(),
            CreatedAtUtc = DateTime.UtcNow,
        };

        return new LoginResult
        {
            Outcome = LoginOutcome.Success,
            User = user,
            ServiceModel = new LoginServiceModel
            {
                User = userServiceModel,
                AccessToken = accessToken,
                AccessTokenExpiresAtUtc = accessTokenExpiresAtUtc,
                RefreshToken = refreshToken,
                RefreshTokenExpiresAtUtc = refreshTokenExpiresAtUtc,
            },
        };
    }

    /// <summary>
    /// Refresh the access token using a valid refresh token (called by /auth/refresh endpoint).
    /// Validates the refresh JWT, reloads the user, re-checks lockout state, and mints a rotated
    /// access+refresh pair (refresh token rotation for security).
    /// </summary>
    public async Task<LoginResult> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNullOrWhiteSpace(refreshToken);

        // Validate refresh token and extract user ID (throws InvalidOperationException on failure).
        Guid userId;
        try
        {
            userId = _jwtTokenService.ValidateRefreshToken(refreshToken);
        }
        catch (InvalidOperationException ex)
        {
            return new LoginResult
            {
                Outcome = LoginOutcome.FailedInvalidCredentials,
                ErrorMessage = ex.Message,
            };
        }

        // Reload user and check if they still exist and are not locked out.
        var user = await _userManager.FindByIdAsync(userId.ToString()).ConfigureAwait(false);
        if (user is null)
        {
            return new LoginResult
            {
                Outcome = LoginOutcome.FailedInvalidCredentials,
                ErrorMessage = "User not found.",
            };
        }

        var isLockedOut = await _userManager.IsLockedOutAsync(user).ConfigureAwait(false);
        if (isLockedOut)
        {
            return new LoginResult
            {
                Outcome = LoginOutcome.FailedAccountLocked,
                User = user,
                ErrorMessage = "Account is locked due to too many failed login attempts.",
            };
        }

        // Mint rotated token pair.
        var roles = await _userManager.GetRolesAsync(user).ConfigureAwait(false);
        var (accessToken, accessTokenExpiresAtUtc) = _jwtTokenService.MintAccessToken(user, roles.ToList());
        var (newRefreshToken, newRefreshTokenExpiresAtUtc) = _jwtTokenService.MintRefreshToken(user);

        var userServiceModel = new UserServiceModel
        {
            UserId = user.Id,
            Email = user.Email ?? "",
            UserName = user.UserName ?? "",
            Roles = roles.ToList(),
            CreatedAtUtc = DateTime.UtcNow,
        };

        return new LoginResult
        {
            Outcome = LoginOutcome.Success,
            User = user,
            ServiceModel = new LoginServiceModel
            {
                User = userServiceModel,
                AccessToken = accessToken,
                AccessTokenExpiresAtUtc = accessTokenExpiresAtUtc,
                RefreshToken = newRefreshToken,
                RefreshTokenExpiresAtUtc = newRefreshTokenExpiresAtUtc,
            },
        };
    }
}

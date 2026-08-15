using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using ProjectChicago.Identity.Core.Authorization.Contracts;
using ProjectChicago.Identity.Core.Models.DataModels.Entities;

namespace ProjectChicago.Identity.Core.Authorization.Business;

// Authentication business logic (SEC-001, SEC-020..025, ADR-0018): Credential validation via SignInManager,
// lockout enforcement, and CSRF token generation. Never logs password material or sensitive tokens.
// SignInManager handles password hashing verification, security stamp validation, and lockout mechanics
// (SEC-020..024). Passwords are never materialized beyond the wire format for hashing comparison.
// Returns structured LoginResult (not throwing exceptions) so Facade can record appropriate audit events.
public class AuthenticationBusiness
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IAuthenticationSchemeProvider _authenticationSchemeProvider;
    private readonly UserManager<ApplicationUser> _userManager;

    public AuthenticationBusiness(
        SignInManager<ApplicationUser> signInManager,
        IAuthenticationSchemeProvider authenticationSchemeProvider,
        UserManager<ApplicationUser> userManager)
    {
        ArgumentNullException.ThrowIfNull(signInManager);
        ArgumentNullException.ThrowIfNull(authenticationSchemeProvider);
        ArgumentNullException.ThrowIfNull(userManager);
        _signInManager = signInManager;
        _authenticationSchemeProvider = authenticationSchemeProvider;
        _userManager = userManager;
    }

    // Attempt login and return structured result for audit event determination (SEC-001..025, AUDIT-001).
    public async Task<LoginResult> LoginAsync(
        LoginViewModel request,
        DateTime expiresAtUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var normalizedUserName = request.UserName.Trim().ToLowerInvariant();
        ApplicationUser? user = null;

        // Attempt to find user to determine if we can emit audit event with valid user ID (vs Anonymous for unknown user).
        // This lookup does not leak user-existence information to the client (we still return generic "invalid credentials" error).
        try
        {
            user = await _userManager.FindByNameAsync(normalizedUserName).ConfigureAwait(false);
        }
        catch
        {
            // User lookup failure; proceed with credential attempt and audit as Anonymous.
        }

        // SignInManager.PasswordSignInAsync handles:
        // 1. User lookup by normalized username
        // 2. Password hash verification (never exposes plaintext password)
        // 3. Lockout check and enforcement
        // 4. Security stamp validation
        // 5. Issue authentication cookie (if successful)
        var result = await _signInManager.PasswordSignInAsync(
            normalizedUserName,
            request.Password,
            isPersistent: false,
            lockoutOnFailure: true);

        if (result.IsLockedOut)
        {
            // Account locked after N failed attempts (SEC-020: lockout enforcement).
            // Audit will record with the user if found, else Anonymous.
            return new LoginResult
            {
                Outcome = LoginOutcome.FailedAccountLocked,
                User = user,
                AttemptedUsername = normalizedUserName,
                ErrorMessage = "Account is locked due to too many failed login attempts.",
            };
        }

        if (result.RequiresTwoFactor)
        {
            // 2FA not yet implemented (ADR-0018: future evolution).
            return new LoginResult
            {
                Outcome = LoginOutcome.FailedTwoFactorRequired,
                User = user,
                AttemptedUsername = normalizedUserName,
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
                AttemptedUsername = normalizedUserName,
                ErrorMessage = "Invalid username or password.",
            };
        }

        // Successful login. User must be non-null here (SignInManager succeeded).
        user ??= await _userManager.FindByNameAsync(normalizedUserName).ConfigureAwait(false);

        // Generate CSRF token (ADR-0018: client includes this in all mutation requests).
        var token = Guid.NewGuid().ToString("N");

        return new LoginResult
        {
            Outcome = LoginOutcome.Success,
            User = user,
            ServiceModel = new LoginServiceModel
            {
                Token = token,
                ExpiresAt = expiresAtUtc,
            },
        };
    }
}

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ProjectChicago.Identity.Core.Authorization.Contracts;
using ProjectChicago.Identity.Core.Authorization.Facade;
using ProjectChicago.Identity.Core.Models.DataModels.Entities;

namespace ProjectChicago.Identity.Controllers;

/// <summary>
/// Authentication endpoints for login, logout, and password change (ADR-0018: cookie authentication + CSRF, SEC-004..005/AUDIT-001).
/// </summary>
[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    private readonly AuthenticationFacade _authenticationFacade;
    private readonly UserManagementFacade _userManagementFacade;
    private readonly UserManager<ApplicationUser> _userManager;

    public AuthController(
        AuthenticationFacade authenticationFacade,
        UserManagementFacade userManagementFacade,
        UserManager<ApplicationUser> userManager)
    {
        ArgumentNullException.ThrowIfNull(authenticationFacade);
        ArgumentNullException.ThrowIfNull(userManagementFacade);
        ArgumentNullException.ThrowIfNull(userManager);
        _authenticationFacade = authenticationFacade;
        _userManagementFacade = userManagementFacade;
        _userManager = userManager;
    }

    /// <summary>
    /// Login with username and password. Issues HTTPOnly session cookie and returns CSRF token.
    /// Records audit event on success, failure, or lockout (SEC-001, SEC-020..025, AUDIT-001..008, ADR-0018).
    /// </summary>
    /// <param name="request">Username and password</param>
    /// <response code="200">Login successful; session cookie issued (httponly, secure, samesite=strict); CSRF token returned</response>
    /// <response code="401">Invalid credentials</response>
    /// <response code="429">Account locked after too many failed attempts</response>
    [HttpPost("login", Name = "Login")]
    [ProducesResponseType(typeof(LoginServiceModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<LoginServiceModel>> LoginAsync(
        [FromBody] LoginViewModel request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _authenticationFacade.LoginAsync(request, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("locked", StringComparison.OrdinalIgnoreCase))
        {
            return StatusCode(StatusCodes.Status429TooManyRequests, new ProblemDetails
            {
                Title = "Account Locked",
                Detail = ex.Message,
                Status = StatusCodes.Status429TooManyRequests,
            });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("username or password", StringComparison.OrdinalIgnoreCase))
        {
            return Unauthorized(new ProblemDetails
            {
                Title = "Authentication Failed",
                Detail = "Invalid username or password.",
                Status = StatusCodes.Status401Unauthorized,
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid Request",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest,
            });
        }
    }

    /// <summary>
    /// Logout (sign out). Records audit event, clears session state and removes authentication cookie.
    /// (SEC-005, AUDIT-001..008, ADR-0018).
    /// </summary>
    /// <response code="200">Logout successful; session cleared</response>
    [HttpPost("logout", Name = "Logout")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> LogoutAsync(CancellationToken cancellationToken = default)
    {
        // Record logout audit event if user is authenticated (SEC-005, AUDIT-001).
        if (User.Identity?.IsAuthenticated == true)
        {
            var userId = User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;
            if (Guid.TryParse(userId, out var userGuid))
            {
                var user = await _userManager.FindByIdAsync(userGuid.ToString()).ConfigureAwait(false);
                if (user is not null)
                {
                    await _authenticationFacade.RecordLogoutAsync(user, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        await HttpContext.SignOutAsync("Cookies");
        return Ok(new { message = "Logged out successfully" });
    }

    /// <summary>
    /// Get current authenticated user info.
    /// (SEC-010, SEC-020..025: authenticated user context).
    /// </summary>
    /// <response code="200">Authenticated; current user info returned</response>
    /// <response code="401">Not authenticated or session expired</response>
    [HttpGet("current-user", Name = "GetCurrentUser")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public IActionResult GetCurrentUser()
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return Unauthorized(new ProblemDetails
            {
                Title = "Not Authenticated",
                Detail = "No valid session. Please log in.",
                Status = StatusCodes.Status401Unauthorized,
            });
        }

        var userId = User.FindFirst("sub")?.Value ?? User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;
        var userName = User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name")?.Value;
        var email = User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress")?.Value;

        return Ok(new
        {
            userId,
            userName,
            email,
            isAuthenticated = User.Identity.IsAuthenticated,
        });
    }

    /// <summary>
    /// Change password for authenticated user.
    /// Validates current password, updates to new password, invalidates existing sessions.
    /// Records audit event (SEC-004, SEC-005, AUDIT-001..008).
    /// </summary>
    /// <param name="request">Change password request (current password, new password, confirmation)</param>
    /// <response code="200">Password changed successfully; session invalidated, user must re-authenticate</response>
    /// <response code="400">Invalid request (validation error, current password incorrect, policy rejection)</response>
    /// <response code="401">Not authenticated</response>
    [HttpPut("password", Name = "ChangePassword")]
    [Authorize]
    [ProducesResponseType(typeof(UserServiceModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserServiceModel>> ChangePasswordAsync(
        [FromBody] ChangePasswordViewModel request,
        CancellationToken cancellationToken = default)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return Unauthorized(new ProblemDetails
            {
                Title = "Not Authenticated",
                Detail = "No valid session. Please log in.",
                Status = StatusCodes.Status401Unauthorized,
            });
        }

        var userId = User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;
        if (!Guid.TryParse(userId, out var userGuid))
        {
            return Unauthorized(new ProblemDetails
            {
                Title = "Invalid User Context",
                Detail = "Cannot determine current user identity.",
                Status = StatusCodes.Status401Unauthorized,
            });
        }

        try
        {
            var result = await _userManagementFacade.ChangePasswordAsync(userGuid, request, cancellationToken).ConfigureAwait(false);
            return Ok(result);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("incorrect", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Password Change Failed",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest,
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Password Change Failed",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest,
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid Request",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest,
            });
        }
    }

    /// <summary>
    /// Initiate password reset for a user (admin-only).
    /// Generates a one-time reset token; admin communicates token to user via out-of-band means.
    /// Records audit event without exposing token (SEC-004, SEC-005, AUDIT-001..008).
    /// </summary>
    /// <param name="request">Reset initiation request (user ID)</param>
    /// <response code="200">Reset token generated successfully</response>
    /// <response code="400">Invalid request (user not found)</response>
    /// <response code="401">Not authenticated</response>
    /// <response code="403">Not authorized (not admin)</response>
    [HttpPost("users/{userId}/reset-password", Name = "InitiatePasswordReset")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> InitiatePasswordResetAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return Unauthorized(new ProblemDetails
            {
                Title = "Not Authenticated",
                Detail = "No valid session. Please log in.",
                Status = StatusCodes.Status401Unauthorized,
            });
        }

        try
        {
            var token = await _userManagementFacade.InitiatePasswordResetAsync(userId, cancellationToken).ConfigureAwait(false);
            return Ok(new { token });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Reset Initiation Failed",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest,
            });
        }
    }

    /// <summary>
    /// Complete password reset for a user (unauthenticated).
    /// Validates reset token and sets new password; invalidates existing sessions.
    /// Records audit event without exposing token or password (SEC-004, SEC-005, AUDIT-001..008).
    /// </summary>
    /// <param name="request">Reset completion request (user ID, token, new password, confirmation)</param>
    /// <response code="200">Password reset successfully; user must re-authenticate</response>
    /// <response code="400">Invalid request (token invalid/expired, policy rejection, validation error)</response>
    [HttpPost("reset-password", Name = "ResetPassword")]
    [ProducesResponseType(typeof(UserServiceModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UserServiceModel>> ResetPasswordAsync(
        [FromBody] ResetPasswordViewModel request,
        CancellationToken cancellationToken = default)
    {
        if (request?.UserId == Guid.Empty)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid Request",
                Detail = "User ID is required.",
                Status = StatusCodes.Status400BadRequest,
            });
        }

        try
        {
            var result = await _userManagementFacade.ResetPasswordAsync(request!.UserId, request, cancellationToken).ConfigureAwait(false);
            return Ok(result);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("failed", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Password Reset Failed",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest,
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid Request",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest,
            });
        }
    }
}

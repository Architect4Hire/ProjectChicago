using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectChicago.Identity.Core.Authorization.Contracts;
using ProjectChicago.Identity.Core.Authorization.Facade;

namespace ProjectChicago.Identity.Controllers;

/// <summary>
/// User management endpoints for administrator use (SEC-004, SEC-010..016, AUDIT-001..008).
/// Administrator-only operations for creating users and assigning roles.
/// </summary>
[ApiController]
[Route("users")]
[Authorize(Roles = "Administrator")]
public class UserController : ControllerBase
{
    private readonly UserManagementFacade _userManagementFacade;

    public UserController(UserManagementFacade userManagementFacade)
    {
        ArgumentNullException.ThrowIfNull(userManagementFacade);
        _userManagementFacade = userManagementFacade;
    }

    /// <summary>
    /// List application users with pagination.
    /// Administrator-only read endpoint. Returns support-safe user metadata (ID, email, role, created-at)
    /// without passwords or security tokens (SEC-004, SEC-010..016).
    /// </summary>
    /// <param name="request">Pagination request (page, pageSize)</param>
    /// <response code="200">Users retrieved successfully with pagination metadata</response>
    /// <response code="400">Invalid request (page/pageSize out of valid range)</response>
    /// <response code="401">Not authenticated</response>
    /// <response code="403">Authenticated but not authorized (not Administrator)</response>
    [HttpGet(Name = "ListUsers")]
    [ProducesResponseType(typeof(PagedResponse<UserServiceModel>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResponse<UserServiceModel>>> ListAsync(
        [FromQuery] ListUsersRequest request,
        CancellationToken cancellationToken = default)
    {
        if (User.Identity is not { IsAuthenticated: true })
        {
            return Unauthorized();
        }

        var response = await _userManagementFacade.ListUsersAsync(request, cancellationToken).ConfigureAwait(false);

        return Ok(response);
    }

    /// <summary>
    /// Get a user by ID with role information.
    /// Administrator-only read endpoint. Returns support-safe user metadata (ID, email, role, created-at)
    /// without passwords or security tokens (SEC-004, SEC-010..016).
    /// </summary>
    /// <param name="id">User ID</param>
    /// <response code="200">User retrieved successfully</response>
    /// <response code="401">Not authenticated</response>
    /// <response code="403">Authenticated but not authorized (not Administrator)</response>
    /// <response code="404">User not found</response>
    [HttpGet("{id:guid}", Name = "GetUserDetail")]
    [ProducesResponseType(typeof(UserServiceModel), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserServiceModel>> GetAsync(
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default)
    {
        if (User.Identity is not { IsAuthenticated: true })
        {
            return Unauthorized();
        }

        var response = await _userManagementFacade.GetUserDetailAsync(id, cancellationToken).ConfigureAwait(false);

        if (response is null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "User Not Found",
                Detail = $"User with ID '{id}' does not exist.",
                Status = StatusCodes.Status404NotFound,
            });
        }

        return Ok(response);
    }

    /// <summary>
    /// Create a new application user with assigned role.
    /// Administrator-only endpoint. Records audit event on success, role validation failure,
    /// or duplicate user detection (SEC-004, SEC-010..016, AUDIT-001..008).
    /// </summary>
    /// <param name="request">User creation request (email, password, role)</param>
    /// <response code="201">User created successfully; returns user ID, email, and assigned role</response>
    /// <response code="400">Invalid request (validation error, password policy violation, role not found)</response>
    /// <response code="401">Not authenticated</response>
    /// <response code="403">Authenticated but not authorized (not Administrator)</response>
    /// <response code="409">Duplicate user (email already exists)</response>
    [HttpPost]
    [ProducesResponseType(typeof(UserServiceModel), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserServiceModel>> CreateUserAsync(
        [FromBody] CreateUserViewModel request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _userManagementFacade.CreateUserAsync(request, cancellationToken);
            return CreatedAtAction(nameof(CreateUserAsync), result);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase))
        {
            return Conflict(new ProblemDetails
            {
                Title = "User Already Exists",
                Detail = ex.Message,
                Status = StatusCodes.Status409Conflict,
            });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("does not exist", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid Role",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest,
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "User Creation Failed",
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
    /// Deactivate a user account.
    /// Administrator-only endpoint. Prevents future authentication and invalidates existing sessions.
    /// Records audit event (SEC-004, SEC-010..016, AUDIT-001..008).
    /// </summary>
    /// <param name="id">User ID</param>
    /// <response code="200">User deactivated successfully; returns updated user info</response>
    /// <response code="400">Invalid request (user not found)</response>
    /// <response code="401">Not authenticated</response>
    /// <response code="403">Authenticated but not authorized (not Administrator)</response>
    [HttpPost("{id}/deactivate")]
    [ProducesResponseType(typeof(UserServiceModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<UserServiceModel>> DeactivateUserAsync(
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _userManagementFacade.DeactivateUserAsync(id, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("does not exist", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "User Not Found",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest,
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Deactivation Failed",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest,
            });
        }
    }

    /// <summary>
    /// Activate a user account.
    /// Administrator-only endpoint. Restores eligibility for authentication.
    /// Records audit event (SEC-004, SEC-010..016, AUDIT-001..008).
    /// </summary>
    /// <param name="id">User ID</param>
    /// <response code="200">User activated successfully; returns updated user info</response>
    /// <response code="400">Invalid request (user not found)</response>
    /// <response code="401">Not authenticated</response>
    /// <response code="403">Authenticated but not authorized (not Administrator)</response>
    [HttpPost("{id}/activate")]
    [ProducesResponseType(typeof(UserServiceModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<UserServiceModel>> ActivateUserAsync(
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _userManagementFacade.ActivateUserAsync(id, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("does not exist", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "User Not Found",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest,
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Activation Failed",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest,
            });
        }
    }

    /// <summary>
    /// Add a role to a user.
    /// Administrator-only endpoint. Assigns role to existing user.
    /// Records audit event (SEC-004, SEC-010..016, AUDIT-001..008).
    /// </summary>
    /// <param name="id">User ID</param>
    /// <param name="request">Add role request (role name)</param>
    /// <response code="200">Role added successfully; returns updated user info</response>
    /// <response code="400">Invalid request (user not found, invalid role, user already in role)</response>
    /// <response code="401">Not authenticated</response>
    /// <response code="403">Authenticated but not authorized (not Administrator)</response>
    [HttpPost("{id}/roles")]
    [ProducesResponseType(typeof(UserServiceModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<UserServiceModel>> AddRoleAsync(
        [FromRoute] Guid id,
        [FromBody] AddRoleViewModel request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _userManagementFacade.AddRoleAsync(id, request, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("does not exist", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "User or Role Not Found",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest,
            });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("already in role", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Role Assignment Failed",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest,
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Role Assignment Failed",
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
    /// Remove a role from a user.
    /// Administrator-only endpoint. Removes role from existing user.
    /// Records audit event (SEC-004, SEC-010..016, AUDIT-001..008).
    /// </summary>
    /// <param name="id">User ID</param>
    /// <param name="roleName">Role name to remove</param>
    /// <response code="200">Role removed successfully; returns updated user info</response>
    /// <response code="400">Invalid request (user not found, user not in role)</response>
    /// <response code="401">Not authenticated</response>
    /// <response code="403">Authenticated but not authorized (not Administrator)</response>
    [HttpDelete("{id}/roles/{roleName}")]
    [ProducesResponseType(typeof(UserServiceModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<UserServiceModel>> RemoveRoleAsync(
        [FromRoute] Guid id,
        [FromRoute] string roleName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _userManagementFacade.RemoveRoleAsync(id, roleName, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("does not exist", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "User Not Found",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest,
            });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not in role", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Role Removal Failed",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest,
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Role Removal Failed",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest,
            });
        }
    }
}

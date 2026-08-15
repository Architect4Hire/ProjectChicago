using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectChicago.Identity.Core.Authorization.Contracts;
using ProjectChicago.Identity.Core.Authorization.Facade;
using ProjectChicago.ServiceDefaults.Filters;

namespace ProjectChicago.Identity.Controllers;

/// <summary>
/// User management endpoints for administrators (SEC-004, SEC-010..016, AUDIT-001..008).
/// Create, read, activate, deactivate users and manage their roles.
/// </summary>
[ApiController]
[Route("users")]
[RequireAuthentication]
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
    /// List application users with pagination. Administrator-only (SEC-004, SEC-010..016).
    /// Returns user ID, email, role, created-at without passwords or security tokens.
    /// </summary>
    /// <param name="request">Pagination request (page, pageSize)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <response code="200">Users retrieved with pagination metadata</response>
    /// <response code="400">Validation error</response>
    /// <response code="401">Not authenticated</response>
    /// <response code="403">Not authorized (requires Administrator role)</response>
    [HttpGet(Name = "ListUsers")]
    [ProducesResponseType(typeof(PagedResponse<UserServiceModel>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResponse<UserServiceModel>>> ListAsync(
        [FromQuery] ListUsersRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await _userManagementFacade.ListUsersAsync(request, cancellationToken).ConfigureAwait(false);

        return Ok(response);
    }

    /// <summary>
    /// Get user detail by ID. Administrator-only (SEC-004, SEC-010..016).
    /// Returns user ID, email, role, created-at; 404 if not found.
    /// </summary>
    /// <param name="id">User ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <response code="200">User retrieved</response>
    /// <response code="401">Not authenticated</response>
    /// <response code="403">Not authorized (requires Administrator role)</response>
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
    /// Create a new user with an assigned role. Administrator-only (SEC-004, SEC-010..016, AUDIT-001..008).
    /// Records audit event on success, validation failure, or duplicate detection.
    /// </summary>
    /// <param name="request">User creation request (email, password, role)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <response code="201">User created</response>
    /// <response code="400">Validation error, password policy violation, or role not found</response>
    /// <response code="401">Not authenticated</response>
    /// <response code="403">Not authorized (requires Administrator role)</response>
    /// <response code="409">Duplicate user (email already exists)</response>
    [HttpPost(Name = "CreateUser")]
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
    /// Deactivate a user account. Administrator-only (SEC-004, SEC-010..016, AUDIT-001..008).
    /// Prevents future authentication and invalidates existing sessions; records audit event.
    /// </summary>
    /// <param name="id">User ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <response code="200">User deactivated</response>
    /// <response code="400">Validation error or user not found</response>
    /// <response code="401">Not authenticated</response>
    /// <response code="403">Not authorized (requires Administrator role)</response>
    [HttpPost("{id}/deactivate", Name = "DeactivateUser")]
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
    /// Activate a user account. Administrator-only (SEC-004, SEC-010..016, AUDIT-001..008).
    /// Restores authentication eligibility; records audit event.
    /// </summary>
    /// <param name="id">User ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <response code="200">User activated</response>
    /// <response code="400">Validation error or user not found</response>
    /// <response code="401">Not authenticated</response>
    /// <response code="403">Not authorized (requires Administrator role)</response>
    [HttpPost("{id}/activate", Name = "ActivateUser")]
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
    /// Add a role to a user. Administrator-only (SEC-004, SEC-010..016, AUDIT-001..008).
    /// Assigns role to user; records audit event.
    /// </summary>
    /// <param name="id">User ID</param>
    /// <param name="request">Add role request (role name)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <response code="200">Role added</response>
    /// <response code="400">Validation error, user not found, invalid role, or user already in role</response>
    /// <response code="401">Not authenticated</response>
    /// <response code="403">Not authorized (requires Administrator role)</response>
    [HttpPost("{id}/roles", Name = "AddRole")]
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
    /// Remove a role from a user. Administrator-only (SEC-004, SEC-010..016, AUDIT-001..008).
    /// Removes role from user; records audit event.
    /// </summary>
    /// <param name="id">User ID</param>
    /// <param name="roleName">Role name to remove</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <response code="200">Role removed</response>
    /// <response code="400">Validation error, user not found, or user not in role</response>
    /// <response code="401">Not authenticated</response>
    /// <response code="403">Not authorized (requires Administrator role)</response>
    [HttpDelete("{id}/roles/{roleName}", Name = "RemoveRole")]
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

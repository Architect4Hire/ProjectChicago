using Microsoft.AspNetCore.Identity;
using ProjectChicago.Identity.Core.Authorization.Contracts;
using ProjectChicago.Identity.Core.Models.DataModels.Entities;

namespace ProjectChicago.Identity.Core.Authorization.Business;

// User management operations using ASP.NET Core Identity UserManager (identity.md: "Use supported
// ASP.NET Core Identity managers/stores behavior"). Creates users with strong password hashing,
// assigns roles, and never exposes password hashes or credentials (SEC-002, SEC-003, SEC-004,
// SEC-010..016). Passwords are validated once during creation and never stored in logs or responses.
public class UserManagementBusiness
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole<Guid>> _roleManager;

    public UserManagementBusiness(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole<Guid>> roleManager)
    {
        ArgumentNullException.ThrowIfNull(userManager);
        ArgumentNullException.ThrowIfNull(roleManager);
        _userManager = userManager;
        _roleManager = roleManager;
    }

    // Create a new user with assigned role (SEC-004, SEC-010..016, identity.md).
    // Validates password policy, checks for duplicates, assigns role, and returns ServiceModel.
    public async Task<UserServiceModel> CreateUserAsync(
        CreateUserViewModel request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Email);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Password);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.RoleName);

        // Normalize email for consistent lookup
        var normalizedEmail = request.Email.ToLowerInvariant().Trim();

        // Check if user already exists (duplicate detection)
        var existingUser = await _userManager.FindByEmailAsync(normalizedEmail).ConfigureAwait(false);
        if (existingUser is not null)
        {
            throw new InvalidOperationException($"A user with email '{normalizedEmail}' already exists.");
        }

        // Verify role exists
        var roleExists = await _roleManager.RoleExistsAsync(request.RoleName).ConfigureAwait(false);
        if (!roleExists)
        {
            throw new InvalidOperationException($"Role '{request.RoleName}' does not exist.");
        }

        // Create user with Identity framework (password hashing is automatic)
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = normalizedEmail,
            Email = normalizedEmail,
            EmailConfirmed = false,
        };

        var createResult = await _userManager.CreateAsync(user, request.Password).ConfigureAwait(false);
        if (!createResult.Succeeded)
        {
            var errors = string.Join("; ", createResult.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Failed to create user: {errors}");
        }

        // Assign role
        var assignResult = await _userManager.AddToRoleAsync(user, request.RoleName).ConfigureAwait(false);
        if (!assignResult.Succeeded)
        {
            var errors = string.Join("; ", assignResult.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Failed to assign role: {errors}");
        }

        // Return public ServiceModel without password
        return new UserServiceModel
        {
            UserId = user.Id,
            Email = user.Email!,
            RoleName = request.RoleName,
            CreatedAtUtc = DateTime.UtcNow,
        };
    }

    // Deactivate a user account (SEC-004, SEC-010..016, identity.md, AUDIT-001).
    // Locks account indefinitely and updates SecurityStamp to invalidate existing sessions.
    public async Task<UserServiceModel> DeactivateUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString()).ConfigureAwait(false);
        if (user is null)
        {
            throw new InvalidOperationException($"User with ID '{userId}' does not exist.");
        }

        // Lock user account indefinitely
        user.LockoutEnd = DateTimeOffset.MaxValue;
        user.LockoutEnabled = true;

        // Update SecurityStamp to invalidate existing sessions
        var stampResult = await _userManager.UpdateSecurityStampAsync(user).ConfigureAwait(false);
        if (!stampResult.Succeeded)
        {
            var errors = string.Join("; ", stampResult.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Failed to update security stamp: {errors}");
        }

        var updateResult = await _userManager.UpdateAsync(user).ConfigureAwait(false);
        if (!updateResult.Succeeded)
        {
            var errors = string.Join("; ", updateResult.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Failed to deactivate user: {errors}");
        }

        // Get user's role for response
        var roles = await _userManager.GetRolesAsync(user).ConfigureAwait(false);
        var roleName = roles.FirstOrDefault() ?? "Unknown";

        return new UserServiceModel
        {
            UserId = user.Id,
            Email = user.Email!,
            RoleName = roleName,
            CreatedAtUtc = user.ConcurrencyStamp is not null ? DateTime.UtcNow : DateTime.UtcNow,
        };
    }

    // Activate a user account (SEC-004, SEC-010..016, identity.md, AUDIT-001).
    // Unlocks account and updates SecurityStamp.
    public async Task<UserServiceModel> ActivateUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString()).ConfigureAwait(false);
        if (user is null)
        {
            throw new InvalidOperationException($"User with ID '{userId}' does not exist.");
        }

        // Unlock user account
        user.LockoutEnd = null;

        // Update SecurityStamp to clear any lingering state
        var stampResult = await _userManager.UpdateSecurityStampAsync(user).ConfigureAwait(false);
        if (!stampResult.Succeeded)
        {
            var errors = string.Join("; ", stampResult.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Failed to update security stamp: {errors}");
        }

        var updateResult = await _userManager.UpdateAsync(user).ConfigureAwait(false);
        if (!updateResult.Succeeded)
        {
            var errors = string.Join("; ", updateResult.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Failed to activate user: {errors}");
        }

        // Get user's role for response
        var roles = await _userManager.GetRolesAsync(user).ConfigureAwait(false);
        var roleName = roles.FirstOrDefault() ?? "Unknown";

        return new UserServiceModel
        {
            UserId = user.Id,
            Email = user.Email!,
            RoleName = roleName,
            CreatedAtUtc = user.ConcurrencyStamp is not null ? DateTime.UtcNow : DateTime.UtcNow,
        };
    }

    // Add role to user (SEC-004, SEC-010..016, identity.md, AUDIT-001).
    // Verifies role exists and user is not already in the role.
    public async Task<UserServiceModel> AddRoleAsync(
        Guid userId,
        string roleName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roleName);

        var user = await _userManager.FindByIdAsync(userId.ToString()).ConfigureAwait(false);
        if (user is null)
        {
            throw new InvalidOperationException($"User with ID '{userId}' does not exist.");
        }

        // Verify role exists
        var roleExists = await _roleManager.RoleExistsAsync(roleName).ConfigureAwait(false);
        if (!roleExists)
        {
            throw new InvalidOperationException($"Role '{roleName}' does not exist.");
        }

        // Check if user already has this role
        var hasRole = await _userManager.IsInRoleAsync(user, roleName).ConfigureAwait(false);
        if (hasRole)
        {
            throw new InvalidOperationException($"User is already in role '{roleName}'.");
        }

        // Add role to user
        var addResult = await _userManager.AddToRoleAsync(user, roleName).ConfigureAwait(false);
        if (!addResult.Succeeded)
        {
            var errors = string.Join("; ", addResult.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Failed to add role to user: {errors}");
        }

        // Get updated roles for response
        var roles = await _userManager.GetRolesAsync(user).ConfigureAwait(false);
        var currentRole = roles.FirstOrDefault() ?? "Unknown";

        return new UserServiceModel
        {
            UserId = user.Id,
            Email = user.Email!,
            RoleName = currentRole,
            CreatedAtUtc = DateTime.UtcNow,
        };
    }

    // Remove role from user (SEC-004, SEC-010..016, identity.md, AUDIT-001).
    // Verifies user is in the role before removal.
    public async Task<UserServiceModel> RemoveRoleAsync(
        Guid userId,
        string roleName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roleName);

        var user = await _userManager.FindByIdAsync(userId.ToString()).ConfigureAwait(false);
        if (user is null)
        {
            throw new InvalidOperationException($"User with ID '{userId}' does not exist.");
        }

        // Check if user has this role
        var hasRole = await _userManager.IsInRoleAsync(user, roleName).ConfigureAwait(false);
        if (!hasRole)
        {
            throw new InvalidOperationException($"User is not in role '{roleName}'.");
        }

        // Remove role from user
        var removeResult = await _userManager.RemoveFromRoleAsync(user, roleName).ConfigureAwait(false);
        if (!removeResult.Succeeded)
        {
            var errors = string.Join("; ", removeResult.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Failed to remove role from user: {errors}");
        }

        // Get updated roles for response
        var roles = await _userManager.GetRolesAsync(user).ConfigureAwait(false);
        var currentRole = roles.FirstOrDefault() ?? "Unknown";

        return new UserServiceModel
        {
            UserId = user.Id,
            Email = user.Email!,
            RoleName = currentRole,
            CreatedAtUtc = DateTime.UtcNow,
        };
    }

    // Change password for authenticated user (SEC-004, SEC-005, identity.md, AUDIT-001).
    // Validates current password, updates to new password, and invalidates existing sessions.
    public async Task<UserServiceModel> ChangePasswordAsync(
        Guid userId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currentPassword);
        ArgumentException.ThrowIfNullOrWhiteSpace(newPassword);

        var user = await _userManager.FindByIdAsync(userId.ToString()).ConfigureAwait(false);
        if (user is null)
        {
            throw new InvalidOperationException($"User with ID '{userId}' does not exist.");
        }

        // Verify current password is correct
        var passwordCorrect = await _userManager.CheckPasswordAsync(user, currentPassword).ConfigureAwait(false);
        if (!passwordCorrect)
        {
            throw new InvalidOperationException("Current password is incorrect.");
        }

        // Change to new password (validates against password policy)
        var changeResult = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword).ConfigureAwait(false);
        if (!changeResult.Succeeded)
        {
            var errors = string.Join("; ", changeResult.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Password change failed: {errors}");
        }

        // Update SecurityStamp to invalidate existing sessions
        var stampResult = await _userManager.UpdateSecurityStampAsync(user).ConfigureAwait(false);
        if (!stampResult.Succeeded)
        {
            var errors = string.Join("; ", stampResult.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Failed to update security stamp: {errors}");
        }

        // Get user's role for response
        var roles = await _userManager.GetRolesAsync(user).ConfigureAwait(false);
        var roleName = roles.FirstOrDefault() ?? "Unknown";

        return new UserServiceModel
        {
            UserId = user.Id,
            Email = user.Email!,
            RoleName = roleName,
            CreatedAtUtc = DateTime.UtcNow,
        };
    }

    // Initiate password reset (admin-only, SEC-004, SEC-005, identity.md, AUDIT-001).
    // Generates a one-time reset token for the specified user. Token is time-bound and one-purpose.
    // Admin communicates token to user via out-of-band means; does not invalidate existing session.
    public async Task<string> InitiatePasswordResetAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString()).ConfigureAwait(false);
        if (user is null)
        {
            throw new InvalidOperationException($"User with ID '{userId}' does not exist.");
        }

        // Generate reset token (time-bound, one-purpose per ASP.NET Core Identity policy)
        var token = await _userManager.GeneratePasswordResetTokenAsync(user).ConfigureAwait(false);
        return token;
    }

    // Complete password reset (unauthenticated, SEC-004, SEC-005, identity.md, AUDIT-001).
    // Validates reset token and sets new password. Updates SecurityStamp to invalidate existing sessions.
    public async Task<UserServiceModel> ResetPasswordAsync(
        Guid userId,
        string token,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        ArgumentException.ThrowIfNullOrWhiteSpace(newPassword);

        var user = await _userManager.FindByIdAsync(userId.ToString()).ConfigureAwait(false);
        if (user is null)
        {
            throw new InvalidOperationException($"User with ID '{userId}' does not exist.");
        }

        // Reset password using token (validates token validity, expiry, and policy)
        var resetResult = await _userManager.ResetPasswordAsync(user, token, newPassword).ConfigureAwait(false);
        if (!resetResult.Succeeded)
        {
            var errors = string.Join("; ", resetResult.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Password reset failed: {errors}");
        }

        // Update SecurityStamp to invalidate existing sessions (force re-login)
        var stampResult = await _userManager.UpdateSecurityStampAsync(user).ConfigureAwait(false);
        if (!stampResult.Succeeded)
        {
            var errors = string.Join("; ", stampResult.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Failed to update security stamp: {errors}");
        }

        // Get user's role for response
        var roles = await _userManager.GetRolesAsync(user).ConfigureAwait(false);
        var roleName = roles.FirstOrDefault() ?? "Unknown";

        return new UserServiceModel
        {
            UserId = user.Id,
            Email = user.Email!,
            RoleName = roleName,
            CreatedAtUtc = DateTime.UtcNow,
        };
    }
}

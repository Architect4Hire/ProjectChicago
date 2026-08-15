using Microsoft.AspNetCore.Identity;
using ProjectChicago.Identity.Core.Models.DataModels.Entities;

namespace ProjectChicago.Identity.Core.Authorization.Data;

// Seeds default user account for local development (development-only, not for production).
// Idempotent: if the user already exists, it is skipped.
public sealed class UserSeeder
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole<Guid>> _roleManager;

    public UserSeeder(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole<Guid>> roleManager)
    {
        ArgumentNullException.ThrowIfNull(userManager);
        ArgumentNullException.ThrowIfNull(roleManager);
        _userManager = userManager;
        _roleManager = roleManager;
    }

    public async Task SeedDefaultUserAsync(
        string email,
        string password,
        string roleName = "Administrator",
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        ArgumentException.ThrowIfNullOrWhiteSpace(roleName);

        // Check if user already exists
        var existingUser = await _userManager.FindByEmailAsync(email);
        if (existingUser is not null)
        {
            return; // User already exists, skip
        }

        // Ensure role exists
        var roleExists = await _roleManager.RoleExistsAsync(roleName);
        if (!roleExists)
        {
            var roleResult = await _roleManager.CreateAsync(new IdentityRole<Guid> { Name = roleName });
            if (!roleResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Failed to create role '{roleName}': {string.Join(", ", roleResult.Errors.Select(e => e.Description))}");
            }
        }

        // Create user
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            EmailConfirmed = true,
        };

        var createResult = await _userManager.CreateAsync(user, password);
        if (!createResult.Succeeded)
        {
            throw new InvalidOperationException(
                $"Failed to create user '{email}': {string.Join(", ", createResult.Errors.Select(e => e.Description))}");
        }

        // Assign role
        var roleResult2 = await _userManager.AddToRoleAsync(user, roleName);
        if (!roleResult2.Succeeded)
        {
            throw new InvalidOperationException(
                $"Failed to assign role '{roleName}' to user '{email}': {string.Join(", ", roleResult2.Errors.Select(e => e.Description))}");
        }
    }
}

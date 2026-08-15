using Microsoft.AspNetCore.Identity;

namespace ProjectChicago.Identity.Core.Authorization.Data;

// Role seeding operations using Identity RoleManager (identity.md: "Use supported ASP.NET Core Identity
// managers/stores behavior rather than custom credential code"). Idempotent - repeated calls are safe and
// do not duplicate roles. Only creates roles if they do not already exist (checked by NormalizedName).
// (SEC-010..016): Establishes deterministic role names for server-side authorization.
public class RoleSeeder
{
    private readonly RoleManager<IdentityRole<Guid>> _roleManager;

    public RoleSeeder(RoleManager<IdentityRole<Guid>> roleManager)
    {
        ArgumentNullException.ThrowIfNull(roleManager);
        _roleManager = roleManager;
    }

    public async Task SeedDefaultRolesAsync(CancellationToken cancellationToken = default)
    {
        var roles = new[] { "Administrator", "Manager", "Contributor", "ReadOnly" };
        foreach (var roleName in roles)
        {
            await CreateRoleIfNotExistsAsync(roleName, cancellationToken);
        }
    }

    private async Task CreateRoleIfNotExistsAsync(string roleName, CancellationToken cancellationToken)
    {
        var normalizedName = roleName.ToUpperInvariant();
        var exists = await _roleManager.RoleExistsAsync(roleName);

        if (!exists)
        {
            var role = new IdentityRole<Guid> { Id = Guid.NewGuid(), Name = roleName };
            var result = await _roleManager.CreateAsync(role);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Failed to create role '{roleName}': {string.Join(", ", result.Errors.Select(e => e.Description))}");
            }
        }
    }
}

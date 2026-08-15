using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ProjectChicago.Identity.Core.Authorization.Business;
using ProjectChicago.Identity.Core.Models.DataModels.Entities;
using ProjectChicago.Identity.Core.Persistence;
using ProjectChicago.Identity.Core.Tests.Persistence;
using IdentityDbContext = ProjectChicago.Identity.Core.Persistence.IdentityDbContext;
using Xunit;

namespace ProjectChicago.Identity.Core.Tests.Authorization.Business;

// Password reset initiation business logic tests (SEC-004, SEC-005).
// Verify token generation, one-time token behavior, and user existence checks.
public sealed class PasswordResetInitiationTests : IClassFixture<MsSqlContainerFixture>
{
    private readonly MsSqlContainerFixture _fixture;

    public PasswordResetInitiationTests(MsSqlContainerFixture fixture)
    {
        _fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
    }

    private async Task<(IdentityDbContext, UserManager<ApplicationUser>, RoleManager<IdentityRole<Guid>>)> CreateContextAndManagersAsync(string databaseName)
    {
        var builder = new SqlConnectionStringBuilder(_fixture.ConnectionString)
        {
            InitialCatalog = databaseName,
        };

        var dbContextBuilder = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseSqlServer(builder.ConnectionString)
            .Options;

        var context = new IdentityDbContext(dbContextBuilder);
        await context.Database.EnsureCreatedAsync();

        var userStore = new UserStore<ApplicationUser, IdentityRole<Guid>, IdentityDbContext, Guid>(context);
        var userManager = new UserManager<ApplicationUser>(
            userStore,
            null!,
            new PasswordHasher<ApplicationUser>(),
            Enumerable.Empty<IUserValidator<ApplicationUser>>(),
            Enumerable.Empty<IPasswordValidator<ApplicationUser>>(),
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            null!,
            null!
        );

        var roleStore = new IdentityRoleStore(context);
        var roleManager = new RoleManager<IdentityRole<Guid>>(
            roleStore,
            Enumerable.Empty<IRoleValidator<IdentityRole<Guid>>>(),
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            null!
        );

        return (context, userManager, roleManager);
    }

    private class IdentityRoleStore : IRoleStore<IdentityRole<Guid>>
    {
        private readonly IdentityDbContext _context;

        public IdentityRoleStore(IdentityDbContext context)
        {
            _context = context;
        }

        public async Task<IdentityResult> CreateAsync(IdentityRole<Guid> role, CancellationToken cancellationToken)
        {
            _context.Roles.Add(role);
            await _context.SaveChangesAsync(cancellationToken);
            return IdentityResult.Success;
        }

        public async Task<IdentityResult> UpdateAsync(IdentityRole<Guid> role, CancellationToken cancellationToken)
        {
            _context.Roles.Update(role);
            await _context.SaveChangesAsync(cancellationToken);
            return IdentityResult.Success;
        }

        public async Task<IdentityResult> DeleteAsync(IdentityRole<Guid> role, CancellationToken cancellationToken)
        {
            _context.Roles.Remove(role);
            await _context.SaveChangesAsync(cancellationToken);
            return IdentityResult.Success;
        }

        public async Task<IdentityRole<Guid>?> FindByIdAsync(string id, CancellationToken cancellationToken)
        {
            return await _context.Roles.FindAsync(new object[] { Guid.Parse(id) }, cancellationToken: cancellationToken);
        }

        public async Task<IdentityRole<Guid>?> FindByNameAsync(string normalizedRoleName, CancellationToken cancellationToken)
        {
            return await _context.Roles.FirstOrDefaultAsync(r => r.NormalizedName == normalizedRoleName, cancellationToken);
        }

        public Task<string> GetRoleIdAsync(IdentityRole<Guid> role, CancellationToken cancellationToken) => Task.FromResult(role.Id.ToString());
        public Task<string?> GetRoleNameAsync(IdentityRole<Guid> role, CancellationToken cancellationToken) => Task.FromResult<string?>(role.Name);
        public Task SetRoleNameAsync(IdentityRole<Guid> role, string? roleName, CancellationToken cancellationToken)
        {
            role.Name = roleName;
            return Task.CompletedTask;
        }
        public Task<string?> GetNormalizedRoleNameAsync(IdentityRole<Guid> role, CancellationToken cancellationToken) => Task.FromResult<string?>(role.NormalizedName);
        public Task SetNormalizedRoleNameAsync(IdentityRole<Guid> role, string? normalizedName, CancellationToken cancellationToken)
        {
            role.NormalizedName = normalizedName;
            return Task.CompletedTask;
        }

        public void Dispose() { }
    }

    [Fact]
    public async Task InitiateReset_ExistingUser_GeneratesToken()
    {
        var (db, userManager, roleManager) = await CreateContextAndManagersAsync($"IdentityDb_{Guid.NewGuid():N}");
        using (db)
        using (userManager)
        using (roleManager)
        {
            var cred1 = "AbcDefg@123456";

            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = "test@example.com",
                Email = "test@example.com",
            };
            await userManager.CreateAsync(user, cred1);

            var business = new UserManagementBusiness(userManager, roleManager);

            // Act: Initiate reset
            var token = await business.InitiatePasswordResetAsync(user.Id);

            // Assert: Token generated
            Assert.NotNull(token);
            Assert.NotEmpty(token);
        }
    }

    [Fact]
    public async Task InitiateReset_NonexistentUser_Fails()
    {
        var (db, userManager, roleManager) = await CreateContextAndManagersAsync($"IdentityDb_{Guid.NewGuid():N}");
        using (db)
        using (userManager)
        using (roleManager)
        {
            var business = new UserManagementBusiness(userManager, roleManager);
            var nonexistentUserId = Guid.NewGuid();

            // Act & Assert: Exception thrown
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => business.InitiatePasswordResetAsync(nonexistentUserId)
            );

            Assert.Contains("does not exist", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task ResetPassword_ValidToken_SucceedsAndInvalidatesSessions()
    {
        var (db, userManager, roleManager) = await CreateContextAndManagersAsync($"IdentityDb_{Guid.NewGuid():N}");
        using (db)
        using (userManager)
        using (roleManager)
        {
            var cred1 = "AbcDefg@123456";
            var cred2 = "XyzWuv@123456";

            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = "test@example.com",
                Email = "test@example.com",
            };
            await userManager.CreateAsync(user, cred1);

            var business = new UserManagementBusiness(userManager, roleManager);

            // Generate reset token
            var token = await business.InitiatePasswordResetAsync(user.Id);

            var stampBefore = user.SecurityStamp;

            // Act: Complete reset
            var result = await business.ResetPasswordAsync(user.Id, token, cred2);

            // Assert: Reset successful
            Assert.NotNull(result);
            Assert.Equal(user.Id, result.UserId);

            // Verify new password works
            var updatedUser = await userManager.FindByIdAsync(user.Id.ToString());
            Assert.NotNull(updatedUser);
            var newValid = await userManager.CheckPasswordAsync(updatedUser, cred2);
            Assert.True(newValid);

            // Verify old password no longer works
            var oldValid = await userManager.CheckPasswordAsync(updatedUser, cred1);
            Assert.False(oldValid);

            // Verify SecurityStamp changed (session invalidation)
            Assert.NotEqual(stampBefore, updatedUser.SecurityStamp);
        }
    }

    [Fact]
    public async Task ResetPassword_InvalidToken_Fails()
    {
        var (db, userManager, roleManager) = await CreateContextAndManagersAsync($"IdentityDb_{Guid.NewGuid():N}");
        using (db)
        using (userManager)
        using (roleManager)
        {
            var cred1 = "AbcDefg@123456";
            var cred2 = "XyzWuv@123456";

            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = "test@example.com",
                Email = "test@example.com",
            };
            await userManager.CreateAsync(user, cred1);

            var business = new UserManagementBusiness(userManager, roleManager);

            // Act & Assert: Invalid token rejected
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => business.ResetPasswordAsync(user.Id, "invalidtoken", cred2)
            );

            Assert.Contains("failed", exception.Message, StringComparison.OrdinalIgnoreCase);

            // Verify password unchanged
            var updatedUser = await userManager.FindByIdAsync(user.Id.ToString());
            Assert.NotNull(updatedUser);
            var stillValid = await userManager.CheckPasswordAsync(updatedUser, cred1);
            Assert.True(stillValid);
        }
    }

    [Fact]
    public async Task ResetPassword_WeakNew_Fails()
    {
        var (db, userManager, roleManager) = await CreateContextAndManagersAsync($"IdentityDb_{Guid.NewGuid():N}");
        using (db)
        using (userManager)
        using (roleManager)
        {
            var cred1 = "AbcDefg@123456";
            var credWeak = "a";

            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = "test@example.com",
                Email = "test@example.com",
            };
            await userManager.CreateAsync(user, cred1);

            var business = new UserManagementBusiness(userManager, roleManager);

            // Generate valid token
            var token = await business.InitiatePasswordResetAsync(user.Id);

            // Act & Assert: Weak password rejected
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => business.ResetPasswordAsync(user.Id, token, credWeak)
            );

            Assert.Contains("failed", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task ResetPassword_TokenReuseAfterFirstReset_Fails()
    {
        var (db, userManager, roleManager) = await CreateContextAndManagersAsync($"IdentityDb_{Guid.NewGuid():N}");
        using (db)
        using (userManager)
        using (roleManager)
        {
            var cred1 = "AbcDefg@123456";
            var cred2 = "XyzWuv@123456";
            var cred3 = "QweMnb@123456";

            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = "test@example.com",
                Email = "test@example.com",
            };
            await userManager.CreateAsync(user, cred1);

            var business = new UserManagementBusiness(userManager, roleManager);

            // Generate reset token
            var token = await business.InitiatePasswordResetAsync(user.Id);

            // Reset password once (token becomes invalid)
            await business.ResetPasswordAsync(user.Id, token, cred2);

            // Act & Assert: Try to reuse same token (should fail)
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => business.ResetPasswordAsync(user.Id, token, cred3)
            );

            Assert.Contains("failed", exception.Message, StringComparison.OrdinalIgnoreCase);

            // Verify password is still cred2 (not cred3)
            var updatedUser = await userManager.FindByIdAsync(user.Id.ToString());
            Assert.NotNull(updatedUser);
            var cred2Valid = await userManager.CheckPasswordAsync(updatedUser, cred2);
            Assert.True(cred2Valid);
        }
    }
}

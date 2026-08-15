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

// Password change business logic tests (SEC-004, SEC-005).
// Verify change operations, current credential validation, policy enforcement, and session invalidation.
public sealed class PasswordChangeBusinessTests : IClassFixture<MsSqlContainerFixture>
{
    private readonly MsSqlContainerFixture _fixture;

    public PasswordChangeBusinessTests(MsSqlContainerFixture fixture)
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
    public async Task ChangeCredential_CorrectCurrent_SucceedsAndUpdates()
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
            var createResult = await userManager.CreateAsync(user, cred1);
            Assert.True(createResult.Succeeded);

            var business = new UserManagementBusiness(userManager, roleManager);

            // Act: Change credential with correct current
            var result = await business.ChangePasswordAsync(user.Id, cred1, cred2);

            // Assert: Changed successfully
            Assert.NotNull(result);
            Assert.Equal(user.Id, result.UserId);
            Assert.Equal(user.Email, result.Email);

            // Verify new works
            var updatedUser = await userManager.FindByIdAsync(user.Id.ToString());
            Assert.NotNull(updatedUser);
            var newValid = await userManager.CheckPasswordAsync(updatedUser, cred2);
            Assert.True(newValid);

            // Verify old no longer works
            var oldValid = await userManager.CheckPasswordAsync(updatedUser, cred1);
            Assert.False(oldValid);
        }
    }

    [Fact]
    public async Task ChangeCredential_IncorrectCurrent_Fails()
    {
        var (db, userManager, roleManager) = await CreateContextAndManagersAsync($"IdentityDb_{Guid.NewGuid():N}");
        using (db)
        using (userManager)
        using (roleManager)
        {
            var cred1 = "AbcDefg@123456";
            var credWrong = "ZzzZzz@123456";
            var cred2 = "XyzWuv@123456";

            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = "test@example.com",
                Email = "test@example.com",
            };
            await userManager.CreateAsync(user, cred1);

            var business = new UserManagementBusiness(userManager, roleManager);

            // Act: Try to change with wrong current
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => business.ChangePasswordAsync(user.Id, credWrong, cred2)
            );

            Assert.Contains("incorrect", exception.Message, StringComparison.OrdinalIgnoreCase);

            // Verify wasn't changed
            var updatedUser = await userManager.FindByIdAsync(user.Id.ToString());
            Assert.NotNull(updatedUser);
            var stillValid = await userManager.CheckPasswordAsync(updatedUser, cred1);
            Assert.True(stillValid);
        }
    }

    [Fact]
    public async Task ChangeCredential_CorrectCurrent_UpdatesSecurityStamp()
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

            var userBefore = await userManager.FindByIdAsync(user.Id.ToString());
            Assert.NotNull(userBefore);
            var stampBefore = userBefore.SecurityStamp;

            var business = new UserManagementBusiness(userManager, roleManager);

            // Act: Change credential
            await business.ChangePasswordAsync(user.Id, cred1, cred2);

            // Assert: SecurityStamp changed
            var userAfter = await userManager.FindByIdAsync(user.Id.ToString());
            Assert.NotNull(userAfter);
            var stampAfter = userAfter.SecurityStamp;

            Assert.NotEqual(stampBefore, stampAfter);
        }
    }

    [Fact]
    public async Task ChangeCredential_UserNotFound_Fails()
    {
        var (db, userManager, roleManager) = await CreateContextAndManagersAsync($"IdentityDb_{Guid.NewGuid():N}");
        using (db)
        using (userManager)
        using (roleManager)
        {
            var cred1 = "AbcDefg@123456";
            var cred2 = "XyzWuv@123456";

            var business = new UserManagementBusiness(userManager, roleManager);
            var nonexistentUserId = Guid.NewGuid();

            // Act & Assert: Exception thrown
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => business.ChangePasswordAsync(nonexistentUserId, cred1, cred2)
            );

            Assert.Contains("does not exist", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task ChangeCredential_WeakNew_Fails()
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

            // Act: Try to change to weak credential
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => business.ChangePasswordAsync(user.Id, cred1, credWeak)
            );

            Assert.Contains("failed", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
    }
}

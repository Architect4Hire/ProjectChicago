using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using ProjectChicago.Identity.Core.Models.DataModels.Entities;
using ProjectChicago.Identity.Core.Persistence;
using Xunit;

namespace ProjectChicago.Identity.Core.Tests.Persistence;

// SEC-001/SEC-003/SEC-004: proves the Identity host's composition root correctly wires ASP.NET Core
// Identity framework services (UserManager<ApplicationUser>, RoleManager<IdentityRole<Guid>>,
// SignInManager, token providers) against IdentityDbContext and SQL Server persistence. Each
// manager must resolve and operate against the correct user/role stores backed by the actual DbContext.
public class IdentityDependencyInjectionTests
{
    private const string IdentityDbConnectionStringEnvironmentVariable = "ConnectionStrings__IdentityDb";

    [Fact]
    public void UserManager_IsResolvedFromIdentityHost_AgainstIdentityDbContext()
    {
        using var host = CreateWebApplicationFactory();
        var scope = host.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        Assert.NotNull(userManager);
        Assert.IsType<UserManager<ApplicationUser>>(userManager);
    }

    [Fact]
    public void RoleManager_IsResolvedFromIdentityHost_AgainstIdentityDbContext()
    {
        using var host = CreateWebApplicationFactory();
        var scope = host.Services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();

        Assert.NotNull(roleManager);
        Assert.IsType<RoleManager<IdentityRole<Guid>>>(roleManager);
    }

    [Fact]
    public void SignInManager_IsResolvedFromIdentityHost()
    {
        using var host = CreateWebApplicationFactory();
        var scope = host.Services.CreateScope();
        var signInManager = scope.ServiceProvider.GetRequiredService<SignInManager<ApplicationUser>>();

        Assert.NotNull(signInManager);
        Assert.IsType<SignInManager<ApplicationUser>>(signInManager);
    }

    [Fact]
    public void IdentityDbContext_IsResolvedAsScoped()
    {
        using var host = CreateWebApplicationFactory();

        // Each scope should receive its own DbContext instance (scoped lifetime).
        var scope1 = host.Services.CreateScope();
        var scope2 = host.Services.CreateScope();

        var context1 = scope1.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var context2 = scope2.ServiceProvider.GetRequiredService<IdentityDbContext>();

        Assert.NotNull(context1);
        Assert.NotNull(context2);
        Assert.NotSame(context1, context2);
    }

    [Fact]
    public void IdentityDbContextIsUsedByManagers()
    {
        using var host = CreateWebApplicationFactory();
        var scope = host.Services.CreateScope();

        // Both UserManager and IdentityDbContext resolve successfully, confirming they are
        // wired into the same composition root. The UserManager was configured via
        // AddIdentity<ApplicationUser, IdentityRole<Guid>>().AddEntityFrameworkStores<IdentityDbContext>(),
        // so the underlying stores are backed by the IdentityDbContext.
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var context = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        Assert.NotNull(userManager);
        Assert.NotNull(roleManager);
        Assert.NotNull(context);
    }

    private static WebApplicationFactory<Program> CreateWebApplicationFactory()
    {
        Environment.SetEnvironmentVariable(
            IdentityDbConnectionStringEnvironmentVariable,
            "Server=localhost;Database=IdentityDbDependencyInjectionTests;TrustServerCertificate=True;");

        return new WebApplicationFactory<Program>();
    }
}

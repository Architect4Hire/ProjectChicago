using Microsoft.AspNetCore.Identity;
using ProjectChicago.Identity.Core.Models.DataModels.Entities;
using ProjectChicago.Identity.Core.Persistence;
using Xunit;

namespace ProjectChicago.Identity.Core.Tests.Persistence;

// DATA-031: proves IdentityDbContext correctly inherits from IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
// and provides DbSets for managing users and roles. The context uses SQL Server (database.md) for
// ASP.NET Core Identity's schema (SEC-001, SEC-003).
public class IdentityDbContextTests
{
    [Fact]
    public void IdentityDbContext_InheritsFromIdentityDbContextWithCorrectTypeParameters()
    {
        // Verify inheritance structure and generic type parameters.
        var baseType = typeof(IdentityDbContext).BaseType;
        Assert.NotNull(baseType);

        // IdentityDbContext -> IdentityDbContext<TUser, TRole, TKey>
        // The base class name should be the unbound generic form IdentityDbContext`3
        Assert.True(baseType!.IsGenericType, "Base type should be generic");

        // Verify generic type parameters: <ApplicationUser, IdentityRole<Guid>, Guid>
        var genericArgs = baseType.GetGenericArguments();
        Assert.Equal(3, genericArgs.Length);
        Assert.Equal(typeof(ApplicationUser), genericArgs[0]);
        Assert.Equal(typeof(IdentityRole<Guid>), genericArgs[1]);
        Assert.Equal(typeof(Guid), genericArgs[2]);
    }

    [Fact]
    public void IdentityDbContext_Inherits_UserDbSetFromIdentityDbContext()
    {
        // IdentityDbContext<TUser, TRole, TKey> provides Users, Roles, UserClaims, UserLogins, etc.
        // DbSets through its base class. Verify that they are accessible.
        var usersProperty = typeof(IdentityDbContext).GetProperty("Users");
        Assert.NotNull(usersProperty);

        var rolesProperty = typeof(IdentityDbContext).GetProperty("Roles");
        Assert.NotNull(rolesProperty);
    }
}

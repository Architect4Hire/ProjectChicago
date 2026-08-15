using Microsoft.AspNetCore.Identity;
using ProjectChicago.Identity.Core.Models.DataModels.Entities;
using Xunit;

namespace ProjectChicago.Identity.Core.Tests.Models;

// SEC-001/DATA-007: proves ApplicationUser correctly extends IdentityUser<Guid> and inherits all
// required ASP.NET Core Identity properties for account management, authentication, and security
// (password hashing, lockout, email/phone confirmation, security tokens).
public class ApplicationUserTests
{
    [Fact]
    public void ApplicationUser_InheritsFromIdentityUserGuid()
    {
        // Verify inheritance structure: ApplicationUser -> IdentityUser<Guid>
        var baseType = typeof(ApplicationUser).BaseType;
        Assert.NotNull(baseType);
        Assert.Equal(typeof(IdentityUser<Guid>), baseType);
    }

    [Fact]
    public void ApplicationUser_PrimaryKeyIsGuid()
    {
        // DATA-007: public identifiers are application-assigned GUIDs, not database-generated
        // sequential values. Guid is safe to expose externally without a separate public-ID field.
        var idProperty = typeof(ApplicationUser).GetProperty(nameof(ApplicationUser.Id));
        Assert.NotNull(idProperty);
        Assert.Equal(typeof(Guid), idProperty!.PropertyType);
    }

    [Fact]
    public void ApplicationUser_InheritsRequiredIdentityProperties()
    {
        // Verify that ApplicationUser has access to core Identity properties through inheritance.
        var properties = typeof(ApplicationUser).GetProperties();
        var propertyNames = properties.Select(p => p.Name).ToHashSet();

        // SEC-004: account creation, activation, deactivation, lockout, password reset
        Assert.Contains(nameof(IdentityUser<Guid>.Id), propertyNames);
        Assert.Contains(nameof(IdentityUser<Guid>.UserName), propertyNames);
        Assert.Contains(nameof(IdentityUser<Guid>.Email), propertyNames);
        Assert.Contains(nameof(IdentityUser<Guid>.EmailConfirmed), propertyNames);
        Assert.Contains(nameof(IdentityUser<Guid>.PasswordHash), propertyNames);
        Assert.Contains(nameof(IdentityUser<Guid>.PhoneNumber), propertyNames);
        Assert.Contains(nameof(IdentityUser<Guid>.PhoneNumberConfirmed), propertyNames);
        Assert.Contains(nameof(IdentityUser<Guid>.TwoFactorEnabled), propertyNames);
        Assert.Contains(nameof(IdentityUser<Guid>.LockoutEnd), propertyNames);
        Assert.Contains(nameof(IdentityUser<Guid>.LockoutEnabled), propertyNames);
        Assert.Contains(nameof(IdentityUser<Guid>.AccessFailedCount), propertyNames);
        Assert.Contains(nameof(IdentityUser<Guid>.SecurityStamp), propertyNames);
        Assert.Contains(nameof(IdentityUser<Guid>.ConcurrencyStamp), propertyNames);
    }

    [Fact]
    public void ApplicationUser_CanBeInstantiated()
    {
        // Verify that ApplicationUser can be created (it should have a parameterless constructor
        // inherited from IdentityUser).
        var user = new ApplicationUser();
        Assert.NotNull(user);
        Assert.Equal(Guid.Empty, user.Id);
    }
}

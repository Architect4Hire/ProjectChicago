using Microsoft.AspNetCore.Identity;

namespace ProjectChicago.Identity.Core.Models.DataModels.Entities;

// Identity's application user (SEC-001, SEC-004, DATA-031). Uses Guid as the primary key for
// consistency with other CRM entities (DATA-007: public identifiers are application-assigned safe
// GUIDs, not database-generated sequential values). ASP.NET Core Identity provides password hashing,
// account security, lockout, roles/claims, and token management through supported framework APIs
// (SEC-002, SEC-003). All application-level properties are immutable in construction; state changes
// happen only through authorized facades (backend.md).
public sealed class ApplicationUser : IdentityUser<Guid>
{
    // ApplicationUser inherits from IdentityUser<Guid>:
    //   - Id (Guid primary key)
    //   - UserName (unique string user/login identity)
    //   - NormalizedUserName
    //   - Email
    //   - NormalizedEmail
    //   - EmailConfirmed
    //   - PasswordHash
    //   - SecurityStamp
    //   - ConcurrencyStamp
    //   - PhoneNumber
    //   - PhoneNumberConfirmed
    //   - TwoFactorEnabled
    //   - LockoutEnd (nullable DateTimeOffset for lockout tracking per SEC-004)
    //   - LockoutEnabled
    //   - AccessFailedCount
    //   - (managed by UserManager through IdentityUserStore)
    //
    // Additional columns for CRM-specific identity context may be added in a future microstep
    // alongside appropriate Business/Facade/Data/Repository patterns for account lifecycle
    // operations. For now, this model establishes the core IdentityUser foundation.
}

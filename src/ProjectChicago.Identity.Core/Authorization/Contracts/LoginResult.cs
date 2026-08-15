using ProjectChicago.Identity.Core.Models.DataModels.Entities;

namespace ProjectChicago.Identity.Core.Authorization.Contracts;

// Result of login attempt indicating success, failure reason, and user entity if applicable
// (SEC-001..025, AUDIT-001). Used internally by Facade to determine which audit event to record.
public sealed class LoginResult
{
    public required LoginOutcome Outcome { get; init; }

    public LoginServiceModel? ServiceModel { get; init; }

    public ApplicationUser? User { get; init; }

    public string? AttemptedUsername { get; init; }

    public string? ErrorMessage { get; init; }
}

// Login attempt outcomes for audit event selection (SEC-001..025, AUDIT-001).
public enum LoginOutcome
{
    Success,
    FailedInvalidCredentials,
    FailedAccountLocked,
    FailedTwoFactorRequired,
}

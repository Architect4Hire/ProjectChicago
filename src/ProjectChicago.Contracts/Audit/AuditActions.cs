namespace ProjectChicago.Contracts.Audit;

// The common EntityMutationAudited.Action vocabulary from AUDIT-003. "Common" is explicitly not
// exhaustive, so Action stays a plain string on the contract; these constants are the
// well-known, documented values rather than a closed set.
public static class AuditActions
{
    public const string Created = "Created";
    public const string Updated = "Updated";
    public const string StatusChanged = "StatusChanged";
    public const string Assigned = "Assigned";
    public const string Reassigned = "Reassigned";
    public const string PriorityChanged = "PriorityChanged";
    public const string Completed = "Completed";
    public const string Reopened = "Reopened";
    public const string Archived = "Archived";
    public const string Restored = "Restored";

    // Authentication events (Identity service, SEC-001..025, AUDIT-001).
    public const string LoggedIn = "LoggedIn";
    public const string FailedLogin = "FailedLogin";
    public const string AccountLocked = "AccountLocked";
    public const string LoggedOut = "LoggedOut";

    // User management events (Identity service, SEC-004, AUDIT-001).
    public const string UserCreated = "UserCreated";
    public const string UserDeactivated = "UserDeactivated";
    public const string UserActivated = "UserActivated";
    public const string RoleAdded = "RoleAdded";
    public const string RoleRemoved = "RoleRemoved";
    public const string PasswordChanged = "PasswordChanged";
    public const string PasswordResetInitiated = "PasswordResetInitiated";
    public const string PasswordReset = "PasswordReset";
}

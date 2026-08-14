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
}

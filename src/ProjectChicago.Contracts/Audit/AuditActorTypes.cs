namespace ProjectChicago.Contracts.Audit;

// The closed, stable vocabulary for EntityMutationAudited.ActorType (AUDIT-002, AUDIT-006).
// A plain string constant (not an enum) so the wire value stays stable regardless of a
// consumer's JSON enum-serialization configuration.
public static class AuditActorTypes
{
    public const string User = "User";
    public const string Service = "Service";
    public const string System = "System";
    public const string Anonymous = "Anonymous";
}

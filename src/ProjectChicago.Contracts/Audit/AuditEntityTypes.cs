namespace ProjectChicago.Contracts.Audit;

// EntityMutationAudited.EntityType values for the entities ADR-0015 permanently assigns to CRM
// (AUDIT-001). Extend only alongside a superseding architecture decision, not incidentally.
public static class AuditEntityTypes
{
    public const string Client = "Client";
    public const string Project = "Project";
    public const string Task = "Task";
}

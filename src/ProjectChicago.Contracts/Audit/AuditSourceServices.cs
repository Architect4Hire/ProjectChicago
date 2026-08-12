namespace ProjectChicago.Contracts.Audit;

// EntityMutationAudited.SourceService values for the ADR-0015 bounded services that own
// mutating CRM/Identity/Notification/Workflow behavior. Audit and Search are consumers/read
// models, not mutation owners, so they are intentionally absent here.
public static class AuditSourceServices
{
    public const string Crm = "Crm";
    public const string Identity = "Identity";
    public const string Notification = "Notification";
    public const string Workflow = "Workflow";
}

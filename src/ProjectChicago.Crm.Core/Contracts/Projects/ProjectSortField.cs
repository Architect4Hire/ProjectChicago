namespace ProjectChicago.Crm.Contracts.Projects;

// Wire contract for Project list sort field (PROJECT-023). Mirrors ClientSortField to keep sort
// semantics consistent across CRM entities. Maps to ProjectListSortField in the Core layer.
public enum ProjectSortField
{
    Name = 0,

    LastModifiedAtUtc = 1,

    CreatedAtUtc = 2,

    Status = 3,

    Priority = 4,

    TargetCompletionDateUtc = 5,
}

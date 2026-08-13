namespace ProjectChicago.Crm.Contracts.Projects;

// Wire contract for Project list sort direction (PROJECT-023). Mirrors ClientSortDirection
// to keep sort semantics consistent across CRM entities.
public enum ProjectSortDirection
{
    Ascending = 0,

    Descending = 1,
}

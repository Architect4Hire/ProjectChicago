namespace ProjectChicago.Crm.Core.Repositories;

// Sort direction for IProjectRepository.ListAsync (PROJECT-023). Mirrors ClientListSortDirection
// (CLIENT-023) - a caller chooses Ascending (oldest/first) or Descending (newest/last) for the
// selected sort field.
public enum ProjectListSortDirection
{
    Ascending = 0,

    Descending = 1,
}

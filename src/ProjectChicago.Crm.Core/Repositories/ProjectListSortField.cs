namespace ProjectChicago.Crm.Core.Repositories;

// Sort field options for IProjectRepository.ListAsync (PROJECT-020..023). Mirrors ClientListSortField
// (CLIENT-023) to keep sort semantics consistent across CRM entities. A sortable field is one where
// a caller might reasonably want results ordered (e.g., by date to find newest/oldest work, by
// priority to find critical work). All sort translations are handled by the Repository (not Business
// or Facade), so callers depend on this enum's stability - renaming or removing a field requires
// explicit design approval and coordinated contract/wire changes.
public enum ProjectListSortField
{
    // Default: sort by name (PROJECT-023 / PERF-003; repository's ApplySort uses this when the sort
    // field is unmatched or null - see ClientRepository's sort pattern).
    Name = 0,

    // Sort by most-recently-modified first (PROJECT-023).
    LastModifiedAtUtc = 1,

    // Sort by creation order (PROJECT-023).
    CreatedAtUtc = 2,

    // Sort by project status (PROJECT-023).
    Status = 3,

    // Sort by assigned priority (PROJECT-023).
    Priority = 4,

    // Sort by target completion date (PROJECT-023).
    TargetCompletionDateUtc = 5,
}

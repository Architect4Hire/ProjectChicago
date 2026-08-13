using ProjectChicago.Crm.Core.Models.DataModels.Entities;

namespace ProjectChicago.Crm.Core.Repositories;

// Repository-level result of the Client detail query (CLIENT-030..032). Client is always
// non-null here - IClientRepository.GetDetailAsync itself returns null when no Client with the
// requested Id exists, so callers never need to null-check Client on this type. Project/Task
// bounds (how many rows land in each collection) are applied by the repository query, not here -
// this type only carries what the query already decided to return.
public sealed record ClientDetailQueryResult
{
    public required Client Client { get; init; }

    public required IReadOnlyList<Project> ActiveProjects { get; init; }

    public required IReadOnlyList<Project> HistoricalProjects { get; init; }

    public required IReadOnlyList<TaskItem> OpenTasks { get; init; }

    public required IReadOnlyList<TaskItem> RecentlyCompletedTasks { get; init; }
}

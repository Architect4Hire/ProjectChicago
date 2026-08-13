using ProjectChicago.Crm.Core.Models.DataModels.Entities;

namespace ProjectChicago.Crm.Core.Repositories;

// Repository-level result for Project detail queries (PROJECT-030; backend.md, data.md).
// Represents the composite data needed for a Project detail view: the Project aggregate,
// the owning Client, open TaskItems, completed TaskItems, and a count of recent audit events.
// Business layer translates this into ProjectDetailServiceModel for the API.
public sealed class ProjectDetailResult
{
    public required Project Project { get; init; }

    public required Client Client { get; init; }

    public required IReadOnlyList<TaskItem> OpenTasks { get; init; }

    public required IReadOnlyList<TaskItem> CompletedTasks { get; init; }

    // Count of recent audit events for this Project (used to indicate activity without querying
    // the Audit database directly - PROJECT-030 "audit history where authorized").
    // Recent is defined as events within the last 30 days (a heuristic; the exact window is
    // subject to observability/UX review).
    public required int RecentActivityCount { get; init; }
}

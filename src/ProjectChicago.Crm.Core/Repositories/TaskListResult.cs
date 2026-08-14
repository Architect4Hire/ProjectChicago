using ProjectChicago.Crm.Core.Models.DataModels.Entities;

namespace ProjectChicago.Crm.Core.Repositories;

// Repository-owned output of ITaskRepository.ListAsync (TASK-020, PERF-003). TotalCount is
// the count across the whole filtered result set (not just this page), which is what a caller needs
// to compute PagedResponse.TotalPages further up the stack - it is not itself a paging contract.
public sealed record TaskListResult
{
    public required IReadOnlyList<TaskItem> Items { get; init; }

    public required int TotalCount { get; init; }
}

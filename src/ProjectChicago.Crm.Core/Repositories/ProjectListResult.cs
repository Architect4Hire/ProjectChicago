using ProjectChicago.Crm.Core.Models.DataModels.Entities;

namespace ProjectChicago.Crm.Core.Repositories;

// Repository-owned output of IProjectRepository.ListAsync (PROJECT-020, PROJECT-023). TotalCount is
// the count across the whole filtered result set (not just this page), which is what a caller needs
// to compute PagedResponse.TotalPages further up the stack - it is not itself a paging contract.
public sealed record ProjectListResult
{
    public required IReadOnlyList<Project> Items { get; init; }

    public required int TotalCount { get; init; }
}

using Microsoft.EntityFrameworkCore;
using ProjectChicago.Crm.Core.Models.DataModels.Entities;
using ProjectChicago.Crm.Core.Persistence;

namespace ProjectChicago.Crm.Core.Repositories;

// SQL Server-backed IProjectRepository (PROJECT-001..002, PROJECT-020..023, DATA-001..005; backend.md,
// database.md). Works only against CrmDbContext, per the owning-service-database rule - no
// cross-service queries, no transactions, no business decisions.
public sealed class ProjectRepository : IProjectRepository
{
    private readonly CrmDbContext _dbContext;

    public ProjectRepository(CrmDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task InsertAsync(Project project, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(project);

        await _dbContext.Projects.AddAsync(project, cancellationToken).ConfigureAwait(false);
    }

    public Task<bool> ClientExistsAsync(Guid clientId, CancellationToken cancellationToken) =>
        _dbContext.Clients.AnyAsync(c => c.Id == clientId, cancellationToken);

    public async Task<ProjectListResult> ListAsync(ProjectListFilter filter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);

        if (filter.Page < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(filter), filter.Page, "Page must be 1 or greater.");
        }

        if (filter.PageSize < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(filter), filter.PageSize, "PageSize must be 1 or greater.");
        }

        // Use query composition pattern: when search is requested, join with Clients to enable
        // CLIENT NAME searching. Otherwise, query Projects directly (PERF-004/PROJECT-022).
        IQueryable<Project> query;

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search;
            query = from p in _dbContext.Projects.AsNoTracking()
                    join c in _dbContext.Clients.AsNoTracking() on p.ClientId equals c.Id
                    where p.Name.Contains(search) || (p.Description != null && p.Description.Contains(search)) || c.Name.Contains(search)
                    select p;
        }
        else
        {
            query = _dbContext.Projects.AsNoTracking();
        }

        // PROJECT-014/DATA-020: Exclude archived Projects from normal list results unless explicitly
        // requested. Archived Projects remain available in detail/audit views and can be included via
        // IncludeArchived filter option.
        if (!filter.IncludeArchived)
        {
            query = query.Where(p => p.Status != ProjectStatus.Archived);
        }

        // PROJECT-021 Client filter: when ClientId is not Guid.Empty, restrict to that Client's Projects.
        // Guid.Empty means "search across all authorized Clients" (PROJECT-020).
        if (filter.ClientId != Guid.Empty)
        {
            query = query.Where(p => p.ClientId == filter.ClientId);
        }

        if (filter.Status is { } status)
        {
            query = query.Where(p => p.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(filter.OwnerUserId))
        {
            query = query.Where(p => p.OwnerUserId == filter.OwnerUserId);
        }

        if (filter.Priority is { } priority)
        {
            query = query.Where(p => p.Priority == priority);
        }

        if (filter.StartDateUtc is { } startDate)
        {
            query = query.Where(p => p.StartDateUtc >= startDate);
        }

        if (filter.TargetCompletionDateUtc is { } targetDate)
        {
            query = query.Where(p => p.TargetCompletionDateUtc <= targetDate);
        }

        // Count before Skip/Take, against the filtered-but-unpaged query (PERF-003/PROJECT-023).
        var totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        var items = await ApplySort(query, filter.SortBy, filter.SortDirection)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new ProjectListResult
        {
            Items = items,
            TotalCount = totalCount,
        };
    }

    private static IQueryable<Project> ApplySort(
        IQueryable<Project> query,
        ProjectListSortField sortBy,
        ProjectListSortDirection sortDirection)
    {
        var ascending = sortDirection == ProjectListSortDirection.Ascending;

        IOrderedQueryable<Project> ordered = sortBy switch
        {
            ProjectListSortField.CreatedAtUtc => ascending
                ? query.OrderBy(p => p.CreatedAtUtc)
                : query.OrderByDescending(p => p.CreatedAtUtc),
            ProjectListSortField.LastModifiedAtUtc => ascending
                ? query.OrderBy(p => p.LastModifiedAtUtc)
                : query.OrderByDescending(p => p.LastModifiedAtUtc),
            ProjectListSortField.Status => ascending
                ? query.OrderBy(p => p.Status)
                : query.OrderByDescending(p => p.Status),
            ProjectListSortField.Priority => ascending
                ? query.OrderBy(p => p.Priority)
                : query.OrderByDescending(p => p.Priority),
            ProjectListSortField.TargetCompletionDateUtc => ascending
                ? query.OrderBy(p => p.TargetCompletionDateUtc)
                : query.OrderByDescending(p => p.TargetCompletionDateUtc),
            _ => ascending
                ? query.OrderBy(p => p.Name)
                : query.OrderByDescending(p => p.Name),
        };

        // Deterministic tie-breaker: Id is unique, so paging never skips or duplicates rows when
        // many Projects share the same primary sort value (PROJECT-023).
        return ascending ? ordered.ThenBy(p => p.Id) : ordered.ThenByDescending(p => p.Id);
    }

    public async Task<ProjectDetailResult?> GetDetailAsync(Guid projectId, CancellationToken cancellationToken)
    {
        if (projectId == Guid.Empty)
        {
            throw new ArgumentException("Project Id cannot be empty.", nameof(projectId));
        }

        // Fetch the Project by Id (PROJECT-030: project information).
        var project = await _dbContext.Projects
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == projectId, cancellationToken)
            .ConfigureAwait(false);

        if (project is null)
        {
            return null;
        }

        // Fetch the owning Client (PROJECT-030: Client).
        var client = await _dbContext.Clients
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == project.ClientId, cancellationToken)
            .ConfigureAwait(false);

        // A Project must have a Client (DATA-002). If the Client is missing, the database
        // integrity is compromised, but we still return null to the caller rather than throwing.
        if (client is null)
        {
            return null;
        }

        // Fetch open Tasks for this Project (PROJECT-030: open Tasks). A Task is considered
        // open when its status is not Completed or Cancelled (TASK-020).
        var openTasks = await _dbContext.Tasks
            .AsNoTracking()
            .Where(t => t.ProjectId == projectId
                && t.Status != TaskItemStatus.Completed
                && t.Status != TaskItemStatus.Cancelled)
            .OrderBy(t => t.DueDateUtc)
            .ThenBy(t => t.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Fetch completed Tasks for this Project (PROJECT-030: completed Tasks).
        var completedTasks = await _dbContext.Tasks
            .AsNoTracking()
            .Where(t => t.ProjectId == projectId && t.Status == TaskItemStatus.Completed)
            .OrderByDescending(t => t.CompletedAtUtc)
            .ThenByDescending(t => t.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Recent activity count (PROJECT-030: "Recent activity...audit history where authorized").
        // This count represents the number of audit events for this Project within the last 30 days.
        // The actual audit trail is managed by the Audit service and is not directly queryable here
        // (onion-boundaries.md: no cross-service DB access). For this microstep, the count is set to 0
        // and awaits audit service integration (PROJECT-031 integration).
        var recentActivityCount = 0;

        return new ProjectDetailResult
        {
            Project = project,
            Client = client,
            OpenTasks = openTasks,
            CompletedTasks = completedTasks,
            RecentActivityCount = recentActivityCount,
        };
    }

    public async Task<Project?> GetAsync(Guid projectId, CancellationToken cancellationToken)
    {
        if (projectId == Guid.Empty)
        {
            throw new ArgumentException("Project Id cannot be empty.", nameof(projectId));
        }

        return await _dbContext.Projects
            .FirstOrDefaultAsync(p => p.Id == projectId, cancellationToken)
            .ConfigureAwait(false);
    }
}

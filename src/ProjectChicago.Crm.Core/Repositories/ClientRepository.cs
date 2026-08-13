using Microsoft.EntityFrameworkCore;
using ProjectChicago.Crm.Core.Models.DataModels.Entities;
using ProjectChicago.Crm.Core.Persistence;

namespace ProjectChicago.Crm.Core.Repositories;

// SQL Server-backed IClientRepository (CLIENT-001/CLIENT-004, DATA-004/DATA-005; backend.md,
// database.md). Works only against CrmDbContext, per the owning-service-database rule - no
// cross-service queries, no transactions, no duplicate-detection policy.
public sealed class ClientRepository : IClientRepository
{
    // CLIENT-030..032/PERF-003: the detail view is one record's consolidated summary, not a
    // paginated collection, so there is no caller-supplied page size to bound it with. These caps
    // are a narrow, reversible assumption (CLAUDE.md Usage #5) recording only that some bound
    // exists - revisit if the product ever needs every Project/Task on a very large Client visible
    // here rather than through the dedicated Project/Task list views these bounds intentionally
    // leave for a later microstep.
    private const int MaxProjectsPerSection = 50;
    private const int MaxOpenTasks = 100;
    private const int MaxRecentlyCompletedTasks = 10;

    // PROJECT-010/PROJECT-014: a Project's current status doubles as its archived state, so
    // "active" here means "still open work" and "historical" means everything else, including
    // Archived. No requirement enumerates this split explicitly; it is the narrowest reading of
    // CLIENT-030's "Active Projects" / "Historical Projects" bullets against PROJECT-010's status
    // set (CLAUDE.md Usage #5).
    private static readonly ProjectStatus[] ActiveProjectStatuses =
    [
        ProjectStatus.Planned,
        ProjectStatus.Active,
        ProjectStatus.OnHold,
    ];

    private readonly CrmDbContext _dbContext;

    public ClientRepository(CrmDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task InsertAsync(Client client, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);

        await _dbContext.Clients.AddAsync(client, cancellationToken).ConfigureAwait(false);
    }

    public Task<Client?> GetForUpdateAsync(Guid clientId, CancellationToken cancellationToken) =>
        _dbContext.Clients.SingleOrDefaultAsync(c => c.Id == clientId, cancellationToken);

    public async Task<IReadOnlyList<Client>> FindDuplicateCandidatesAsync(
        string? normalizedName,
        string? normalizedEmail,
        string? normalizedPhone,
        CancellationToken cancellationToken)
    {
        var hasName = !string.IsNullOrWhiteSpace(normalizedName);
        var hasEmail = !string.IsNullOrWhiteSpace(normalizedEmail);
        var hasPhone = !string.IsNullOrWhiteSpace(normalizedPhone);

        if (!hasName && !hasEmail && !hasPhone)
        {
            return [];
        }

        return await _dbContext.Clients
            .Where(c =>
                (hasName && c.Name == normalizedName) ||
                (hasEmail && c.PrimaryEmail == normalizedEmail) ||
                (hasPhone && c.PrimaryPhone == normalizedPhone))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<ClientListResult> ListAsync(ClientListFilter filter, CancellationToken cancellationToken)
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

        IQueryable<Client> query = _dbContext.Clients.AsNoTracking();

        // CLIENT-013: "Archived Clients shall not appear in normal active Client lists unless
        // explicitly requested." A caller explicitly requests Archived Clients either by filtering
        // on that exact LifecycleStatus or by asking for IsActive == false; any other combination
        // gets the default exclusion below.
        var archivedExplicitlyRequested =
            filter.LifecycleStatus == ClientLifecycleStatus.Archived || filter.IsActive == false;

        if (!archivedExplicitlyRequested)
        {
            query = query.Where(c => c.LifecycleStatus != ClientLifecycleStatus.Archived);
        }

        if (filter.LifecycleStatus is { } lifecycleStatus)
        {
            query = query.Where(c => c.LifecycleStatus == lifecycleStatus);
        }

        if (!string.IsNullOrWhiteSpace(filter.OwnerUserId))
        {
            query = query.Where(c => c.OwnerUserId == filter.OwnerUserId);
        }

        if (filter.IsActive is { } isActive)
        {
            query = isActive
                ? query.Where(c => c.LifecycleStatus != ClientLifecycleStatus.Archived)
                : query.Where(c => c.LifecycleStatus == ClientLifecycleStatus.Archived);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search;
            query = query.Where(c =>
                c.Name.Contains(search) ||
                (c.PrimaryContactName != null && c.PrimaryContactName.Contains(search)) ||
                (c.PrimaryEmail != null && c.PrimaryEmail.Contains(search)) ||
                (c.PrimaryPhone != null && c.PrimaryPhone.Contains(search)));
        }

        // Count before Skip/Take, against the filtered-but-unpaged query (PERF-003/CLIENT-024).
        var totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        var items = await ApplySort(query, filter.SortBy, filter.SortDirection)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new ClientListResult
        {
            Items = items,
            TotalCount = totalCount,
        };
    }

    private static IQueryable<Client> ApplySort(
        IQueryable<Client> query,
        ClientListSortField sortBy,
        ClientListSortDirection sortDirection)
    {
        var ascending = sortDirection == ClientListSortDirection.Ascending;

        IOrderedQueryable<Client> ordered = sortBy switch
        {
            ClientListSortField.CreatedAtUtc => ascending
                ? query.OrderBy(c => c.CreatedAtUtc)
                : query.OrderByDescending(c => c.CreatedAtUtc),
            ClientListSortField.LastModifiedAtUtc => ascending
                ? query.OrderBy(c => c.LastModifiedAtUtc)
                : query.OrderByDescending(c => c.LastModifiedAtUtc),
            ClientListSortField.LifecycleStatus => ascending
                ? query.OrderBy(c => c.LifecycleStatus)
                : query.OrderByDescending(c => c.LifecycleStatus),
            _ => ascending
                ? query.OrderBy(c => c.Name)
                : query.OrderByDescending(c => c.Name),
        };

        // Deterministic tie-breaker: Id is unique, so paging never skips or duplicates rows when
        // many Clients share the same primary sort value (CLIENT-023/CLIENT-024).
        return ascending ? ordered.ThenBy(c => c.Id) : ordered.ThenByDescending(c => c.Id);
    }

    public async Task<ClientDetailQueryResult?> GetDetailAsync(Guid clientId, CancellationToken cancellationToken)
    {
        var client = await _dbContext.Clients.AsNoTracking()
            .SingleOrDefaultAsync(c => c.Id == clientId, cancellationToken)
            .ConfigureAwait(false);

        if (client is null)
        {
            return null;
        }

        var activeProjects = await _dbContext.Projects.AsNoTracking()
            .Where(p => p.ClientId == clientId && ActiveProjectStatuses.Contains(p.Status))
            .OrderByDescending(p => p.LastModifiedAtUtc)
            .ThenBy(p => p.Id)
            .Take(MaxProjectsPerSection)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var historicalProjects = await _dbContext.Projects.AsNoTracking()
            .Where(p => p.ClientId == clientId && !ActiveProjectStatuses.Contains(p.Status))
            .OrderByDescending(p => p.LastModifiedAtUtc)
            .ThenBy(p => p.Id)
            .Take(MaxProjectsPerSection)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Joined directly against Projects rather than fetching this Client's Project Ids first,
        // so "which Tasks belong to this Client" stays one indexed SQL query per section
        // (IX_Projects_ClientId, IX_Tasks_ProjectId) instead of an extra round trip (PERF-004).
        var openTasks = await (
                from t in _dbContext.Tasks.AsNoTracking()
                join p in _dbContext.Projects.AsNoTracking() on t.ProjectId equals p.Id
                where p.ClientId == clientId
                    && t.Status != TaskItemStatus.Completed
                    && t.Status != TaskItemStatus.Cancelled
                orderby t.DueDateUtc == null, t.DueDateUtc, t.Id
                select t)
            .Take(MaxOpenTasks)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var recentlyCompletedTasks = await (
                from t in _dbContext.Tasks.AsNoTracking()
                join p in _dbContext.Projects.AsNoTracking() on t.ProjectId equals p.Id
                where p.ClientId == clientId && t.Status == TaskItemStatus.Completed
                orderby t.CompletedAtUtc descending, t.Id
                select t)
            .Take(MaxRecentlyCompletedTasks)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new ClientDetailQueryResult
        {
            Client = client,
            ActiveProjects = activeProjects,
            HistoricalProjects = historicalProjects,
            OpenTasks = openTasks,
            RecentlyCompletedTasks = recentlyCompletedTasks,
        };
    }

    public async Task<bool> HasActiveProjectsAsync(Guid clientId, CancellationToken cancellationToken)
    {
        return await _dbContext.Projects
            .AsNoTracking()
            .AnyAsync(p => p.ClientId == clientId && ActiveProjectStatuses.Contains(p.Status), cancellationToken)
            .ConfigureAwait(false);
    }
}

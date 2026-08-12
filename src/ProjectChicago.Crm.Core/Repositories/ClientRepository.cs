using Microsoft.EntityFrameworkCore;
using ProjectChicago.Crm.Core.Models.DataModels.Entities;
using ProjectChicago.Crm.Core.Persistence;

namespace ProjectChicago.Crm.Core.Repositories;

// SQL Server-backed IClientRepository (CLIENT-001/CLIENT-004, DATA-004/DATA-005; backend.md,
// database.md). Works only against CrmDbContext, per the owning-service-database rule - no
// cross-service queries, no transactions, no duplicate-detection policy.
public sealed class ClientRepository : IClientRepository
{
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
}

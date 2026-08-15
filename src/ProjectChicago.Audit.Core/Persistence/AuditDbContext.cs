using Microsoft.EntityFrameworkCore;

namespace ProjectChicago.Audit.Core.Persistence;

/// <summary>
/// Audit Service database context. Maintains append-only audit entries and inbox for event processing idempotency (ADR-0016).
/// </summary>
public class AuditDbContext(DbContextOptions<AuditDbContext> options) : DbContext(options)
{
    // DbSets for audit entries and inbox will be added as implementation proceeds.
    // Audit is append-only: no UPDATE/DELETE paths for business records.
}

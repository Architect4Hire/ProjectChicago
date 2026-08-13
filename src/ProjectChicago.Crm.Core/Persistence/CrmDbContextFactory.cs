using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ProjectChicago.Crm.Core.Persistence;

// dotnet ef design-time factory. CrmDbContext's real connection string only exists once Aspire's
// AppHost injects the "CrmDb" resource into the API host/Functions host at run time
// (AddSqlServerDbContext in Program.cs) - there is no running orchestrator for `dotnet ef` to read
// that from. This factory gives the EF tooling a SQL Server-shaped connection string good enough to
// build the model and scaffold migrations; it is never invoked outside `dotnet ef` and has no effect
// on the app's runtime configuration or credentials (database.md: "Prefer Aspire-injected...
// connection configuration instead of literal connection strings" governs runtime wiring, not this
// design-time-only tool entry point).
public sealed class CrmDbContextFactory : IDesignTimeDbContextFactory<CrmDbContext>
{
    public CrmDbContext CreateDbContext(string[] args)
    {
        return CreateDbContext();
    }

    public CrmDbContext CreateDbContext()
    {
        var optionsBuilder = new DbContextOptionsBuilder<CrmDbContext>()
            .UseSqlServer("Server=.;Database=CrmDb;Trusted_Connection=True;TrustServerCertificate=True;");

        return new CrmDbContext(optionsBuilder.Options);
    }
}

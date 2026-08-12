using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProjectChicago.Crm.Core.Persistence;
using Xunit;

namespace ProjectChicago.Crm.Api.Tests;

// Host-start composition test (OTEL-001..006/DATA-030..034/OPS-001): proves the real Crm Program.cs
// composition root wires ServiceDefaults plus the Aspire SQL Server EF Core integration so
// CrmDbContext resolves from DI against the "CrmDb" connection name. Only the connection string
// needs to exist in configuration for AddSqlServerDbContext to register successfully; resolving the
// pooled DbContext builds its options/model but never opens a connection, so no SQL Server instance
// is required to prove this seam.
public class HostCompositionTests
{
    private const string CrmDbConnectionStringEnvironmentVariable = "ConnectionStrings__CrmDb";

    [Fact]
    public void CrmDbContext_ResolvesFromHostServiceProvider()
    {
        Environment.SetEnvironmentVariable(
            CrmDbConnectionStringEnvironmentVariable,
            "Server=localhost;Database=CrmDbHostCompositionTests;TrustServerCertificate=True;");

        try
        {
            using var factory = new WebApplicationFactory<Program>();
            using var scope = factory.Services.CreateScope();

            var context = scope.ServiceProvider.GetRequiredService<CrmDbContext>();

            Assert.IsType<CrmDbContext>(context);
        }
        finally
        {
            Environment.SetEnvironmentVariable(CrmDbConnectionStringEnvironmentVariable, null);
        }
    }
}

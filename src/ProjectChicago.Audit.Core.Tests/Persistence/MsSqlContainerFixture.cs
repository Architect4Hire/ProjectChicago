using Testcontainers.MsSql;
using Xunit;

namespace ProjectChicago.Audit.Core.Tests.Persistence;

/// <summary>
/// Shared real SQL Server container for Audit Service integration tests (database.md: "EF InMemory
/// is not proof that SQL Server persistence works" - rowversion-based optimistic concurrency and
/// unique constraint idempotency in particular have no meaningful InMemory equivalent). One container
/// is started per test class via IClassFixture and reused by every [Fact] in that class; each test
/// gets its own database inside it for isolation (see AuditDataTests).
/// </summary>
public sealed class MsSqlContainerFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container =
        new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    public string ConnectionString => _container.GetConnectionString();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

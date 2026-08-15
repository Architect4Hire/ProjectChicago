using Testcontainers.MsSql;
using Xunit;

namespace ProjectChicago.Identity.Core.Tests.Persistence;

// Shared real SQL Server container for IdentityOutboxStore integration tests (database.md: "EF InMemory
// is not proof that SQL Server persistence works" - RowVersion-based optimistic concurrency in
// particular has no meaningful InMemory equivalent). One container is started per test class via
// IClassFixture and reused by every [Fact] in that class; each test gets its own database inside it
// for isolation (see IdentityOutboxStoreTests).
public sealed class MsSqlContainerFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container =
        new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    public string ConnectionString => _container.GetConnectionString();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace MultiInherit.Tests.Integration;

/// <summary>
/// xUnit collection fixture that starts a PostgreSQL container once per test collection.
/// The schema is created via EnsureCreated on startup.
/// </summary>
public sealed class PostgreSqlFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    /// <summary>Connection string to the running PostgreSQL instance.</summary>
    public string ConnectionString { get; private set; } = string.Empty;

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();
        ConnectionString = _container.GetConnectionString();

        // Create schema once for the whole collection
        await using var ctx = CreateContext();
        await ctx.Database.EnsureCreatedAsync();
    }

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();

    /// <summary>Creates a fresh DbContext backed by the running container.</summary>
    public TestDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder()
            .UseNpgsql(ConnectionString)
            .Options;

        return new TestDbContext(options);
    }
}

/// <summary>
/// xUnit collection definition — shares one PostgreSQL container across
/// all integration test classes.
/// </summary>
[CollectionDefinition("Integration")]
public class IntegrationCollection : ICollectionFixture<PostgreSqlFixture>
{
}

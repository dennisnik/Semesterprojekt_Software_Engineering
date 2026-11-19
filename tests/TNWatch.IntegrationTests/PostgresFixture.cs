using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using TNWatch.Persistence;

namespace TNWatch.IntegrationTests;

/// <summary>One PostgreSQL 16 container per test collection; schema created via the real EF migrations.</summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("tnwatch")
        .WithUsername("tnwatch")
        .WithPassword("integration-test")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await using var provider = CreateServices();
        await provider.MigrateTnWatchDatabaseAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    public ServiceProvider CreateServices(Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection().AddLogging();
        services.AddTnWatchPersistence(ConnectionString);
        configure?.Invoke(services);
        return services.BuildServiceProvider();
    }

    /// <summary>Removes all rows between tests.</summary>
    public async Task ResetAsync()
    {
        await using var provider = CreateServices();
        var factory = provider.GetRequiredService<IDbContextFactory<TnWatchDbContext>>();
        await using var db = await factory.CreateDbContextAsync();
        await db.Database.ExecuteSqlRawAsync(
            "TRUNCATE telemetry.measurements, alerting.status_changes, core.battery_events, core.traffic_nodes CASCADE");
    }
}

[CollectionDefinition(Name)]
public sealed class PostgresGroup : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres";
}

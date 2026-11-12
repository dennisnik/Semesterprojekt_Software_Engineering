using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TNWatch.Domain.Fleet;

namespace TNWatch.Persistence;

public static class PersistenceServiceCollectionExtensions
{
    /// <summary>Registers DbContext (factory + scoped), repository and query services.</summary>
    public static IServiceCollection AddTnWatchPersistence(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddDbContextFactory<TnWatchDbContext>(o => o
            .UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure(5))
            .UseSnakeCaseNamingConvention());
        services.AddScoped<ITelemetryRepository, TelemetryRepository>();
        services.AddSingleton<IFleetQueries, FleetQueries>();
        return services;
    }

    /// <summary>Applies pending EF Core migrations (ADR-03 mitigation: migrations on startup).</summary>
    public static async Task MigrateTnWatchDatabaseAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        var factory = services.GetRequiredService<IDbContextFactory<TnWatchDbContext>>();
        await using var db = await factory.CreateDbContextAsync(ct);
        await db.Database.MigrateAsync(ct);
    }
}

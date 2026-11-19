using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TNWatch.Persistence;

/// <summary>Used only by <c>dotnet ef</c> to create migrations. No real connection is opened.</summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<TnWatchDbContext>
{
    public TnWatchDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<TnWatchDbContext>()
            .UseNpgsql("Host=localhost;Database=tnwatch;Username=design;Password=design")
            .UseSnakeCaseNamingConvention()
            .Options);
}

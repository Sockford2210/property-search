using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PropertySearch.Infrastructure;

/// <summary>
/// Lets the EF Core CLI (<c>dotnet ef migrations</c> / <c>database update</c>)
/// create a <see cref="PropertyDbContext"/> without a running application host.
/// The connection string comes from the <c>PROPERTYSEARCH_DB</c> environment
/// variable, falling back to the local Docker Compose database on port 5433.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<PropertyDbContext>
{
    private const string DefaultConnectionString =
        "Host=localhost;Port=5433;Database=propertysearch;Username=propertysearch;Password=propertysearch";

    public PropertyDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("PROPERTYSEARCH_DB") ?? DefaultConnectionString;

        var options = new DbContextOptionsBuilder<PropertyDbContext>()
            .UsePropertySearchNpgsql(connectionString)
            .Options;

        return new PropertyDbContext(options);
    }
}

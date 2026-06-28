using Microsoft.EntityFrameworkCore;

namespace PropertySearch.Infrastructure;

public static class PropertyDbContextOptionsExtensions
{
    /// <summary>
    /// Applies the standard provider configuration for the property database:
    /// Npgsql against the given connection string with snake_case naming. Shared
    /// by the design-time factory and the integration tests so the generated
    /// schema is identical everywhere.
    /// </summary>
    public static DbContextOptionsBuilder<TContext> UsePropertySearchNpgsql<TContext>(
        this DbContextOptionsBuilder<TContext> builder, string connectionString)
        where TContext : DbContext
    {
        builder
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention();
        return builder;
    }
}

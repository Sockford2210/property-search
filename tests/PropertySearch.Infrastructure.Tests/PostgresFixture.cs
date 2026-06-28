using Microsoft.EntityFrameworkCore;
using Npgsql;
using PropertySearch.Infrastructure;
using Respawn;
using Testcontainers.PostgreSql;
using Xunit;

namespace PropertySearch.Infrastructure.Tests;

/// <summary>
/// Spins up a throwaway PostGIS container for the whole test collection, applies
/// the EF Core migrations once, and resets data between tests via Respawn.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgis/postgis:17-3.5")
        .WithDatabase("propertysearch")
        .WithUsername("propertysearch")
        .WithPassword("propertysearch")
        .Build();

    private Respawner _respawner = null!;
    private NpgsqlConnection _connection = null!;

    public string ConnectionString => _container.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();

        await using (var context = CreateContext())
        {
            await context.Database.MigrateAsync();
        }

        _connection = new NpgsqlConnection(ConnectionString);
        await _connection.OpenAsync();

        _respawner = await Respawner.CreateAsync(_connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = ["public"],
            TablesToIgnore =
            [
                new Respawn.Graph.Table("__EFMigrationsHistory"),
                new Respawn.Graph.Table("spatial_ref_sys"), // PostGIS reference data
            ],
        });
    }

    public PropertyDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<PropertyDbContext>()
            .UsePropertySearchNpgsql(ConnectionString)
            .Options;

        return new PropertyDbContext(options);
    }

    /// <summary>Truncates all data, leaving the schema and migration history intact.</summary>
    public Task ResetAsync() => _respawner.ResetAsync(_connection);

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
        await _container.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres";
}

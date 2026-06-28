using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PropertySearch.Domain.Enums;
using PropertySearch.Infrastructure.Stations;
using Xunit;

namespace PropertySearch.Infrastructure.Tests;

[Collection(PostgresCollection.Name)]
public sealed class StationImportTests(PostgresFixture fixture) : IAsyncLifetime
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync() => new(fixture.ResetAsync());

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private async Task<StationImportResult> RunImportAsync()
    {
        await using var context = fixture.CreateContext();
        var service = new StationImportService(context, new EmbeddedStationDataSource());
        return await service.ImportAsync(Ct);
    }

    [Fact]
    public async Task Import_populates_more_than_300_stations()
    {
        var result = await RunImportAsync();

        result.Inserted.Should().BeGreaterThan(300);

        await using var context = fixture.CreateContext();
        (await context.Stations.CountAsync(Ct)).Should().BeGreaterThan(300);
    }

    [Fact]
    public async Task Import_is_idempotent_on_a_second_run()
    {
        await RunImportAsync();

        int countAfterFirst;
        await using (var context = fixture.CreateContext())
        {
            countAfterFirst = await context.Stations.CountAsync(Ct);
        }

        var second = await RunImportAsync();

        second.Inserted.Should().Be(0);
        second.Updated.Should().Be(0);
        second.Unchanged.Should().Be(countAfterFirst);

        await using (var context = fixture.CreateContext())
        {
            (await context.Stations.CountAsync(Ct)).Should().Be(countAfterFirst);
        }
    }

    [Fact]
    public async Task Multi_mode_interchange_collapses_to_a_single_row_with_primary_mode()
    {
        await RunImportAsync();

        await using var context = fixture.CreateContext();
        var stratford = await context.Stations.Where(s => s.Name == "Stratford").ToListAsync(Ct);

        stratford.Should().ContainSingle();
        stratford[0].Mode.Should().Be(TransportMode.Underground);
    }

    [Fact]
    public async Task Station_codes_are_unique()
    {
        await RunImportAsync();

        await using var context = fixture.CreateContext();
        var total = await context.Stations.CountAsync(Ct);
        var distinct = await context.Stations.Select(s => s.StationCode).Distinct().CountAsync(Ct);

        distinct.Should().Be(total);
    }

    [Fact]
    public async Task Imported_station_has_a_generated_location()
    {
        await RunImportAsync();

        long id;
        await using (var context = fixture.CreateContext())
        {
            id = (await context.Stations.FirstAsync(s => s.Name == "King's Cross St. Pancras", Ct)).Id;
        }

        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(
            "SELECT ST_Y(location), ST_X(location) FROM stations WHERE id = @id", connection);
        command.Parameters.AddWithValue("id", id);
        await using var reader = await command.ExecuteReaderAsync(Ct);

        (await reader.ReadAsync(Ct)).Should().BeTrue();
        reader.GetDouble(0).Should().BeApproximately(51.53, 0.05); // latitude
        reader.GetDouble(1).Should().BeApproximately(-0.123, 0.05); // longitude
    }
}

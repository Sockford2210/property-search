using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PropertySearch.Domain;
using PropertySearch.Domain.Enums;
using PropertySearch.Infrastructure.Spatial;
using PropertySearch.Infrastructure.Stations;
using Xunit;

namespace PropertySearch.Infrastructure.Tests;

[Collection(PostgresCollection.Name)]
public sealed class NearestStationFinderTests(PostgresFixture fixture) : IAsyncLifetime
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync() => new(fixture.ResetAsync());

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    // Real coordinates for a handful of central-London stations.
    private static readonly (string Code, string Name, double Lat, double Lon)[] SeedStations =
    [
        ("KGX", "King's Cross St. Pancras", 51.5308, -0.1238),
        ("EUS", "Euston", 51.5282, -0.1337),
        ("AGL", "Angel", 51.5322, -0.1058),
        ("WLO", "Waterloo", 51.5031, -0.1132),
        ("OXC", "Oxford Circus", 51.5152, -0.1419),
    ];

    private async Task SeedAsync()
    {
        await using var context = fixture.CreateContext();
        context.Stations.AddRange(SeedStations.Select(s => new Station
        {
            StationCode = s.Code,
            Name = s.Name,
            Mode = TransportMode.Underground,
            Latitude = s.Lat,
            Longitude = s.Lon,
        }));
        await context.SaveChangesAsync(Ct);
    }

    [Fact]
    public async Task Returns_the_nearest_station_to_a_coordinate()
    {
        await SeedAsync();

        await using var context = fixture.CreateContext();
        var finder = new NearestStationFinder(context);

        // A point essentially on top of King's Cross.
        var result = await finder.FindNearestAsync(51.5308, -0.1238, Ct);

        result.Should().NotBeNull();
        result!.Station.Name.Should().Be("King's Cross St. Pancras");
    }

    [Fact]
    public async Task Reports_distance_in_metres_within_tolerance()
    {
        await SeedAsync();

        await using var context = fixture.CreateContext();
        var finder = new NearestStationFinder(context);

        // ~700m due south of King's Cross — closest station is still King's Cross.
        // Expected great-circle distance from 51.5308 to 51.5245 at this longitude
        // is ~700m; assert station identity hard and the metres loosely.
        var result = await finder.FindNearestAsync(51.5245, -0.1238, Ct);

        result.Should().NotBeNull();
        result!.Station.Name.Should().Be("King's Cross St. Pancras");
        result.DistanceMeters.Should().BeApproximately(700, 50);
    }

    [Fact]
    public async Task Returns_the_truly_closer_of_two_nearby_stations()
    {
        await SeedAsync();

        await using var context = fixture.CreateContext();
        var finder = new NearestStationFinder(context);

        // A point between King's Cross and Angel, closer to Angel. Confirms the
        // finder selects by measured distance and not, say, insertion order.
        var result = await finder.FindNearestAsync(51.5315, -0.1100, Ct);

        result.Should().NotBeNull();
        result!.Station.Name.Should().Be("Angel");
    }

    [Fact]
    public async Task Returns_null_when_there_are_no_stations()
    {
        await using var context = fixture.CreateContext();
        var finder = new NearestStationFinder(context);

        var result = await finder.FindNearestAsync(51.5308, -0.1238, Ct);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Works_against_the_full_imported_dataset()
    {
        await using (var context = fixture.CreateContext())
        {
            await new StationImportService(context, new EmbeddedStationDataSource()).ImportAsync(Ct);
        }

        await using var queryContext = fixture.CreateContext();
        var finder = new NearestStationFinder(queryContext);

        // A query right at King's Cross should find a station within a few hundred
        // metres. No brittle name assertion — this proves the finder runs at the
        // full ~300-station scale and returns a sensible result.
        var result = await finder.FindNearestAsync(51.5308, -0.1238, Ct);

        result.Should().NotBeNull();
        result!.DistanceMeters.Should().BeLessThan(500);
    }
}

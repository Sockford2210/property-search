using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PropertySearch.Domain;
using PropertySearch.Domain.Enums;
using Xunit;

namespace PropertySearch.Infrastructure.Tests;

[Collection(PostgresCollection.Name)]
public sealed class SchemaTests(PostgresFixture fixture) : IAsyncLifetime
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync() => new(fixture.ResetAsync());

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Can_insert_and_retrieve_a_property()
    {
        long id;
        await using (var context = fixture.CreateContext())
        {
            var property = new Property
            {
                DisplayAddress = "1 Test Street, London",
                RentPcm = 1995.00m,
                Bedrooms = 2,
                Bathrooms = 1,
            };
            context.Properties.Add(property);
            await context.SaveChangesAsync(Ct);
            id = property.Id;
        }

        await using (var context = fixture.CreateContext())
        {
            var loaded = await context.Properties.SingleAsync(p => p.Id == id, Ct);
            loaded.DisplayAddress.Should().Be("1 Test Street, London");
            loaded.RentPcm.Should().Be(1995.00m);
            loaded.Bedrooms.Should().Be(2);
            loaded.CreatedAt.Should().NotBe(default);
            loaded.UpdatedAt.Should().NotBe(default);
        }
    }

    [Fact]
    public async Task Can_insert_and_retrieve_a_station_with_generated_location()
    {
        long id;
        await using (var context = fixture.CreateContext())
        {
            var station = new Station
            {
                Name = "King's Cross St. Pancras",
                Mode = TransportMode.Underground,
                Latitude = 51.5308,
                Longitude = -0.1238,
            };
            context.Stations.Add(station);
            await context.SaveChangesAsync(Ct);
            id = station.Id;
        }

        await using (var context = fixture.CreateContext())
        {
            var loaded = await context.Stations.SingleAsync(s => s.Id == id, Ct);
            loaded.Name.Should().Be("King's Cross St. Pancras");
            loaded.Mode.Should().Be(TransportMode.Underground);
        }

        // The PostGIS geometry column is generated from longitude/latitude.
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(
            "SELECT ST_X(location), ST_Y(location) FROM stations WHERE id = @id", connection);
        command.Parameters.AddWithValue("id", id);
        await using var reader = await command.ExecuteReaderAsync(Ct);
        (await reader.ReadAsync(Ct)).Should().BeTrue();
        reader.GetDouble(0).Should().BeApproximately(-0.1238, 1e-9); // X = longitude
        reader.GetDouble(1).Should().BeApproximately(51.5308, 1e-9); // Y = latitude
    }

    [Fact]
    public async Task Station_mode_is_persisted_as_a_string()
    {
        await using (var context = fixture.CreateContext())
        {
            context.Stations.Add(new Station
            {
                Name = "Canary Wharf",
                Mode = TransportMode.ElizabethLine,
                Latitude = 51.5054,
                Longitude = -0.0204,
            });
            await context.SaveChangesAsync(Ct);
        }

        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand("SELECT mode FROM stations LIMIT 1", connection);
        var mode = (string?)await command.ExecuteScalarAsync(Ct);
        mode.Should().Be("ElizabethLine");
    }

    [Fact]
    public async Task Can_create_a_listing_and_navigate_to_its_source_and_property()
    {
        long listingId;
        await using (var context = fixture.CreateContext())
        {
            var source = new Source { Code = "rightmove", Name = "Rightmove", Enabled = true };
            var property = new Property { DisplayAddress = "5 Canonical Road, London" };
            var listing = new Listing
            {
                Source = source,
                Property = property,
                ExternalId = "RM-12345",
                Url = "https://www.rightmove.co.uk/properties/12345",
                DisplayAddress = "5 Canonical Road, London",
                RentPcm = 2100.00m,
                Bedrooms = 2,
                Bathrooms = 2,
                FirstSeenAt = DateTime.UtcNow,
                LastSeenAt = DateTime.UtcNow,
            };
            context.Listings.Add(listing);
            await context.SaveChangesAsync(Ct);
            listingId = listing.Id;
        }

        await using (var context = fixture.CreateContext())
        {
            var loaded = await context.Listings
                .Include(l => l.Source)
                .Include(l => l.Property)
                .SingleAsync(l => l.Id == listingId, Ct);

            loaded.Source.Code.Should().Be("rightmove");
            loaded.Property.Should().NotBeNull();
            loaded.Property!.DisplayAddress.Should().Be("5 Canonical Road, London");
            loaded.PropertyId.Should().Be(loaded.Property.Id);
            loaded.Status.Should().Be(ListingStatus.Active);
        }
    }

    [Fact]
    public async Task A_listing_without_a_property_is_allowed()
    {
        await using var context = fixture.CreateContext();
        var source = new Source { Code = "rightmove", Name = "Rightmove", Enabled = true };
        context.Listings.Add(new Listing
        {
            Source = source,
            ExternalId = "RM-99999",
            Url = "https://www.rightmove.co.uk/properties/99999",
            DisplayAddress = "Unnormalised Address",
            RentPcm = 1500m,
            Bedrooms = 1,
            FirstSeenAt = DateTime.UtcNow,
            LastSeenAt = DateTime.UtcNow,
        });

        var act = async () => await context.SaveChangesAsync(Ct);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Duplicate_external_id_within_a_source_is_rejected()
    {
        await using var context = fixture.CreateContext();
        var source = new Source { Code = "rightmove", Name = "Rightmove", Enabled = true };
        context.Sources.Add(source);
        await context.SaveChangesAsync(Ct);

        Listing MakeListing() => new()
        {
            SourceId = source.Id,
            ExternalId = "RM-DUPLICATE",
            Url = "https://www.rightmove.co.uk/properties/1",
            DisplayAddress = "Somewhere",
            RentPcm = 1000m,
            Bedrooms = 1,
            FirstSeenAt = DateTime.UtcNow,
            LastSeenAt = DateTime.UtcNow,
        };

        context.Listings.Add(MakeListing());
        await context.SaveChangesAsync(Ct);

        context.Listings.Add(MakeListing());
        var act = async () => await context.SaveChangesAsync(Ct);
        await act.Should().ThrowAsync<DbUpdateException>();
    }
}

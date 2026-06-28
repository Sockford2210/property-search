using FluentAssertions;
using PropertySearch.Infrastructure.Sources.Rightmove;
using Xunit;

namespace PropertySearch.Infrastructure.Tests;

public sealed class RightmoveListingParserTests
{
    private readonly RightmoveListingParser _parser = new();

    [Fact]
    public void Parse_returns_fully_populated_listing_from_monthly_fixture()
    {
        var html = FixtureLoader.Load("rightmove-listing-99001.html");

        var result = _parser.Parse(html);

        result.ExternalId.Should().Be("99001");
        result.Url.Should().Be(new Uri("https://www.rightmove.co.uk/properties/99001"));
        result.DisplayAddress.Should().Be("Flat 1, Test Street, London, W1");
        result.RentPcm.Should().Be(2000m);
        result.Bedrooms.Should().Be(2);
        result.Bathrooms.Should().Be(1);
        result.Latitude.Should().BeApproximately(51.5074, 0.0001);
        result.Longitude.Should().BeApproximately(-0.1278, 0.0001);
        result.Description.Should().Be("A lovely two-bedroom flat in central London.");
    }

    [Fact]
    public void Parse_converts_weekly_rent_to_monthly()
    {
        var html = FixtureLoader.Load("rightmove-listing-weekly.html");

        var result = _parser.Parse(html);

        // 500 * 52 / 12 = 2166.666... -> rounds to 2167
        result.RentPcm.Should().Be(2167m);
    }

    [Fact]
    public void Parse_maps_studio_to_zero_bedrooms()
    {
        var html = FixtureLoader.Load("rightmove-listing-studio.html");

        var result = _parser.Parse(html);

        result.Bedrooms.Should().Be(0);
    }

    [Fact]
    public void Parse_succeeds_with_null_optional_fields_when_absent()
    {
        var html = FixtureLoader.Load("rightmove-listing-no-optionals.html");

        var result = _parser.Parse(html);

        result.Bathrooms.Should().BeNull();
        result.Latitude.Should().BeNull();
        result.Longitude.Should().BeNull();
        result.Description.Should().BeNull();
        result.ExternalId.Should().Be("99004");
        result.RentPcm.Should().Be(1800m);
    }

    [Fact]
    public void Parse_throws_ListingParseException_for_poa_rent()
    {
        var html = FixtureLoader.Load("rightmove-listing-poa.html");

        var act = () => _parser.Parse(html);

        act.Should().Throw<ListingParseException>();
    }

    [Fact]
    public void Parse_throws_ListingParseException_when_display_address_is_missing()
    {
        var html = FixtureLoader.Load("rightmove-listing-no-address.html");

        var act = () => _parser.Parse(html);

        act.Should().Throw<ListingParseException>();
    }

    [Fact]
    public void Parse_throws_ListingParseException_when_page_model_is_absent()
    {
        var html = FixtureLoader.Load("rightmove-listing-malformed.html");

        var act = () => _parser.Parse(html);

        act.Should().Throw<ListingParseException>();
    }
}

using FluentAssertions;
using PropertySearch.Infrastructure.Sources.Rightmove;
using Xunit;

namespace PropertySearch.Infrastructure.Tests;

public sealed class RightmoveSearchParserTests
{
    private readonly RightmoveSearchParser _parser = new();

    [Fact]
    public void Parse_extracts_result_count_and_refs_from_fixture()
    {
        var html = FixtureLoader.Load("rightmove-search-page1.html");

        var (resultCount, refs) = _parser.Parse(html);

        resultCount.Should().Be(6);
        refs.Should().HaveCount(3);
    }

    [Fact]
    public void Parse_returns_correct_external_ids_from_fixture()
    {
        var html = FixtureLoader.Load("rightmove-search-page1.html");

        var (_, refs) = _parser.Parse(html);

        refs.Select(r => r.ExternalId).Should().BeEquivalentTo(
            ["12340001", "12340002", "12340003"],
            opts => opts.WithStrictOrdering());
    }

    [Fact]
    public void Parse_returns_absolute_urls_from_fixture()
    {
        var html = FixtureLoader.Load("rightmove-search-page1.html");

        var (_, refs) = _parser.Parse(html);

        refs.Should().AllSatisfy(r =>
        {
            r.Url.IsAbsoluteUri.Should().BeTrue();
            r.Url.Host.Should().Be("www.rightmove.co.uk");
            r.Url.AbsolutePath.Should().StartWith("/properties/");
        });
    }

    [Fact]
    public void Parse_returns_zero_refs_for_empty_results_fixture()
    {
        var html = FixtureLoader.Load("rightmove-search-empty.html");

        var (resultCount, refs) = _parser.Parse(html);

        resultCount.Should().Be(0);
        refs.Should().BeEmpty();
    }

    [Fact]
    public void Parse_throws_RightmoveParseException_when_jsonModel_is_absent()
    {
        var html = FixtureLoader.Load("rightmove-search-malformed.html");

        var act = () => _parser.Parse(html);

        act.Should().Throw<RightmoveParseException>();
    }
}

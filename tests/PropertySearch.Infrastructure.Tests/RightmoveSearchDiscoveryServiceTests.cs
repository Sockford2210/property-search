using FluentAssertions;
using Microsoft.Extensions.Options;
using PropertySearch.Infrastructure.Sources.Rightmove;
using Xunit;

namespace PropertySearch.Infrastructure.Tests;

public sealed class RightmoveSearchDiscoveryServiceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly RightmoveSearchCriteria Criteria = new(
        LocationIdentifier: "REGION^87490",
        MinPrice: null,
        MaxPrice: 2500,
        MinBedrooms: 1,
        MaxBedrooms: null,
        RadiusMiles: 0.5);

    private static RightmoveSearchDiscoveryService BuildService(
        IPageFetcher fetcher,
        int maxResults = 1000)
    {
        var opts = Options.Create(new RightmoveScraperOptions
        {
            MaxResults = maxResults,
            PolitenessDelay = TimeSpan.Zero,
        });
        return new RightmoveSearchDiscoveryService(fetcher, new RightmoveSearchParser(), opts);
    }

    private static string BuildPage(IEnumerable<(int Id, string Url)> properties, int resultCount)
    {
        var propsJson = string.Join(",", properties.Select(p =>
            $"{{\"id\":{p.Id},\"propertyUrl\":\"{p.Url}\"}}"));
        return $$"""
            <html><body>
            <script>window.jsonModel = {"resultCount":"{{resultCount}}","properties":[{{propsJson}}]}</script>
            </body></html>
            """;
    }

    [Fact]
    public async Task Discover_returns_union_of_refs_across_multiple_pages()
    {
        var page1 = BuildPage(
            [(12340001, "/properties/12340001"), (12340002, "/properties/12340002")],
            resultCount: 100);
        var page2 = BuildPage(
            [(12340003, "/properties/12340003"), (12340004, "/properties/12340004")],
            resultCount: 100);

        var fetcher = new FakePageFetcher(page1, page2, BuildPage([], 100));
        var service = BuildService(fetcher);

        var refs = await service.DiscoverAsync(Criteria, Ct);

        refs.Select(r => r.ExternalId).Should().BeEquivalentTo(
            ["12340001", "12340002", "12340003", "12340004"]);
    }

    [Fact]
    public async Task Stops_when_page_returns_no_properties()
    {
        var page1 = BuildPage(
            [(12340001, "/properties/12340001"), (12340002, "/properties/12340002")],
            resultCount: 100);

        var fetcher = new FakePageFetcher(page1, BuildPage([], 100));
        var service = BuildService(fetcher);

        var refs = await service.DiscoverAsync(Criteria, Ct);

        refs.Should().HaveCount(2);
        fetcher.CallCount.Should().Be(2);
    }

    [Fact]
    public async Task Stops_when_index_reaches_result_count()
    {
        // resultCount=2 matches exactly the number of refs on this page, so next index (24) >= 2 → stop
        var page1 = BuildPage(
            [(12340001, "/properties/12340001"), (12340002, "/properties/12340002")],
            resultCount: 2);

        var fetcher = new FakePageFetcher(page1);
        var service = BuildService(fetcher);

        var refs = await service.DiscoverAsync(Criteria, Ct);

        refs.Should().HaveCount(2);
        fetcher.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task Stops_at_max_results_cap()
    {
        var page1 = BuildPage(
            [(12340001, "/properties/12340001"),
             (12340002, "/properties/12340002"),
             (12340003, "/properties/12340003")],
            resultCount: 100);

        var fetcher = new FakePageFetcher(page1);
        var service = BuildService(fetcher, maxResults: 2);

        var refs = await service.DiscoverAsync(Criteria, Ct);

        refs.Should().HaveCount(2);
        fetcher.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task Deduplicates_refs_that_appear_on_multiple_pages()
    {
        var page1 = BuildPage(
            [(12340001, "/properties/12340001"), (12340002, "/properties/12340002")],
            resultCount: 100);
        var page2 = BuildPage(
            [(12340002, "/properties/12340002"), (12340003, "/properties/12340003")],
            resultCount: 100);

        var fetcher = new FakePageFetcher(page1, page2, BuildPage([], 100));
        var service = BuildService(fetcher);

        var refs = await service.DiscoverAsync(Criteria, Ct);

        refs.Should().HaveCount(3);
        refs.Select(r => r.ExternalId).Should().BeEquivalentTo(
            ["12340001", "12340002", "12340003"]);
    }

    [Fact(Skip = "Live smoke — remove Skip and set RUN_LIVE_SMOKE=true to run; hits Rightmove")]
    [Trait("Category", "LiveSmoke")]
    public async Task Live_search_returns_100_or_more_refs_with_valid_property_urls()
    {
        var opts = Options.Create(new RightmoveScraperOptions
        {
            PolitenessDelay = TimeSpan.FromSeconds(2),
            MaxResults = 200,
        });

        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/137.0.0.0 Safari/537.36");

        var fetcher = new HttpPageFetcher(httpClient, opts);
        var service = new RightmoveSearchDiscoveryService(fetcher, new RightmoveSearchParser(), opts);

        var criteria = new RightmoveSearchCriteria(
            LocationIdentifier: "REGION^87490",
            MinPrice: null,
            MaxPrice: 2500,
            MinBedrooms: 1,
            MaxBedrooms: null,
            RadiusMiles: 1.0);

        var refs = await service.DiscoverAsync(criteria, Ct);

        refs.Should().HaveCountGreaterThanOrEqualTo(100);
        refs.Should().AllSatisfy(r =>
        {
            r.Url.IsAbsoluteUri.Should().BeTrue();
            r.Url.AbsolutePath.Should().MatchRegex(@"^/properties/\d+");
        });
    }

    private sealed class FakePageFetcher(params string[] pages) : IPageFetcher
    {
        private readonly Queue<string> _pages = new(pages);
        public int CallCount { get; private set; }

        public Task<string> GetAsync(Uri url, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(_pages.Count > 0 ? _pages.Dequeue() : BuildPage([], 0));
        }
    }
}

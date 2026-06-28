using Microsoft.Extensions.Options;

namespace PropertySearch.Infrastructure.Sources.Rightmove;

public sealed class RightmoveSearchDiscoveryService(
    IPageFetcher pageFetcher,
    RightmoveSearchParser parser,
    IOptions<RightmoveScraperOptions> options)
{
    private const int PageSize = 24;

    public async Task<IReadOnlyList<SearchResultRef>> DiscoverAsync(
        RightmoveSearchCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        var opts = options.Value;
        var seen = new Dictionary<string, SearchResultRef>(StringComparer.Ordinal);
        int index = 0;
        int resultCount = int.MaxValue;

        while (index < resultCount)
        {
            var url = BuildSearchUrl(criteria, opts.BaseUrl, index);
            var html = await pageFetcher.GetAsync(url, cancellationToken);
            var (pageResultCount, refs) = parser.Parse(html);

            resultCount = pageResultCount;

            if (refs.Count == 0)
                break;

            foreach (var r in refs)
                seen.TryAdd(r.ExternalId, r);

            if (seen.Count >= opts.MaxResults)
                break;

            index += PageSize;
        }

        return seen.Values.Take(opts.MaxResults).ToList().AsReadOnly();
    }

    private static Uri BuildSearchUrl(RightmoveSearchCriteria criteria, string baseUrl, int index)
    {
        var parameters = new List<string>
        {
            $"locationIdentifier={Uri.EscapeDataString(criteria.LocationIdentifier)}",
            $"radius={criteria.RadiusMiles:F1}",
            $"index={index}",
        };

        if (criteria.MinPrice.HasValue)
            parameters.Add($"minPrice={criteria.MinPrice.Value}");
        if (criteria.MaxPrice.HasValue)
            parameters.Add($"maxPrice={criteria.MaxPrice.Value}");
        if (criteria.MinBedrooms.HasValue)
            parameters.Add($"minBedrooms={criteria.MinBedrooms.Value}");
        if (criteria.MaxBedrooms.HasValue)
            parameters.Add($"maxBedrooms={criteria.MaxBedrooms.Value}");

        return new Uri($"{baseUrl}/property-to-rent/find.html?{string.Join('&', parameters)}");
    }
}

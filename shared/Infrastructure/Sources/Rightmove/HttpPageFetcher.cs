using System.Net;
using Microsoft.Extensions.Options;

namespace PropertySearch.Infrastructure.Sources.Rightmove;

public sealed class HttpPageFetcher(HttpClient httpClient, IOptions<RightmoveScraperOptions> options) : IPageFetcher
{
    public async Task<string> GetAsync(Uri url, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync(url, cancellationToken);

        if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests)
            throw new ScrapeBlockedException(
                $"Scrape blocked by Rightmove: {(int)response.StatusCode} {response.ReasonPhrase}");

        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync(cancellationToken);

        await Task.Delay(options.Value.PolitenessDelay, cancellationToken);

        return html;
    }
}

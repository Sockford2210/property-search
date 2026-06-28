namespace PropertySearch.Infrastructure.Sources.Rightmove;

public interface IPageFetcher
{
    Task<string> GetAsync(Uri url, CancellationToken cancellationToken = default);
}

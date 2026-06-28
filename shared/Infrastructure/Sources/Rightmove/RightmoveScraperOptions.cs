namespace PropertySearch.Infrastructure.Sources.Rightmove;

public sealed class RightmoveScraperOptions
{
    public const string SectionName = "RightmoveScraper";

    public TimeSpan PolitenessDelay { get; set; } = TimeSpan.FromSeconds(2);
    public int MaxResults { get; set; } = 1000;
    public string BaseUrl { get; set; } = "https://www.rightmove.co.uk";
}

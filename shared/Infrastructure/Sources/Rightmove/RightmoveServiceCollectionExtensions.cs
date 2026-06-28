using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace PropertySearch.Infrastructure.Sources.Rightmove;

public static class RightmoveServiceCollectionExtensions
{
    public static IServiceCollection AddRightmoveScraper(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<RightmoveScraperOptions>(
            configuration.GetSection(RightmoveScraperOptions.SectionName));

        services.AddSingleton<RightmoveSearchParser>();
        services.AddScoped<RightmoveSearchDiscoveryService>();

        services
            .AddHttpClient<IPageFetcher, HttpPageFetcher>(client =>
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd(
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/137.0.0.0 Safari/537.36");
                client.DefaultRequestHeaders.Accept.ParseAdd("text/html");
            })
            .AddStandardResilienceHandler();

        return services;
    }
}

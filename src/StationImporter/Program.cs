using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PropertySearch.Infrastructure;
using PropertySearch.Infrastructure.Stations;

// Connection string matches the design-time factory: env-var driven, defaulting
// to the local Docker Compose database on port 5433. The schema must already be
// applied (`dotnet ef database update`); this app does not migrate.
const string defaultConnectionString =
    "Host=localhost;Port=5433;Database=propertysearch;Username=propertysearch;Password=propertysearch";

var builder = Host.CreateApplicationBuilder(args);

var connectionString =
    Environment.GetEnvironmentVariable("PROPERTYSEARCH_DB") ?? defaultConnectionString;

builder.Services.AddDbContext<PropertyDbContext>(options =>
    options.UsePropertySearchNpgsql(connectionString));
builder.Services.AddSingleton<IStationDataSource, EmbeddedStationDataSource>();
builder.Services.AddScoped<StationImportService>();

using var host = builder.Build();

var logger = host.Services.GetRequiredService<ILogger<Program>>();

try
{
    using var scope = host.Services.CreateScope();
    var importer = scope.ServiceProvider.GetRequiredService<StationImportService>();

    var result = await importer.ImportAsync();

    logger.LogInformation(
        "Station import complete: {Inserted} inserted, {Updated} updated, {Unchanged} unchanged, {Total} total.",
        result.Inserted, result.Updated, result.Unchanged, result.Total);
    return 0;
}
catch (Exception ex)
{
    logger.LogError(ex, "Station import failed.");
    return 1;
}

// Program is referenced by ILogger<Program>; make the implicit class discoverable.
public partial class Program;

using Microsoft.EntityFrameworkCore;
using PropertySearch.Domain;

namespace PropertySearch.Infrastructure.Stations;

/// <summary>
/// Imports the station dataset into the <c>stations</c> table. Dataset rows are
/// collapsed to one row per physical station (grouped by <see cref="StationRecord.Code"/>,
/// labelled with the primary mode), then upserted by <c>station_code</c> so the
/// import is idempotent — re-running makes no changes.
/// </summary>
public sealed class StationImportService(PropertyDbContext context, IStationDataSource dataSource)
{
    public async Task<StationImportResult> ImportAsync(CancellationToken cancellationToken = default)
    {
        var physicalStations = dataSource.GetStations()
            .GroupBy(record => record.Code)
            .Select(group =>
            {
                var primaryMode = StationModePrecedence.SelectPrimaryMode(group.Select(r => r.Mode));
                return group.First(r => r.Mode == primaryMode);
            })
            .ToList();

        var existing = await context.Stations.ToDictionaryAsync(s => s.StationCode, cancellationToken);

        var inserted = 0;
        var updated = 0;
        var unchanged = 0;

        foreach (var record in physicalStations)
        {
            if (existing.TryGetValue(record.Code, out var station))
            {
                if (station.Name != record.Name
                    || station.Mode != record.Mode
                    || station.Latitude != record.Latitude
                    || station.Longitude != record.Longitude)
                {
                    station.Name = record.Name;
                    station.Mode = record.Mode;
                    station.Latitude = record.Latitude;
                    station.Longitude = record.Longitude;
                    updated++;
                }
                else
                {
                    unchanged++;
                }
            }
            else
            {
                context.Stations.Add(new Station
                {
                    StationCode = record.Code,
                    Name = record.Name,
                    Mode = record.Mode,
                    Latitude = record.Latitude,
                    Longitude = record.Longitude,
                });
                inserted++;
            }
        }

        await context.SaveChangesAsync(cancellationToken);

        return new StationImportResult(inserted, updated, unchanged, physicalStations.Count);
    }
}

/// <summary>Outcome of a single <see cref="StationImportService.ImportAsync"/> run.</summary>
public sealed record StationImportResult(int Inserted, int Updated, int Unchanged, int Total);

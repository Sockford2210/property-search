using PropertySearch.Domain.Enums;

namespace PropertySearch.Infrastructure.Stations;

/// <summary>
/// A single row of the station dataset: one (physical station, mode) pairing.
/// Rows sharing a <see cref="Code"/> are collapsed into one <see cref="Domain.Station"/>
/// by <see cref="StationImportService"/>.
/// </summary>
public sealed record StationRecord(
    string Code,
    string Name,
    double Latitude,
    double Longitude,
    TransportMode Mode);

using PropertySearch.Domain;

namespace PropertySearch.Infrastructure.Spatial;

/// <summary>
/// Finds the nearest <see cref="Station"/> to a geographic coordinate. Backed by
/// the GiST-indexed PostGIS <c>location</c> column created in Phase 1. The Phase 3
/// spatial entry point; consumed by property enrichment in Phase 8.
/// </summary>
public interface INearestStationFinder
{
    /// <summary>
    /// Returns the single nearest station to the given coordinate and its distance
    /// in metres on the WGS84 spheroid, or <c>null</c> when no stations exist.
    /// </summary>
    Task<NearestStationResult?> FindNearestAsync(
        double latitude, double longitude, CancellationToken cancellationToken = default);
}

/// <summary>The nearest station to a query point and its great-circle distance in metres.</summary>
public sealed record NearestStationResult(Station Station, double DistanceMeters);

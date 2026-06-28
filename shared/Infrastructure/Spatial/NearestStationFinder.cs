using Microsoft.EntityFrameworkCore;
using PropertySearch.Domain;

namespace PropertySearch.Infrastructure.Spatial;

/// <summary>
/// Nearest-station lookup against PostGIS. Candidates are ordered cheaply by the
/// GiST-indexed planar KNN operator (<c>&lt;-&gt;</c>), then the true distance in
/// metres is measured on the WGS84 spheroid by casting to <c>geography</c>. The
/// closest few candidates are re-ranked by that spheroidal distance so the rare
/// case where planar ordering disagrees with metric ordering is still correct.
/// </summary>
public sealed class NearestStationFinder(PropertyDbContext context) : INearestStationFinder
{
    /// <summary>
    /// How many of the planar-nearest candidates to re-measure with the spheroidal
    /// distance. Five is ample to absorb any planar/metric disagreement at the
    /// single-nearest scale while staying a tiny, index-served fetch.
    /// </summary>
    private const int CandidateCount = 5;

    public async Task<NearestStationResult?> FindNearestAsync(
        double latitude, double longitude, CancellationToken cancellationToken = default)
    {
        // {0} = longitude, {1} = latitude (PostGIS point order is X=lon, Y=lat).
        // Placeholders are reused, so SqlQueryRaw binds exactly two parameters.
        // CandidateCount is a trusted internal constant appended to the text.
        var sql =
            """
            SELECT s.id AS "id",
                   ST_Distance(s.location::geography,
                               ST_SetSRID(ST_MakePoint({0}, {1}), 4326)::geography) AS "distance_meters"
            FROM stations s
            ORDER BY s.location <-> ST_SetSRID(ST_MakePoint({0}, {1}), 4326)
            LIMIT
            """ + " " + CandidateCount;

        var candidates = await context.Database
            .SqlQueryRaw<NearestCandidate>(sql, longitude, latitude)
            .ToListAsync(cancellationToken);

        if (candidates.Count == 0)
        {
            return null;
        }

        var nearest = candidates.MinBy(c => c.DistanceMeters)!;

        var station = await context.Stations
            .FirstAsync(s => s.Id == nearest.Id, cancellationToken);

        return new NearestStationResult(station, nearest.DistanceMeters);
    }

    /// <summary>Row shape for the candidate query; not part of the EF model.</summary>
    private sealed record NearestCandidate(long Id, double DistanceMeters);
}

using PropertySearch.Domain.Enums;

namespace PropertySearch.Domain;

/// <summary>
/// A public-transport station. Imported in Phase 2 and used for nearest-station
/// and distance calculations from Phase 3 onwards.
/// </summary>
public class Station : AuditableEntity
{
    /// <summary>
    /// Stable natural key for the physical station — the TfL/NaPTAN hub code for
    /// multi-mode interchanges, otherwise the station's NaPTAN id. Used as the
    /// idempotent upsert key by the Phase 2 import service.
    /// </summary>
    public required string StationCode { get; set; }

    public required string Name { get; set; }

    public TransportMode Mode { get; set; }

    public double Latitude { get; set; }

    public double Longitude { get; set; }
}

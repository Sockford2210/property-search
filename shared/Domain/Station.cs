using PropertySearch.Domain.Enums;

namespace PropertySearch.Domain;

/// <summary>
/// A public-transport station. Imported in Phase 2 and used for nearest-station
/// and distance calculations from Phase 3 onwards.
/// </summary>
public class Station : AuditableEntity
{
    public required string Name { get; set; }

    public TransportMode Mode { get; set; }

    public double Latitude { get; set; }

    public double Longitude { get; set; }
}

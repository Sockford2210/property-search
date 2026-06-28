namespace PropertySearch.Domain;

/// <summary>
/// A canonical, source-independent property record. Produced by reconciling one
/// or more <see cref="Listing"/>s during normalisation. Descriptive fields are
/// nullable because they are populated by later phases, not at creation.
/// </summary>
public class Property : AuditableEntity
{
    public string? DisplayAddress { get; set; }

    /// <summary>Canonical monthly rent in GBP.</summary>
    public decimal? RentPcm { get; set; }

    public int? Bedrooms { get; set; }

    public int? Bathrooms { get; set; }

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    public ICollection<Listing> Listings { get; } = new List<Listing>();
}

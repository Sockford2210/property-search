using PropertySearch.Domain.Enums;

namespace PropertySearch.Domain;

/// <summary>
/// A property advert as scraped from a single source portal. Holds the raw,
/// per-portal view. Multiple listings may later be reconciled into one canonical
/// <see cref="Property"/> by the normalisation phase.
/// </summary>
public class Listing : AuditableEntity
{
    /// <summary>Source portal this listing came from.</summary>
    public long SourceId { get; set; }

    public Source Source { get; set; } = null!;

    /// <summary>Canonical property this listing has been normalised into, if any.</summary>
    public long? PropertyId { get; set; }

    public Property? Property { get; set; }

    /// <summary>Portal's own identifier for the listing. Unique within a source.</summary>
    public required string ExternalId { get; set; }

    public required string Url { get; set; }

    public required string DisplayAddress { get; set; }

    /// <summary>Monthly rent in GBP.</summary>
    public decimal RentPcm { get; set; }

    /// <summary>Number of bedrooms (studio = 0).</summary>
    public int Bedrooms { get; set; }

    public int? Bathrooms { get; set; }

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    public string? Description { get; set; }

    public ListingStatus Status { get; set; } = ListingStatus.Active;

    /// <summary>First time this listing was observed on the portal.</summary>
    public DateTime FirstSeenAt { get; set; }

    /// <summary>Most recent time this listing was observed on the portal.</summary>
    public DateTime LastSeenAt { get; set; }
}

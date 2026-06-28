namespace PropertySearch.Domain;

/// <summary>
/// An external property portal that listings are collected from
/// (e.g. Rightmove, Zoopla). A reference/lookup record.
/// </summary>
public class Source : AuditableEntity
{
    /// <summary>Stable slug, unique across sources (e.g. "rightmove").</summary>
    public required string Code { get; set; }

    /// <summary>Human-readable name (e.g. "Rightmove").</summary>
    public required string Name { get; set; }

    /// <summary>Base URL of the portal, if known.</summary>
    public string? BaseUrl { get; set; }

    /// <summary>Whether this source is currently in use.</summary>
    public bool Enabled { get; set; }

    public ICollection<Listing> Listings { get; } = new List<Listing>();
}

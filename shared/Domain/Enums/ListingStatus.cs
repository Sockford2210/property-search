namespace PropertySearch.Domain.Enums;

/// <summary>
/// Lifecycle state of a <see cref="Listing"/> relative to its source portal.
/// Persisted as a string for human-readability and resilience to reordering.
/// </summary>
public enum ListingStatus
{
    /// <summary>Currently present on the source portal.</summary>
    Active,

    /// <summary>No longer present on the source portal.</summary>
    Removed,
}

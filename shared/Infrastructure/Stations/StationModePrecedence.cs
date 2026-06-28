using PropertySearch.Domain.Enums;

namespace PropertySearch.Infrastructure.Stations;

/// <summary>
/// Resolves the single primary <see cref="TransportMode"/> for a physical station
/// served by several modes. A station's row keeps one mode (Phase 1 schema), so
/// interchanges are labelled by the most rapid-transit mode they offer.
/// </summary>
public static class StationModePrecedence
{
    /// <summary>Highest precedence first.</summary>
    private static readonly TransportMode[] Order =
    [
        TransportMode.Underground,
        TransportMode.ElizabethLine,
        TransportMode.Overground,
        TransportMode.Dlr,
        TransportMode.NationalRail,
    ];

    public static TransportMode SelectPrimaryMode(IEnumerable<TransportMode> modes)
    {
        var present = modes as IReadOnlyCollection<TransportMode> ?? modes.ToList();
        if (present.Count == 0)
        {
            throw new ArgumentException("At least one mode is required.", nameof(modes));
        }

        foreach (var mode in Order)
        {
            if (present.Contains(mode))
            {
                return mode;
            }
        }

        // Defensive: a mode not listed in the precedence order — keep the first.
        return present.First();
    }
}

namespace PropertySearch.Infrastructure.Sources.Rightmove;

public sealed record RightmoveSearchCriteria(
    string LocationIdentifier,
    int? MinPrice,
    int? MaxPrice,
    int? MinBedrooms,
    int? MaxBedrooms,
    double RadiusMiles);

namespace PropertySearch.Infrastructure.Sources.Rightmove;

public sealed record ParsedListing(
    string ExternalId,
    Uri Url,
    string DisplayAddress,
    decimal RentPcm,
    int Bedrooms,
    int? Bathrooms,
    double? Latitude,
    double? Longitude,
    string? Description);

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using AngleSharp.Html.Parser;

namespace PropertySearch.Infrastructure.Sources.Rightmove;

public sealed class RightmoveListingParser
{
    private const string BaseUrl = "https://www.rightmove.co.uk";
    private static readonly IHtmlParser s_htmlParser = new HtmlParser();
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public ParsedListing Parse(string html)
    {
        using var document = s_htmlParser.ParseDocument(html);

        var scriptTag = document.Scripts
            .FirstOrDefault(s => s.Text.Contains("window.PAGE_MODEL", StringComparison.Ordinal));

        if (scriptTag is null)
            throw new ListingParseException("No script containing window.PAGE_MODEL found in page.");

        var json = ExtractJson(scriptTag.Text);

        PageModel model;
        try
        {
            model = JsonSerializer.Deserialize<PageModel>(json, s_jsonOptions)
                ?? throw new ListingParseException("Deserialized PAGE_MODEL was null.");
        }
        catch (JsonException ex)
        {
            throw new ListingParseException($"Failed to deserialize PAGE_MODEL: {ex.Message}");
        }

        var data = model.PropertyData
            ?? throw new ListingParseException("PAGE_MODEL.propertyData is missing.");

        var externalId = data.Id.ToString(CultureInfo.InvariantCulture);
        var url = new Uri($"{BaseUrl}/properties/{externalId}");

        var displayAddress = data.Address?.DisplayAddress;
        if (string.IsNullOrWhiteSpace(displayAddress))
            throw new ListingParseException("Missing required field: displayAddress.", url);

        var rentPcm = NormaliseRent(data.Prices, url);

        if (data.Bedrooms is null)
            throw new ListingParseException("Missing required field: bedrooms.", url);

        return new ParsedListing(
            ExternalId: externalId,
            Url: url,
            DisplayAddress: displayAddress,
            RentPcm: rentPcm,
            Bedrooms: data.Bedrooms.Value,
            Bathrooms: data.Bathrooms,
            Latitude: data.Location?.Latitude,
            Longitude: data.Location?.Longitude,
            Description: StripHtml(data.Text?.Description));
    }

    private static decimal NormaliseRent(PricesModel? prices, Uri url)
    {
        if (prices is null)
            throw new ListingParseException("Missing required field: prices.", url);

        var frequency = prices.Frequency?.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(frequency))
            throw new ListingParseException("Rent frequency is missing or unparseable.", url);

        if (frequency == "poa")
            throw new ListingParseException("Rent is POA and cannot be stored as a fixed amount.", url);

        var amount = prices.Amount;
        if (amount <= 0)
            throw new ListingParseException("Rent amount is zero or negative.", url);

        return frequency switch
        {
            "monthly" => amount,
            "weekly" => Math.Round(amount * 52m / 12m, MidpointRounding.AwayFromZero),
            _ => throw new ListingParseException($"Unrecognised rent frequency '{frequency}'.", url),
        };
    }

    private static string? StripHtml(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return null;
        using var doc = s_htmlParser.ParseDocument(html);
        var text = doc.Body?.TextContent?.Trim();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static string ExtractJson(string scriptText)
    {
        const string marker = "window.PAGE_MODEL = ";
        var markerIndex = scriptText.IndexOf(marker, StringComparison.Ordinal);
        if (markerIndex < 0)
            throw new ListingParseException("PAGE_MODEL assignment not found in script.");

        var jsonStart = scriptText.IndexOf('{', markerIndex + marker.Length);
        var jsonEnd = scriptText.LastIndexOf('}');

        if (jsonStart < 0 || jsonEnd <= jsonStart)
            throw new ListingParseException("Could not locate JSON object boundaries in PAGE_MODEL script.");

        return scriptText[jsonStart..(jsonEnd + 1)];
    }

    private sealed record PageModel(
        [property: JsonPropertyName("propertyData")] PropertyDataModel? PropertyData);

    private sealed record PropertyDataModel(
        [property: JsonPropertyName("id")] long Id,
        [property: JsonPropertyName("address")] AddressModel? Address,
        [property: JsonPropertyName("prices")] PricesModel? Prices,
        [property: JsonPropertyName("bedrooms")] int? Bedrooms,
        [property: JsonPropertyName("bathrooms")] int? Bathrooms,
        [property: JsonPropertyName("location")] LocationModel? Location,
        [property: JsonPropertyName("text")] TextModel? Text);

    private sealed record AddressModel(
        [property: JsonPropertyName("displayAddress")] string? DisplayAddress);

    private sealed record PricesModel(
        [property: JsonPropertyName("amount")] decimal Amount,
        [property: JsonPropertyName("frequency")] string? Frequency);

    private sealed record LocationModel(
        [property: JsonPropertyName("latitude")] double? Latitude,
        [property: JsonPropertyName("longitude")] double? Longitude);

    private sealed record TextModel(
        [property: JsonPropertyName("description")] string? Description);
}

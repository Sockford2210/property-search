using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using AngleSharp.Html.Parser;

namespace PropertySearch.Infrastructure.Sources.Rightmove;

public sealed class RightmoveSearchParser
{
    private const string BaseUrl = "https://www.rightmove.co.uk";
    private static readonly IHtmlParser s_htmlParser = new HtmlParser();
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public (int ResultCount, IReadOnlyList<SearchResultRef> Refs) Parse(string html)
    {
        using var document = s_htmlParser.ParseDocument(html);

        var scriptTag = document.Scripts
            .FirstOrDefault(s => s.Text.Contains("window.jsonModel", StringComparison.Ordinal));

        if (scriptTag is null)
            throw new RightmoveParseException("No script containing window.jsonModel found in page.");

        var json = ExtractJson(scriptTag.Text);

        JsonModel model;
        try
        {
            model = JsonSerializer.Deserialize<JsonModel>(json, s_jsonOptions)
                ?? throw new RightmoveParseException("Deserialized jsonModel was null.");
        }
        catch (JsonException ex)
        {
            throw new RightmoveParseException($"Failed to deserialize jsonModel: {ex.Message}");
        }

        var resultCount = int.Parse(
            model.ResultCount.Replace(",", "", StringComparison.Ordinal),
            CultureInfo.InvariantCulture);

        var refs = model.Properties
            .Select(p => new SearchResultRef(p.Id.ToString(CultureInfo.InvariantCulture), ToAbsoluteUrl(p.PropertyUrl)))
            .ToList()
            .AsReadOnly();

        return (resultCount, refs);
    }

    private static string ExtractJson(string scriptText)
    {
        const string marker = "window.jsonModel = ";
        var markerIndex = scriptText.IndexOf(marker, StringComparison.Ordinal);
        if (markerIndex < 0)
            throw new RightmoveParseException("jsonModel assignment not found in script.");

        var jsonStart = scriptText.IndexOf('{', markerIndex + marker.Length);
        var jsonEnd = scriptText.LastIndexOf('}');

        if (jsonStart < 0 || jsonEnd <= jsonStart)
            throw new RightmoveParseException("Could not locate JSON object boundaries in jsonModel script.");

        return scriptText[jsonStart..(jsonEnd + 1)];
    }

    private static Uri ToAbsoluteUrl(string propertyUrl) =>
        propertyUrl.StartsWith('/')
            ? new Uri(BaseUrl + propertyUrl)
            : new Uri(propertyUrl);

    private sealed record JsonModel(
        [property: JsonPropertyName("resultCount")] string ResultCount,
        [property: JsonPropertyName("properties")] JsonProperty[] Properties);

    private sealed record JsonProperty(
        [property: JsonPropertyName("id")] long Id,
        [property: JsonPropertyName("propertyUrl")] string PropertyUrl);
}

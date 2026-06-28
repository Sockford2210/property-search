using System.Text.Json;
using System.Text.Json.Serialization;

namespace PropertySearch.Infrastructure.Stations;

/// <summary>
/// Reads the station dataset from the <c>london-stations.json</c> resource
/// embedded in this assembly. The dataset is generated from TfL open data by
/// <c>scripts/build-station-dataset.py</c>; see <c>Data/README.md</c>.
/// </summary>
public sealed class EmbeddedStationDataSource : IStationDataSource
{
    private const string ResourceSuffix = "london-stations.json";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public IReadOnlyList<StationRecord> GetStations()
    {
        var assembly = typeof(EmbeddedStationDataSource).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .Single(name => name.EndsWith(ResourceSuffix, StringComparison.Ordinal));

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded station dataset '{resourceName}' could not be opened.");

        return JsonSerializer.Deserialize<List<StationRecord>>(stream, SerializerOptions)
            ?? throw new InvalidOperationException("Station dataset deserialised to null.");
    }
}

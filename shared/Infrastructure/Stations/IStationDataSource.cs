namespace PropertySearch.Infrastructure.Stations;

/// <summary>
/// Supplies the raw station dataset to the import service. Abstracted so the
/// source (currently a committed, embedded file) can change without touching
/// import logic.
/// </summary>
public interface IStationDataSource
{
    IReadOnlyList<StationRecord> GetStations();
}

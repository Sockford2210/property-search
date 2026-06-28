namespace PropertySearch.Infrastructure.Tests;

internal static class FixtureLoader
{
    private static readonly string FixtureRoot =
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "Rightmove");

    public static string Load(string name) =>
        File.ReadAllText(Path.Combine(FixtureRoot, name));
}

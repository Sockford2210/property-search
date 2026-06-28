namespace PropertySearch.Infrastructure.Sources.Rightmove;

public sealed class ScrapeBlockedException : Exception
{
    public ScrapeBlockedException(string message) : base(message) { }
    public ScrapeBlockedException(string message, Exception inner) : base(message, inner) { }
}

namespace PropertySearch.Infrastructure.Sources.Rightmove;

public sealed class RightmoveParseException : Exception
{
    public RightmoveParseException(string message) : base(message) { }
}

namespace PropertySearch.Infrastructure.Sources.Rightmove;

public sealed class ListingParseException : Exception
{
    public ListingParseException(string reason) : base(reason) { }

    public ListingParseException(string reason, Uri? sourceUrl)
        : base(sourceUrl is null ? reason : $"{reason} (url: {sourceUrl})")
    {
        SourceUrl = sourceUrl;
    }

    public Uri? SourceUrl { get; }
}

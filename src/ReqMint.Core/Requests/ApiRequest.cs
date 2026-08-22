namespace ReqMint.Core.Requests;

public sealed record ApiRequest
{
    private static readonly IReadOnlyDictionary<string, string> EmptyHeaders =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public required string Method { get; init; }

    public required Uri Url { get; init; }

    public IReadOnlyDictionary<string, string> Headers { get; init; } = EmptyHeaders;

    public string? Body { get; init; }

    public string? ContentType { get; init; }

    public static ApiRequest Create(string method, string url)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(method);

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("A valid HTTP or HTTPS URL is required.", nameof(url));
        }

        return new ApiRequest
        {
            Method = method.Trim().ToUpperInvariant(),
            Url = uri,
        };
    }
}

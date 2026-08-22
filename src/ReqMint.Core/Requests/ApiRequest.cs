namespace ReqMint.Core.Requests;

public sealed record ApiRequest
{
    public required string Method { get; init; }

    public required Uri Url { get; init; }

    public IReadOnlyList<RequestField> QueryParameters { get; init; } = [];

    public IReadOnlyList<RequestField> Headers { get; init; } = [];

    public ApiRequestBody? Body { get; init; }

    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);

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

public sealed record RequestField(string Name, string Value);

public sealed record ApiRequestBody(string Content, string ContentType);

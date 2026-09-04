using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using ReqMint.Core.Requests;

namespace ReqMint.Http;

public sealed class HttpRequestExecutor : IRequestExecutor, IDisposable
{
    private readonly HttpClient _client;

    public HttpRequestExecutor()
        : this(CreateDefaultHandler(useCookies: false))
    {
    }

    public HttpRequestExecutor(HttpMessageHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _client = new HttpClient(handler, disposeHandler: true)
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
    }

    public async Task<ApiResponse> ExecuteAsync(
        ApiRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Request timeout must be greater than zero.");
        }

        if (request.ResponsePreviewLimitBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                "Response preview limit must be greater than zero.");
        }

        using var message = CreateMessage(request);
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(request.Timeout);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var response = await _client.SendAsync(
                message,
                HttpCompletionOption.ResponseHeadersRead,
                timeoutSource.Token);

            var (body, isTruncated) = await ReadPreviewAsync(
                response.Content,
                request.ResponsePreviewLimitBytes,
                timeoutSource.Token);
            stopwatch.Stop();
            var headers = MergeHeaders(response);

            return new ApiResponse(
                (int)response.StatusCode,
                response.ReasonPhrase ?? string.Empty,
                headers,
                body,
                response.Content.Headers.ContentType?.ToString(),
                stopwatch.Elapsed,
                isTruncated);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"The request exceeded the {request.Timeout.TotalSeconds:N0} second timeout.", exception);
        }
    }

    public void Dispose() => _client.Dispose();

    internal static SocketsHttpHandler CreateDefaultHandler(
        bool useCookies,
        CookieContainer? cookieContainer = null) => new()
    {
        AutomaticDecompression = DecompressionMethods.All,
        AllowAutoRedirect = true,
        UseCookies = useCookies,
        CookieContainer = cookieContainer ?? new CookieContainer(),
        PooledConnectionLifetime = TimeSpan.FromMinutes(10),
    };

    private static HttpRequestMessage CreateMessage(ApiRequest request)
    {
        var message = new HttpRequestMessage(new HttpMethod(request.Method), BuildUri(request))
        {
            Version = HttpVersion.Version20,
            VersionPolicy = HttpVersionPolicy.RequestVersionOrLower,
        };

        if (request.Body is not null)
        {
            message.Content = string.Equals(
                request.Body.ContentType,
                "application/x-www-form-urlencoded",
                StringComparison.OrdinalIgnoreCase) && request.Body.FormFields.Count > 0
                ? new FormUrlEncodedContent(request.Body.FormFields
                    .Where(field => field.IsEnabled)
                    .Select(field =>
                    new KeyValuePair<string, string>(field.Name, field.Value)))
                : new StringContent(
                    request.Body.Content,
                    Encoding.UTF8,
                    request.Body.ContentType);
        }

        foreach (var header in request.Headers)
        {
            if (string.IsNullOrWhiteSpace(header.Name))
            {
                continue;
            }

            if (header.Value.IndexOfAny(['\r', '\n']) >= 0)
            {
                throw new ArgumentException(
                    "HTTP header values cannot contain carriage-return or line-feed characters.",
                    nameof(request));
            }

            try
            {
                if (!message.Headers.TryAddWithoutValidation(header.Name, header.Value))
                {
                    message.Content ??= new ByteArrayContent([]);
                    if (!message.Content.Headers.TryAddWithoutValidation(header.Name, header.Value))
                    {
                        throw new ArgumentException("An HTTP header name is invalid.", nameof(request));
                    }
                }
            }
            catch (FormatException exception)
            {
                throw new ArgumentException("An HTTP header name is invalid.", nameof(request), exception);
            }
        }

        return message;
    }

    private static Uri BuildUri(ApiRequest request)
    {
        var queryParts = new List<string>();
        var existingQuery = request.Url.Query.TrimStart('?');

        if (!string.IsNullOrWhiteSpace(existingQuery))
        {
            queryParts.Add(existingQuery);
        }

        queryParts.AddRange(request.QueryParameters
            .Where(parameter => !string.IsNullOrWhiteSpace(parameter.Name))
            .Select(parameter =>
                $"{Uri.EscapeDataString(parameter.Name)}={Uri.EscapeDataString(parameter.Value)}"));

        if (queryParts.Count == 0)
        {
            return request.Url;
        }

        var builder = new UriBuilder(request.Url)
        {
            Query = string.Join("&", queryParts),
        };

        return builder.Uri;
    }

    private async Task<(string Body, bool IsTruncated)> ReadPreviewAsync(
        HttpContent content,
        int previewLimitBytes,
        CancellationToken cancellationToken)
    {
        await using var stream = await content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream(Math.Min(previewLimitBytes, 64 * 1024));
        var chunk = new byte[16 * 1024];
        var remaining = (long)previewLimitBytes + 1;

        while (remaining > 0)
        {
            var read = await stream.ReadAsync(
                chunk.AsMemory(0, (int)Math.Min(chunk.Length, remaining)),
                cancellationToken);

            if (read == 0)
            {
                break;
            }

            buffer.Write(chunk, 0, read);
            remaining -= read;
        }

        var bytes = buffer.ToArray();
        var isTruncated = bytes.Length > previewLimitBytes;
        var visibleLength = Math.Min(bytes.Length, previewLimitBytes);
        var encoding = ResolveEncoding(content.Headers.ContentType);

        return (encoding.GetString(bytes, 0, visibleLength), isTruncated);
    }

    private static Encoding ResolveEncoding(MediaTypeHeaderValue? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType?.CharSet))
        {
            return Encoding.UTF8;
        }

        try
        {
            return Encoding.GetEncoding(contentType.CharSet.Trim('"'));
        }
        catch (ArgumentException)
        {
            return Encoding.UTF8;
        }
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> MergeHeaders(
        HttpResponseMessage response)
    {
        var headers = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var header in response.Headers)
        {
            headers[header.Key] = header.Value.ToArray();
        }

        foreach (var header in response.Content.Headers)
        {
            headers[header.Key] = header.Value.ToArray();
        }

        return headers;
    }
}

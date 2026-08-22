using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using ReqMint.Core.Requests;

namespace ReqMint.Http;

public sealed class HttpRequestExecutor : IRequestExecutor, IDisposable
{
    public const int DefaultPreviewLimitBytes = 2 * 1024 * 1024;

    private readonly HttpClient _client;
    private readonly int _previewLimitBytes;

    public HttpRequestExecutor(int previewLimitBytes = DefaultPreviewLimitBytes)
        : this(CreateDefaultHandler(), previewLimitBytes)
    {
    }

    public HttpRequestExecutor(HttpMessageHandler handler, int previewLimitBytes = DefaultPreviewLimitBytes)
    {
        ArgumentNullException.ThrowIfNull(handler);

        if (previewLimitBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(previewLimitBytes));
        }

        _previewLimitBytes = previewLimitBytes;
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

        using var message = CreateMessage(request);
        var stopwatch = Stopwatch.StartNew();

        using var response = await _client.SendAsync(
            message,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        var (body, isTruncated) = await ReadPreviewAsync(response.Content, cancellationToken);
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

    public void Dispose() => _client.Dispose();

    private static SocketsHttpHandler CreateDefaultHandler() => new()
    {
        AutomaticDecompression = DecompressionMethods.All,
        AllowAutoRedirect = true,
        UseCookies = true,
        CookieContainer = new CookieContainer(),
        PooledConnectionLifetime = TimeSpan.FromMinutes(10),
    };

    private static HttpRequestMessage CreateMessage(ApiRequest request)
    {
        var message = new HttpRequestMessage(new HttpMethod(request.Method), request.Url)
        {
            Version = HttpVersion.Version20,
            VersionPolicy = HttpVersionPolicy.RequestVersionOrLower,
        };

        if (request.Body is not null)
        {
            message.Content = new StringContent(
                request.Body,
                Encoding.UTF8,
                request.ContentType ?? "application/json");
        }

        foreach (var header in request.Headers)
        {
            if (!message.Headers.TryAddWithoutValidation(header.Key, header.Value))
            {
                message.Content ??= new ByteArrayContent([]);
                message.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        return message;
    }

    private async Task<(string Body, bool IsTruncated)> ReadPreviewAsync(
        HttpContent content,
        CancellationToken cancellationToken)
    {
        await using var stream = await content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream(Math.Min(_previewLimitBytes, 64 * 1024));
        var chunk = new byte[16 * 1024];
        var remaining = _previewLimitBytes + 1;

        while (remaining > 0)
        {
            var read = await stream.ReadAsync(
                chunk.AsMemory(0, Math.Min(chunk.Length, remaining)),
                cancellationToken);

            if (read == 0)
            {
                break;
            }

            buffer.Write(chunk, 0, read);
            remaining -= read;
        }

        var bytes = buffer.ToArray();
        var isTruncated = bytes.Length > _previewLimitBytes;
        var visibleLength = Math.Min(bytes.Length, _previewLimitBytes);
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

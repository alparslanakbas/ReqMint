using System.Net;
using System.Net.Sockets;
using System.Text;
using ReqMint.Core.Requests;

namespace ReqMint.Http.Tests;

public class HttpRequestExecutorTests
{
    [Fact]
    public async Task WorkspaceExecutor_DisablesCookiesByDefaultAndIsolatesEnabledSessions()
    {
        await using var server = new CookieLoopbackServer();
        using var executor = new WorkspaceHttpRequestExecutor();
        var setCookieRequest = ApiRequest.Create("GET", new Uri(server.BaseUri, "set").AbsoluteUri);
        var echoCookieRequest = ApiRequest.Create("GET", new Uri(server.BaseUri, "echo").AbsoluteUri);

        await executor.ExecuteAsync(setCookieRequest);
        var disabledResponse = await executor.ExecuteAsync(echoCookieRequest);
        var manualCookieResponse = await executor.ExecuteAsync(echoCookieRequest with
        {
            Headers = [new RequestField("Cookie", "manual=visible")],
        });

        Assert.False(executor.IsEnabled);
        Assert.Equal(string.Empty, disabledResponse.Body);
        Assert.Contains("manual=visible", manualCookieResponse.Body, StringComparison.Ordinal);

        executor.SetEnabled(true);
        executor.SelectWorkspace(Path.Combine(Path.GetTempPath(), "reqmint-cookie-alpha"));
        await executor.ExecuteAsync(setCookieRequest);
        var alphaResponse = await executor.ExecuteAsync(echoCookieRequest);

        executor.SelectWorkspace(Path.Combine(Path.GetTempPath(), "reqmint-cookie-beta"));
        var betaResponse = await executor.ExecuteAsync(echoCookieRequest);

        executor.SelectWorkspace(Path.Combine(Path.GetTempPath(), "reqmint-cookie-alpha"));
        var restoredAlphaResponse = await executor.ExecuteAsync(echoCookieRequest);
        executor.ClearActiveWorkspace();
        var clearedAlphaResponse = await executor.ExecuteAsync(echoCookieRequest);
        await executor.ExecuteAsync(setCookieRequest);
        executor.SetEnabled(false);
        executor.SetEnabled(true);
        var disabledAndRestoredAlphaResponse = await executor.ExecuteAsync(echoCookieRequest);

        Assert.Contains("session=alpha", alphaResponse.Body, StringComparison.Ordinal);
        Assert.Equal(string.Empty, betaResponse.Body);
        Assert.Contains("session=alpha", restoredAlphaResponse.Body, StringComparison.Ordinal);
        Assert.Equal(string.Empty, clearedAlphaResponse.Body);
        Assert.Equal(string.Empty, disabledAndRestoredAlphaResponse.Body);
    }

    [Fact]
    public async Task ExecuteAsync_PreservesSuccessfulJsonResponse()
    {
        using var executor = new HttpRequestExecutor(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"orderId\":42}", Encoding.UTF8, "application/json"),
        }));

        var response = await executor.ExecuteAsync(ApiRequest.Create("GET", "https://example.com/orders/42"));

        Assert.True(response.IsSuccessStatusCode);
        Assert.Equal(200, response.StatusCode);
        Assert.Equal("{\"orderId\":42}", response.Body);
        Assert.Contains("application/json", response.ContentType);
        Assert.False(response.IsBodyTruncated);
    }

    [Fact]
    public async Task ExecuteAsync_PreservesErrorResponseBody()
    {
        using var executor = new HttpRequestExecutor(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("validation failed"),
        }));

        var response = await executor.ExecuteAsync(ApiRequest.Create("GET", "https://example.com/orders/42"));

        Assert.False(response.IsSuccessStatusCode);
        Assert.Equal(400, response.StatusCode);
        Assert.Equal("validation failed", response.Body);
    }

    [Fact]
    public async Task ExecuteAsync_TruncatesPreviewAtConfiguredLimit()
    {
        using var executor = new HttpRequestExecutor(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("1234567890"),
        }));
        var request = ApiRequest.Create("GET", "https://example.com/large") with
        {
            ResponsePreviewLimitBytes = 5,
        };

        var response = await executor.ExecuteAsync(request);

        Assert.Equal("12345", response.Body);
        Assert.True(response.IsBodyTruncated);
    }

    [Fact]
    public async Task ExecuteAsync_RejectsInvalidResponsePreviewLimit()
    {
        using var executor = new HttpRequestExecutor(new StubHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)));
        var request = ApiRequest.Create("GET", "https://example.com/large") with
        {
            ResponsePreviewLimitBytes = 0,
        };

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => executor.ExecuteAsync(request));
    }

    [Fact]
    public async Task ExecuteAsync_ComposesMethodQueryHeadersAndBody()
    {
        string? observedMethod = null;
        Uri? observedUri = null;
        string? observedHeader = null;
        string? observedBody = null;
        string? observedContentType = null;

        using var executor = new HttpRequestExecutor(new AsyncStubHandler(async (request, cancellationToken) =>
        {
            observedMethod = request.Method.Method;
            observedUri = request.RequestUri;
            observedHeader = request.Headers.GetValues("X-Client").Single();
            observedBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            observedContentType = request.Content.Headers.ContentType?.MediaType;

            return new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent("created"),
            };
        }));

        var request = ApiRequest.Create("POST", "https://example.com/orders?existing=true") with
        {
            QueryParameters =
            [
                new RequestField("include", "items & totals"),
                new RequestField("include", "customer"),
            ],
            Headers = [new RequestField("X-Client", "ReqMint")],
            Body = new ApiRequestBody("{\"name\":\"Sample\"}", "application/json"),
        };

        await executor.ExecuteAsync(request);

        Assert.Equal("POST", observedMethod);
        Assert.Equal(
            "https://example.com/orders?existing=true&include=items%20%26%20totals&include=customer",
            observedUri?.AbsoluteUri);
        Assert.Equal("ReqMint", observedHeader);
        Assert.Equal("{\"name\":\"Sample\"}", observedBody);
        Assert.Equal("application/json", observedContentType);
    }

    [Fact]
    public async Task ExecuteAsync_EncodesEnabledFormFields()
    {
        string? observedBody = null;
        string? observedContentType = null;
        using var executor = new HttpRequestExecutor(new AsyncStubHandler(async (request, cancellationToken) =>
        {
            observedBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            observedContentType = request.Content.Headers.ContentType?.MediaType;
            return new HttpResponseMessage(HttpStatusCode.OK);
        }));
        var request = ApiRequest.Create("POST", "https://example.com/forms") with
        {
            Body = new ApiRequestBody(string.Empty, "application/x-www-form-urlencoded")
            {
                FormFields =
                [
                    new RequestField("name", "Mint & Co"),
                    new RequestField("ignored", "value", IsEnabled: false),
                    new RequestField("tag", "a/b"),
                ],
            },
        };

        await executor.ExecuteAsync(request);

        Assert.Equal("name=Mint+%26+Co&tag=a%2Fb", observedBody);
        Assert.Equal("application/x-www-form-urlencoded", observedContentType);
    }

    [Fact]
    public async Task ExecuteAsync_StreamsMultipartFieldsAndFiles()
    {
        var filePath = Path.GetTempFileName();
        await File.WriteAllTextAsync(filePath, "mint-file-content");
        try
        {
            string? observedBody = null;
            string? observedContentType = null;
            using var executor = new HttpRequestExecutor(new AsyncStubHandler(async (request, cancellationToken) =>
            {
                observedBody = await request.Content!.ReadAsStringAsync(cancellationToken);
                observedContentType = request.Content.Headers.ContentType?.MediaType;
                return new HttpResponseMessage(HttpStatusCode.OK);
            }));
            var request = ApiRequest.Create("POST", "https://example.com/upload") with
            {
                Body = new ApiRequestBody(string.Empty, "multipart/form-data")
                {
                    FormFields =
                    [
                        new RequestField("description", "ReqMint upload"),
                        new RequestField("ignored", "value", IsEnabled: false),
                    ],
                    FileFields =
                    [
                        new RequestFileField("attachment", "sample.txt")
                        {
                            LocalPath = filePath,
                        },
                    ],
                },
            };

            await executor.ExecuteAsync(request);

            Assert.Equal("multipart/form-data", observedContentType);
            Assert.Contains("name=description", observedBody, StringComparison.Ordinal);
            Assert.Contains("ReqMint upload", observedBody, StringComparison.Ordinal);
            Assert.Contains("name=attachment", observedBody, StringComparison.Ordinal);
            Assert.Contains("filename=sample.txt", observedBody, StringComparison.Ordinal);
            Assert.Contains("mint-file-content", observedBody, StringComparison.Ordinal);
            Assert.DoesNotContain("ignored", observedBody, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task ExecuteAsync_ThrowsTimeoutExceptionWhenRequestExceedsLimit()
    {
        using var executor = new HttpRequestExecutor(new AsyncStubHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }));

        var request = ApiRequest.Create("GET", "https://example.com/slow") with
        {
            Timeout = TimeSpan.FromMilliseconds(50),
        };

        await Assert.ThrowsAsync<TimeoutException>(() => executor.ExecuteAsync(request));
    }

    [Fact]
    public async Task ExecuteAsync_RejectsHeaderValuesContainingNewLines()
    {
        using var executor = new HttpRequestExecutor(new StubHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)));
        var request = ApiRequest.Create("GET", "https://example.com") with
        {
            Headers = [new RequestField("X-Client", "ReqMint\r\nX-Injected: true")],
        };

        await Assert.ThrowsAsync<ArgumentException>(() => executor.ExecuteAsync(request));
    }

    [Fact]
    public async Task ExecuteAsync_RejectsInvalidHeaderNames()
    {
        using var executor = new HttpRequestExecutor(new StubHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)));
        var request = ApiRequest.Create("GET", "https://example.com") with
        {
            Headers = [new RequestField("Invalid Header", "value")],
        };

        await Assert.ThrowsAsync<ArgumentException>(() => executor.ExecuteAsync(request));
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => Task.FromResult(responder(request));
    }

    private sealed class AsyncStubHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => responder(request, cancellationToken);
    }

    private sealed class CookieLoopbackServer : IAsyncDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _cancellation = new();
        private readonly Task _serverTask;

        public CookieLoopbackServer()
        {
            _listener.Start();
            var endpoint = (IPEndPoint)_listener.LocalEndpoint;
            BaseUri = new Uri($"http://127.0.0.1:{endpoint.Port}/");
            _serverTask = RunAsync();
        }

        public Uri BaseUri { get; }

        public async ValueTask DisposeAsync()
        {
            await _cancellation.CancelAsync();
            _listener.Stop();
            try
            {
                await _serverTask;
            }
            catch (OperationCanceledException)
            {
            }

            _cancellation.Dispose();
        }

        private async Task RunAsync()
        {
            while (!_cancellation.IsCancellationRequested)
            {
                using var client = await _listener.AcceptTcpClientAsync(_cancellation.Token);
                await RespondAsync(client, _cancellation.Token);
            }
        }

        private static async Task RespondAsync(TcpClient client, CancellationToken cancellationToken)
        {
            await using var stream = client.GetStream();
            using var reader = new StreamReader(
                stream,
                Encoding.ASCII,
                detectEncodingFromByteOrderMarks: false,
                leaveOpen: true);
            var requestLine = await reader.ReadLineAsync(cancellationToken) ?? string.Empty;
            string? cookieHeader = null;
            while (await reader.ReadLineAsync(cancellationToken) is { Length: > 0 } header)
            {
                if (header.StartsWith("Cookie:", StringComparison.OrdinalIgnoreCase))
                {
                    cookieHeader = header["Cookie:".Length..].Trim();
                }
            }

            var setsCookie = requestLine.Contains(" /set ", StringComparison.Ordinal);
            var body = setsCookie ? "stored" : cookieHeader ?? string.Empty;
            var bodyBytes = Encoding.UTF8.GetBytes(body);
            var responseHeaders = "HTTP/1.1 200 OK\r\n"
                + "Content-Type: text/plain; charset=utf-8\r\n"
                + $"Content-Length: {bodyBytes.Length}\r\n"
                + (setsCookie ? "Set-Cookie: session=alpha; Path=/; HttpOnly\r\n" : string.Empty)
                + "Connection: close\r\n\r\n";
            await stream.WriteAsync(Encoding.ASCII.GetBytes(responseHeaders), cancellationToken);
            await stream.WriteAsync(bodyBytes, cancellationToken);
        }
    }
}

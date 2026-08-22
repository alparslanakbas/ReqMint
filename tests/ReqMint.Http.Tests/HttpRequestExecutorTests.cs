using System.Net;
using System.Text;
using ReqMint.Core.Requests;

namespace ReqMint.Http.Tests;

public class HttpRequestExecutorTests
{
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
}

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
        using var executor = new HttpRequestExecutor(
            new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("1234567890"),
            }),
            previewLimitBytes: 5);

        var response = await executor.ExecuteAsync(ApiRequest.Create("GET", "https://example.com/large"));

        Assert.Equal("12345", response.Body);
        Assert.True(response.IsBodyTruncated);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => Task.FromResult(responder(request));
    }
}

using ReqMint.Core.Requests;

namespace ReqMint.Core.Tests;

public class ApiRequestTests
{
    [Fact]
    public void Create_NormalizesMethodAndUrl()
    {
        var request = ApiRequest.Create(" get ", "https://example.com/orders/42");

        Assert.Equal("GET", request.Method);
        Assert.Equal(new Uri("https://example.com/orders/42"), request.Url);
        Assert.Equal(TimeSpan.FromSeconds(30), request.Timeout);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-url")]
    [InlineData("ftp://example.com/file")]
    public void Create_RejectsInvalidOrUnsupportedUrls(string url) =>
        Assert.Throws<ArgumentException>(() => ApiRequest.Create("GET", url));
}

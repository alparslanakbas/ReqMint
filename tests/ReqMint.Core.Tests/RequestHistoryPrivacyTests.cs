using ReqMint.Core.History;
using ReqMint.Core.Requests;
using ReqMint.Core.Workspaces;

namespace ReqMint.Core.Tests;

public sealed class RequestHistoryPrivacyTests
{
    [Fact]
    public void CreateSafeSnapshot_RemovesBodiesAndRedactsSensitiveValues()
    {
        var request = new RequestDocument
        {
            Id = Guid.NewGuid(),
            Name = "Private request",
            Method = "POST",
            Url = "https://api.example.com/items?preview=true&access_token=top-secret#result",
            QueryParameters =
            [
                new RequestField("page", "2"),
                new RequestField("api_key", "secret-key"),
            ],
            Headers =
            [
                new RequestField("Accept", "application/json"),
                new RequestField("Authorization", "Bearer top-secret"),
            ],
            Body = new ApiRequestBody("{\"password\":\"secret\"}", "application/json"),
        };

        var snapshot = RequestHistoryPrivacy.CreateSafeSnapshot(request);

        Assert.Equal(
            "https://api.example.com/items?preview=true&access_token=%5Bredacted%5D#result",
            snapshot.Url);
        Assert.Equal("2", snapshot.QueryParameters[0].Value);
        Assert.Equal(RequestHistoryPrivacy.RedactedValue, snapshot.QueryParameters[1].Value);
        Assert.Equal("application/json", snapshot.Headers[0].Value);
        Assert.Equal(RequestHistoryPrivacy.RedactedValue, snapshot.Headers[1].Value);
        Assert.Null(snapshot.Body);
    }
}

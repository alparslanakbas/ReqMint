namespace ReqMint.Core.Requests;

public sealed record ApiResponse(
    int StatusCode,
    string ReasonPhrase,
    IReadOnlyDictionary<string, IReadOnlyList<string>> Headers,
    string Body,
    string? ContentType,
    TimeSpan Duration,
    bool IsBodyTruncated)
{
    public bool IsSuccessStatusCode => StatusCode is >= 200 and <= 299;
}

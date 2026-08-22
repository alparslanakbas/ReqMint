namespace ReqMint.Legacy.Core;

public interface IApiAccess
{
    Task<(string jsonResponse, bool isDownloadable)> CallApiAsync(
        string url,
        string content,
        HttpAction action = HttpAction.GET,
        bool formatOutput = true);

    Task<(string jsonResponse, bool isDownloadable)> CallApiAsync(
        string url,
        HttpContent? content = null,
        HttpAction action = HttpAction.GET,
        bool formatOutput = true);

    bool IsValidUrl(string url);
}

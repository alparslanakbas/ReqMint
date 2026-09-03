namespace ReqMint.Core.Requests;

public interface IRequestExecutor
{
    Task<ApiResponse> ExecuteAsync(ApiRequest request, CancellationToken cancellationToken = default);
}

public interface IRequestCookieManager
{
    bool IsEnabled { get; }

    void SetEnabled(bool enabled);

    void SelectWorkspace(string? workspaceDirectory);

    void ClearActiveWorkspace();
}

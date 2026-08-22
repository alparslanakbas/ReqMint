namespace ReqMint.Core.Requests;

public interface IRequestExecutor
{
    Task<ApiResponse> ExecuteAsync(ApiRequest request, CancellationToken cancellationToken = default);
}

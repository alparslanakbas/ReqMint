using ReqMint.Core.Security;

namespace ReqMint.Platform.Security;

internal sealed class UnavailableSecretVault : ISecretVault
{
    private const string Message =
        "Secure secret storage is not available on this platform yet. " +
        "ReqMint will not use a plaintext fallback.";

    public Task<string?> GetAsync(
        SecretReference reference,
        CancellationToken cancellationToken = default) =>
        Task.FromException<string?>(new SecretVaultUnavailableException(Message));

    public Task SetAsync(
        SecretReference reference,
        string value,
        CancellationToken cancellationToken = default) =>
        Task.FromException(new SecretVaultUnavailableException(Message));

    public Task DeleteAsync(
        SecretReference reference,
        CancellationToken cancellationToken = default) =>
        Task.FromException(new SecretVaultUnavailableException(Message));
}

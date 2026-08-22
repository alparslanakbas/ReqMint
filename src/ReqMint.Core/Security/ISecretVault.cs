namespace ReqMint.Core.Security;

public interface ISecretVault
{
    Task<string?> GetAsync(
        SecretReference reference,
        CancellationToken cancellationToken = default);

    Task SetAsync(
        SecretReference reference,
        string value,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        SecretReference reference,
        CancellationToken cancellationToken = default);
}

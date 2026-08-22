namespace ReqMint.Core.Security;

public sealed class SecretVaultUnavailableException : Exception
{
    public SecretVaultUnavailableException(string message)
        : base(message)
    {
    }
}

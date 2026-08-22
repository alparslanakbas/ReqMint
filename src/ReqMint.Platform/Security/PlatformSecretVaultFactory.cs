using ReqMint.Core.Security;

namespace ReqMint.Platform.Security;

public static class PlatformSecretVaultFactory
{
    public static ISecretVault Create() => OperatingSystem.IsWindows()
        ? new WindowsCredentialVault()
        : new UnavailableSecretVault();
}

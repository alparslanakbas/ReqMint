using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using ReqMint.Core.Security;

namespace ReqMint.Platform.Security;

internal sealed partial class WindowsCredentialVault : ISecretVault
{
    private const uint CredentialTypeGeneric = 1;
    private const uint CredentialPersistLocalMachine = 2;
    private const int ErrorNotFound = 1168;
    private const int MaximumCredentialBytes = 2560;

    public Task<string?> GetAsync(
        SecretReference reference,
        CancellationToken cancellationToken = default)
    {
        EnsureWindows();
        cancellationToken.ThrowIfCancellationRequested();
        ValidateReference(reference);

        var targetName = CreateTargetName(reference);
        if (!CredRead(targetName, CredentialTypeGeneric, 0, out var credentialPointer))
        {
            var error = Marshal.GetLastWin32Error();
            if (error == ErrorNotFound)
            {
                return Task.FromResult<string?>(null);
            }

            throw new Win32Exception(error, "Could not read the ReqMint secret.");
        }

        try
        {
            var credential = Marshal.PtrToStructure<NativeCredential>(credentialPointer);
            if (credential.CredentialBlobSize == 0)
            {
                return Task.FromResult<string?>(string.Empty);
            }

            var secretBytes = new byte[credential.CredentialBlobSize];
            try
            {
                Marshal.Copy(credential.CredentialBlob, secretBytes, 0, secretBytes.Length);
                return Task.FromResult<string?>(Encoding.UTF8.GetString(secretBytes));
            }
            finally
            {
                CryptographicOperations.ZeroMemory(secretBytes);
            }
        }
        finally
        {
            CredFree(credentialPointer);
        }
    }

    public Task SetAsync(
        SecretReference reference,
        string value,
        CancellationToken cancellationToken = default)
    {
        EnsureWindows();
        ArgumentNullException.ThrowIfNull(value);
        cancellationToken.ThrowIfCancellationRequested();
        ValidateReference(reference);

        var secretBytes = Encoding.UTF8.GetBytes(value);
        if (secretBytes.Length > MaximumCredentialBytes)
        {
            CryptographicOperations.ZeroMemory(secretBytes);
            throw new ArgumentException(
                $"Secret values cannot exceed {MaximumCredentialBytes} UTF-8 bytes.",
                nameof(value));
        }

        var blobPointer = Marshal.AllocHGlobal(secretBytes.Length);
        try
        {
            if (secretBytes.Length > 0)
            {
                Marshal.Copy(secretBytes, 0, blobPointer, secretBytes.Length);
            }

            var credential = new NativeCredential
            {
                Type = CredentialTypeGeneric,
                TargetName = CreateTargetName(reference),
                CredentialBlobSize = (uint)secretBytes.Length,
                CredentialBlob = blobPointer,
                Persist = CredentialPersistLocalMachine,
                UserName = "ReqMint",
            };

            if (!CredWrite(ref credential, 0))
            {
                throw new Win32Exception(
                    Marshal.GetLastWin32Error(),
                    "Could not store the ReqMint secret.");
            }

            return Task.CompletedTask;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secretBytes);
            ZeroAndFree(blobPointer, secretBytes.Length);
        }
    }

    public Task DeleteAsync(
        SecretReference reference,
        CancellationToken cancellationToken = default)
    {
        EnsureWindows();
        cancellationToken.ThrowIfCancellationRequested();
        ValidateReference(reference);

        if (!CredDelete(CreateTargetName(reference), CredentialTypeGeneric, 0))
        {
            var error = Marshal.GetLastWin32Error();
            if (error != ErrorNotFound)
            {
                throw new Win32Exception(error, "Could not delete the ReqMint secret.");
            }
        }

        return Task.CompletedTask;
    }

    private static string CreateTargetName(SecretReference reference)
    {
        var variableHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(reference.VariableName)));
        return $"ReqMint:{reference.WorkspaceId:N}:{reference.EnvironmentId:N}:{variableHash}";
    }

    private static void ValidateReference(SecretReference reference)
    {
        ArgumentNullException.ThrowIfNull(reference);

        if (reference.WorkspaceId == Guid.Empty || reference.EnvironmentId == Guid.Empty)
        {
            throw new ArgumentException("Secret workspace and environment IDs cannot be empty.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(reference.VariableName);
    }

    private static void EnsureWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "Windows Credential Manager is only available on Windows.");
        }
    }

    private static void ZeroAndFree(IntPtr pointer, int length)
    {
        if (pointer == IntPtr.Zero)
        {
            return;
        }

        if (length > 0)
        {
            Marshal.Copy(new byte[length], 0, pointer, length);
        }

        Marshal.FreeHGlobal(pointer);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NativeCredential
    {
        public uint Flags;
        public uint Type;
        public string TargetName;
        public string? Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public string? TargetAlias;
        public string UserName;
    }

    [DllImport("Advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredRead(
        string target,
        uint type,
        uint flags,
        out IntPtr credentialPointer);

    [DllImport("Advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredWrite(ref NativeCredential credential, uint flags);

    [DllImport("Advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredDelete(string target, uint type, uint flags);

    [DllImport("Advapi32.dll")]
    private static extern void CredFree(IntPtr buffer);
}

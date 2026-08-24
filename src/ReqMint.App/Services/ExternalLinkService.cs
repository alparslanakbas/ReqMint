using System.ComponentModel;
using System.Diagnostics;

namespace ReqMint.App.Services;

public interface IExternalLinkService
{
    Task<bool> OpenAsync(Uri uri);
}

public sealed class DesktopExternalLinkService : IExternalLinkService
{
    public Task<bool> OpenAsync(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        if (!uri.IsAbsoluteUri
            || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || !string.IsNullOrEmpty(uri.UserInfo))
        {
            return Task.FromResult(false);
        }

        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = uri.AbsoluteUri,
                UseShellExecute = true,
            });
            return Task.FromResult(process is not null);
        }
        catch (Exception exception) when (exception is Win32Exception
            or InvalidOperationException
            or NotSupportedException)
        {
            return Task.FromResult(false);
        }
    }
}

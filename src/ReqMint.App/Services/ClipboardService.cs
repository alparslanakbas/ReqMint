using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Input.Platform;

namespace ReqMint.App.Services;

public interface IClipboardService
{
    Task<bool> SetTextAsync(string text);
}

public sealed class AvaloniaClipboardService(TopLevel topLevel) : IClipboardService
{
    public async Task<bool> SetTextAsync(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        var clipboard = topLevel.Clipboard;
        if (clipboard is null)
        {
            return false;
        }

        try
        {
            await clipboard.SetTextAsync(text);
            await clipboard.FlushAsync();
            return true;
        }
        catch (Exception exception) when (exception is InvalidOperationException
            or NotSupportedException
            or ExternalException)
        {
            return false;
        }
    }
}

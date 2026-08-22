using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace ReqMint.App.Services;

public sealed class AvaloniaWorkspaceFolderPicker(Window owner) : IWorkspaceFolderPicker
{
    public async Task<string?> PickFolderAsync(string title)
    {
        var folders = await owner.StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions
            {
                Title = title,
                AllowMultiple = false,
            });

        return folders.Count == 1 ? folders[0].TryGetLocalPath() : null;
    }
}

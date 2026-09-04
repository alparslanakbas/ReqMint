using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace ReqMint.App.Services;

public sealed class AvaloniaRequestFilePicker(
    Window owner,
    LocalizationService localization) : IRequestFilePicker
{
    public async Task<PickedRequestFile?> PickAsync()
    {
        var files = await owner.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = localization.GetString("MultipartFilePickerTitle") ?? "Choose a file to upload",
                AllowMultiple = false,
            });
        if (files.Count != 1 || files[0].TryGetLocalPath() is not { } path)
        {
            return null;
        }

        return new PickedRequestFile(files[0].Name, Path.GetFullPath(path));
    }
}

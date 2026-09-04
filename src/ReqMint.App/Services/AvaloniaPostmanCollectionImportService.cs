using System.Text;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using ReqMint.Core.Importing;

namespace ReqMint.App.Services;

public sealed class AvaloniaPostmanCollectionImportService(
    Window owner,
    LocalizationService localization) : IPostmanCollectionImportService
{
    private const int MaximumFileBytes = 16 * 1024 * 1024;

    public async Task<PostmanCollectionSource?> PickAsync(
        CancellationToken cancellationToken = default)
    {
        var files = await owner.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = localization.GetString("PostmanImportPickerTitle")
                    ?? "Import a Postman Collection v2.1",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("Postman Collection JSON")
                    {
                        Patterns = ["*.json"],
                        MimeTypes = ["application/json"],
                    },
                ],
            });
        if (files.Count != 1)
        {
            return null;
        }

        await using var source = await files[0].OpenReadAsync();
        if (source.CanSeek && source.Length > MaximumFileBytes)
        {
            throw new PostmanImportException("The Postman collection is larger than 16 MiB.");
        }

        using var destination = new MemoryStream();
        var buffer = new byte[81920];
        while (true)
        {
            var read = await source.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                break;
            }

            if (destination.Length + read > MaximumFileBytes)
            {
                throw new PostmanImportException("The Postman collection is larger than 16 MiB.");
            }

            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        string content;
        try
        {
            content = new UTF8Encoding(false, true).GetString(destination.ToArray());
        }
        catch (DecoderFallbackException exception)
        {
            throw new PostmanImportException("The Postman collection must use UTF-8 encoding.", exception);
        }

        return new PostmanCollectionSource(files[0].Name, content);
    }
}

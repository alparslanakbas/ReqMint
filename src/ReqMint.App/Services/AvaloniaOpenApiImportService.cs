using System.Text;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using ReqMint.Core.Importing;

namespace ReqMint.App.Services;

public sealed class AvaloniaOpenApiImportService(
    Window owner,
    LocalizationService localization) : IOpenApiImportService
{
    private const int MaximumFileBytes = 16 * 1024 * 1024;

    public async Task<OpenApiDocumentSource?> PickAsync(CancellationToken cancellationToken = default)
    {
        var files = await owner.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = localization.GetString("OpenApiImportPickerTitle") ?? "Import an OpenAPI document",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("OpenAPI JSON or YAML")
                    {
                        Patterns = ["*.json", "*.yaml", "*.yml"],
                        MimeTypes = ["application/json", "application/yaml", "text/yaml"],
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
            throw new OpenApiImportException("The OpenAPI document is larger than 16 MiB.");
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
                throw new OpenApiImportException("The OpenAPI document is larger than 16 MiB.");
            }
            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        try
        {
            return new OpenApiDocumentSource(
                files[0].Name,
                new UTF8Encoding(false, true).GetString(destination.ToArray()));
        }
        catch (DecoderFallbackException exception)
        {
            throw new OpenApiImportException("The OpenAPI document must use UTF-8 encoding.", exception);
        }
    }
}

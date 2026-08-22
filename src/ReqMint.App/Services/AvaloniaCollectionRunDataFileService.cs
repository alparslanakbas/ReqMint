using System.Text;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using ReqMint.Core.Runner;

namespace ReqMint.App.Services;

public sealed class AvaloniaCollectionRunDataFileService(
    Window owner,
    LocalizationService localization) : ICollectionRunDataFileService
{
    public async Task<CollectionRunDataFile?> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        var files = await owner.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = localization.GetString("CollectionRunDataPickerTitle")
                    ?? "Choose JSON or CSV run data",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType(
                        localization.GetString("CollectionRunDataFileType")
                            ?? "JSON or CSV run data")
                    {
                        Patterns = ["*.json", "*.csv"],
                        MimeTypes = ["application/json", "text/csv"],
                    },
                ],
            });
        if (files.Count != 1)
        {
            return null;
        }

        var file = files[0];
        var format = Path.GetExtension(file.Name).ToLowerInvariant() switch
        {
            ".json" => CollectionRunDataFormat.Json,
            ".csv" => CollectionRunDataFormat.Csv,
            _ => throw new CollectionRunDataException(
                "Only JSON and CSV run-data files are supported."),
        };
        await using var source = await file.OpenReadAsync();
        var bytes = await ReadBoundedAsync(source, cancellationToken);
        string content;
        try
        {
            content = new UTF8Encoding(
                encoderShouldEmitUTF8Identifier: false,
                throwOnInvalidBytes: true).GetString(bytes);
        }
        catch (DecoderFallbackException exception)
        {
            throw new CollectionRunDataException(
                "Run-data files must use UTF-8 encoding.",
                exception);
        }

        return new CollectionRunDataFile(
            file.Name,
            CollectionRunDataParser.Parse(content, format));
    }

    private static async Task<byte[]> ReadBoundedAsync(
        Stream source,
        CancellationToken cancellationToken)
    {
        if (source.CanSeek && source.Length > CollectionRunDataParser.MaximumFileBytes)
        {
            throw new CollectionRunDataException("The run-data file is too large.");
        }

        using var destination = new MemoryStream();
        var buffer = new byte[81920];
        while (true)
        {
            var read = await source.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                return destination.ToArray();
            }

            if (destination.Length + read > CollectionRunDataParser.MaximumFileBytes)
            {
                throw new CollectionRunDataException("The run-data file is too large.");
            }

            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
    }
}

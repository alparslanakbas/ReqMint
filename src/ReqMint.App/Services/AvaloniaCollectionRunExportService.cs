using Avalonia.Controls;
using Avalonia.Platform.Storage;
using ReqMint.Core.Runner;

namespace ReqMint.App.Services;

public sealed class AvaloniaCollectionRunExportService(
    Window owner,
    ICollectionRunResultExporter exporter,
    LocalizationService localization) : ICollectionRunExportService
{
    public async Task<bool> ExportAsync(
        CollectionRunResult result,
        CollectionRunExportFormat format,
        string suggestedFileName,
        CancellationToken cancellationToken = default)
    {
        var isJson = format == CollectionRunExportFormat.Json;
        var file = await owner.StorageProvider.SaveFilePickerAsync(
            new FilePickerSaveOptions
            {
                Title = isJson
                    ? localization.GetString("CollectionRunExportJsonTitle")
                        ?? "Export ReqMint JSON report"
                    : localization.GetString("CollectionRunExportJUnitTitle")
                        ?? "Export ReqMint JUnit report",
                SuggestedFileName = suggestedFileName,
                DefaultExtension = isJson ? "json" : "xml",
                ShowOverwritePrompt = true,
                FileTypeChoices =
                [
                    new FilePickerFileType(isJson
                        ? localization.GetString("CollectionRunJsonFileType") ?? "JSON report"
                        : localization.GetString("CollectionRunJUnitFileType") ?? "JUnit XML report")
                    {
                        Patterns = isJson ? ["*.json"] : ["*.xml"],
                        MimeTypes = isJson ? ["application/json"] : ["application/xml", "text/xml"],
                    },
                ],
            });
        if (file is null)
        {
            return false;
        }

        await using var stream = await file.OpenWriteAsync();
        if (stream.CanSeek)
        {
            stream.SetLength(0);
        }

        await exporter.ExportAsync(result, stream, format, cancellationToken);
        await stream.FlushAsync(cancellationToken);
        return true;
    }
}

using ReqMint.Core.Runner;

namespace ReqMint.App.Services;

public interface ICollectionRunExportService
{
    Task<bool> ExportAsync(
        CollectionRunResult result,
        CollectionRunExportFormat format,
        string suggestedFileName,
        CancellationToken cancellationToken = default);
}

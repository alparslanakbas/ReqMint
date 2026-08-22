using ReqMint.Core.Runner;

namespace ReqMint.App.Services;

public interface ICollectionRunDataFileService
{
    Task<CollectionRunDataFile?> LoadAsync(CancellationToken cancellationToken = default);
}

public sealed record CollectionRunDataFile(
    string FileName,
    CollectionRunDataSet DataSet);

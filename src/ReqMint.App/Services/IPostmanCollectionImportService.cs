namespace ReqMint.App.Services;

public sealed record PostmanCollectionSource(string FileName, string Content);

public interface IPostmanCollectionImportService
{
    Task<PostmanCollectionSource?> PickAsync(CancellationToken cancellationToken = default);
}

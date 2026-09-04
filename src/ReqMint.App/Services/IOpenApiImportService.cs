namespace ReqMint.App.Services;

public sealed record OpenApiDocumentSource(string FileName, string Content);

public interface IOpenApiImportService
{
    Task<OpenApiDocumentSource?> PickAsync(CancellationToken cancellationToken = default);
}

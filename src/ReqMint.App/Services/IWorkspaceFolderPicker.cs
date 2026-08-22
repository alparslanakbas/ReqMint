namespace ReqMint.App.Services;

public interface IWorkspaceFolderPicker
{
    Task<string?> PickFolderAsync(string title);
}

namespace ReqMint.App.Services;

public interface IUnsavedChangesPrompt
{
    Task<UnsavedChangesChoice> ShowAsync(string requestName, bool canSave);
}

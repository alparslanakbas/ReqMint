namespace ReqMint.App.Services;

public interface IHistoryClearPrompt
{
    Task<bool> ShowAsync(string workspaceName, int entryCount);
}

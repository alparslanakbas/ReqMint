namespace ReqMint.App.Services;

public interface ICollectionRunHistoryClearPrompt
{
    Task<bool> ShowAsync(string collectionName, int entryCount);
}

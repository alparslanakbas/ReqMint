namespace ReqMint.App.Services;

/// <summary>
/// Confirms deleting a saved request. Deleting is irreversible once the
/// workspace is written, so it always goes through a prompt.
/// </summary>
public interface IRequestDeletePrompt
{
    Task<bool> ShowAsync(string requestName);
}

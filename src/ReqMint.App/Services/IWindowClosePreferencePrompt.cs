namespace ReqMint.App.Services;

public interface IWindowClosePreferencePrompt
{
    Task<WindowClosePromptResult?> ShowAsync(CancellationToken cancellationToken = default);
}

public sealed record WindowClosePromptResult(
    WindowCloseBehavior Behavior,
    bool RememberChoice);

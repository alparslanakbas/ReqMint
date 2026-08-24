namespace ReqMint.App.Services;

public sealed class WindowCloseCoordinator(
    IAppSettingsService settings,
    IWindowClosePreferencePrompt prompt)
{
    public async Task<WindowCloseDecision> DecideAsync(
        CancellationToken cancellationToken = default)
    {
        var configuredBehavior = settings.Current.WindowCloseBehavior;
        if (configuredBehavior != WindowCloseBehavior.Ask)
        {
            return Map(configuredBehavior);
        }

        var result = await prompt.ShowAsync(cancellationToken);
        if (result is null)
        {
            return WindowCloseDecision.Cancel;
        }

        if (result.Behavior == WindowCloseBehavior.Ask)
        {
            throw new InvalidOperationException("A close prompt must choose KeepRunning or Exit.");
        }

        if (result.RememberChoice)
        {
            settings.Update(settings.Current with { WindowCloseBehavior = result.Behavior });
        }

        return Map(result.Behavior);
    }

    private static WindowCloseDecision Map(WindowCloseBehavior behavior) => behavior switch
    {
        WindowCloseBehavior.KeepRunning => WindowCloseDecision.Hide,
        WindowCloseBehavior.Exit => WindowCloseDecision.Exit,
        _ => WindowCloseDecision.Cancel,
    };
}

public enum WindowCloseDecision
{
    Cancel,
    Hide,
    Exit,
}

using ReqMint.App.Services;

namespace ReqMint.App.Tests;

public sealed class WindowCloseCoordinatorTests
{
    [Fact]
    public async Task DecideAsync_RemembersKeepRunningChoiceWhenRequested()
    {
        var settings = new StubSettings();
        var prompt = new StubPrompt(new WindowClosePromptResult(
            WindowCloseBehavior.KeepRunning,
            RememberChoice: true));
        var coordinator = new WindowCloseCoordinator(settings, prompt);

        var decision = await coordinator.DecideAsync();

        Assert.Equal(WindowCloseDecision.Hide, decision);
        Assert.Equal(WindowCloseBehavior.KeepRunning, settings.Current.WindowCloseBehavior);
        Assert.Equal(1, prompt.CallCount);
    }

    [Fact]
    public async Task DecideAsync_DoesNotPersistOneTimeChoice()
    {
        var settings = new StubSettings();
        var prompt = new StubPrompt(new WindowClosePromptResult(
            WindowCloseBehavior.Exit,
            RememberChoice: false));
        var coordinator = new WindowCloseCoordinator(settings, prompt);

        var decision = await coordinator.DecideAsync();

        Assert.Equal(WindowCloseDecision.Exit, decision);
        Assert.Equal(WindowCloseBehavior.Ask, settings.Current.WindowCloseBehavior);
    }

    [Theory]
    [InlineData(WindowCloseBehavior.KeepRunning, WindowCloseDecision.Hide)]
    [InlineData(WindowCloseBehavior.Exit, WindowCloseDecision.Exit)]
    public async Task DecideAsync_UsesConfiguredBehaviorWithoutPrompt(
        WindowCloseBehavior behavior,
        WindowCloseDecision expected)
    {
        var settings = new StubSettings(new AppSettings { WindowCloseBehavior = behavior });
        var prompt = new StubPrompt(null);
        var coordinator = new WindowCloseCoordinator(settings, prompt);

        var decision = await coordinator.DecideAsync();

        Assert.Equal(expected, decision);
        Assert.Equal(0, prompt.CallCount);
    }

    [Fact]
    public async Task DecideAsync_CancelsWhenPromptIsDismissed()
    {
        var coordinator = new WindowCloseCoordinator(
            new StubSettings(),
            new StubPrompt(null));

        var decision = await coordinator.DecideAsync();

        Assert.Equal(WindowCloseDecision.Cancel, decision);
    }

    private sealed class StubSettings(AppSettings? initial = null) : IAppSettingsService
    {
        public AppSettings Current { get; private set; } = initial ?? new AppSettings();

        public void Update(AppSettings settings) => Current = settings;
    }

    private sealed class StubPrompt(WindowClosePromptResult? result) : IWindowClosePreferencePrompt
    {
        public int CallCount { get; private set; }

        public Task<WindowClosePromptResult?> ShowAsync(CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(result);
        }
    }
}

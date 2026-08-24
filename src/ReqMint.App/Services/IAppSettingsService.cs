namespace ReqMint.App.Services;

public interface IAppSettingsService
{
    AppSettings Current { get; }

    void Update(AppSettings settings);
}

public sealed record AppSettings
{
    public string? Language { get; init; }

    public string Theme { get; init; } = ThemeCatalog.DefaultId;

    public int HistoryRetentionLimit { get; init; } = 200;

    public int CollectionRunHistoryRetentionLimit { get; init; } = 50;

    public int ResponsePreviewLimitMegabytes { get; init; } = 2;

    public OnboardingStatus OnboardingStatus { get; init; }

    public int OnboardingStep { get; init; }

    public WindowCloseBehavior WindowCloseBehavior { get; init; }
}

public enum OnboardingStatus
{
    NotStarted,
    InProgress,
    Completed,
    Skipped,
}

public enum WindowCloseBehavior
{
    Ask,
    KeepRunning,
    Exit,
}

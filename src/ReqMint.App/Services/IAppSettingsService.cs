namespace ReqMint.App.Services;

public interface IAppSettingsService
{
    AppSettings Current { get; }

    void Update(AppSettings settings);
}

public sealed record AppSettings
{
    public string? Language { get; init; }

    public int HistoryRetentionLimit { get; init; } = 200;

    public int CollectionRunHistoryRetentionLimit { get; init; } = 50;

    public int ResponsePreviewLimitMegabytes { get; init; } = 2;
}

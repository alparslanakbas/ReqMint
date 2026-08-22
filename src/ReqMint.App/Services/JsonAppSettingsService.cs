using System.Text.Json;

namespace ReqMint.App.Services;

public sealed class JsonAppSettingsService : IAppSettingsService
{
    public const int MinimumHistoryRetentionLimit = 25;
    public const int MaximumHistoryRetentionLimit = 2000;
    public const int MinimumCollectionRunHistoryRetentionLimit = 10;
    public const int MaximumCollectionRunHistoryRetentionLimit = 200;
    public const int MinimumResponsePreviewLimitMegabytes = 1;
    public const int MaximumResponsePreviewLimitMegabytes = 20;

    private readonly string _settingsPath;

    public JsonAppSettingsService(string settingsDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(settingsDirectory);
        _settingsPath = Path.Combine(Path.GetFullPath(settingsDirectory), "ui-settings.json");
        Current = Load();
    }

    public AppSettings Current { get; private set; }

    public void Update(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var normalized = Normalize(settings);
        var directory = Path.GetDirectoryName(_settingsPath)!;
        var temporaryPath = $"{_settingsPath}.tmp-{Guid.NewGuid():N}";

        try
        {
            Directory.CreateDirectory(directory);
            using (var stream = File.Create(temporaryPath))
            {
                JsonSerializer.Serialize(stream, normalized);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, _settingsPath, overwrite: true);
            Current = normalized;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException)
        {
            // Settings persistence is best-effort and must never prevent the app from running.
        }
        finally
        {
            TryDeleteTemporaryFile(temporaryPath);
        }
    }

    private AppSettings Load()
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                return new AppSettings();
            }

            using var stream = File.OpenRead(_settingsPath);
            return Normalize(JsonSerializer.Deserialize<AppSettings>(stream) ?? new AppSettings());
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return new AppSettings();
        }
    }

    private static AppSettings Normalize(AppSettings settings) => settings with
    {
        HistoryRetentionLimit = Math.Clamp(
            settings.HistoryRetentionLimit,
            MinimumHistoryRetentionLimit,
            MaximumHistoryRetentionLimit),
        CollectionRunHistoryRetentionLimit = Math.Clamp(
            settings.CollectionRunHistoryRetentionLimit,
            MinimumCollectionRunHistoryRetentionLimit,
            MaximumCollectionRunHistoryRetentionLimit),
        ResponsePreviewLimitMegabytes = Math.Clamp(
            settings.ResponsePreviewLimitMegabytes,
            MinimumResponsePreviewLimitMegabytes,
            MaximumResponsePreviewLimitMegabytes),
    };

    private static void TryDeleteTemporaryFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }
}

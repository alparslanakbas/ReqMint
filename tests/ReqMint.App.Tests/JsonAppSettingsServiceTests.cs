using System.Text.Json;
using ReqMint.App.Services;

namespace ReqMint.App.Tests;

public sealed class JsonAppSettingsServiceTests
{
    [Fact]
    public void Update_RoundTripsLanguageAndHistoryRetention()
    {
        using var directory = new TemporaryDirectory();
        var store = new JsonAppSettingsService(directory.Path);

        store.Update(new AppSettings
        {
            Language = "tr",
            HistoryRetentionLimit = 350,
            CollectionRunHistoryRetentionLimit = 80,
            ResponsePreviewLimitMegabytes = 7,
            OnboardingStatus = OnboardingStatus.InProgress,
            OnboardingStep = 2,
        });
        var reloaded = new JsonAppSettingsService(directory.Path);

        Assert.Equal("tr", reloaded.Current.Language);
        Assert.Equal(350, reloaded.Current.HistoryRetentionLimit);
        Assert.Equal(80, reloaded.Current.CollectionRunHistoryRetentionLimit);
        Assert.Equal(7, reloaded.Current.ResponsePreviewLimitMegabytes);
        Assert.Equal(OnboardingStatus.InProgress, reloaded.Current.OnboardingStatus);
        Assert.Equal(2, reloaded.Current.OnboardingStep);
    }

    [Theory]
    [InlineData(1, JsonAppSettingsService.MinimumHistoryRetentionLimit)]
    [InlineData(5000, JsonAppSettingsService.MaximumHistoryRetentionLimit)]
    public void LoadingSettings_ClampsInvalidHistoryRetention(int value, int expected)
    {
        using var directory = new TemporaryDirectory();
        var path = System.IO.Path.Combine(directory.Path, "ui-settings.json");
        File.WriteAllText(path, JsonSerializer.Serialize(new AppSettings
        {
            Language = "en",
            HistoryRetentionLimit = value,
        }));

        var store = new JsonAppSettingsService(directory.Path);

        Assert.Equal(expected, store.Current.HistoryRetentionLimit);
    }

    [Theory]
    [InlineData(0, JsonAppSettingsService.MinimumResponsePreviewLimitMegabytes)]
    [InlineData(50, JsonAppSettingsService.MaximumResponsePreviewLimitMegabytes)]
    public void LoadingSettings_ClampsInvalidResponsePreviewLimit(int value, int expected)
    {
        using var directory = new TemporaryDirectory();
        var path = System.IO.Path.Combine(directory.Path, "ui-settings.json");
        File.WriteAllText(path, JsonSerializer.Serialize(new AppSettings
        {
            ResponsePreviewLimitMegabytes = value,
        }));

        var store = new JsonAppSettingsService(directory.Path);

        Assert.Equal(expected, store.Current.ResponsePreviewLimitMegabytes);
    }

    [Theory]
    [InlineData(1, JsonAppSettingsService.MinimumCollectionRunHistoryRetentionLimit)]
    [InlineData(500, JsonAppSettingsService.MaximumCollectionRunHistoryRetentionLimit)]
    public void LoadingSettings_ClampsInvalidCollectionRunHistoryRetention(
        int value,
        int expected)
    {
        using var directory = new TemporaryDirectory();
        var path = System.IO.Path.Combine(directory.Path, "ui-settings.json");
        File.WriteAllText(path, JsonSerializer.Serialize(new AppSettings
        {
            CollectionRunHistoryRetentionLimit = value,
        }));

        var store = new JsonAppSettingsService(directory.Path);

        Assert.Equal(expected, store.Current.CollectionRunHistoryRetentionLimit);
    }

    [Fact]
    public void LoadingSettings_NormalizesInvalidOnboardingProgress()
    {
        using var directory = new TemporaryDirectory();
        var path = System.IO.Path.Combine(directory.Path, "ui-settings.json");
        File.WriteAllText(path, JsonSerializer.Serialize(new AppSettings
        {
            OnboardingStatus = (OnboardingStatus)999,
            OnboardingStep = 999,
        }));

        var store = new JsonAppSettingsService(directory.Path);

        Assert.Equal(OnboardingStatus.NotStarted, store.Current.OnboardingStatus);
        Assert.Equal(
            JsonAppSettingsService.MaximumOnboardingStep,
            store.Current.OnboardingStep);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "ReqMint.Tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}

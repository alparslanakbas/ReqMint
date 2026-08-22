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

        store.Update(new AppSettings { Language = "tr", HistoryRetentionLimit = 350 });
        var reloaded = new JsonAppSettingsService(directory.Path);

        Assert.Equal("tr", reloaded.Current.Language);
        Assert.Equal(350, reloaded.Current.HistoryRetentionLimit);
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

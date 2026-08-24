using ReqMint.App.Services;

namespace ReqMint.App.Tests;

public sealed class ThemeServiceTests
{
    [Fact]
    public void Catalog_ContainsFourteenUniqueThemes()
    {
        Assert.Equal(14, ThemeCatalog.All.Count);
        Assert.Equal(14, ThemeCatalog.All.Select(theme => theme.Id).Distinct().Count());
        Assert.Equal(ThemeCatalog.DefaultId, ThemeCatalog.Default.Id);
    }

    [Fact]
    public void Selection_IsRestoredAndPersisted()
    {
        var settings = new StubSettings(new AppSettings { Theme = "midnight" });
        var service = new ThemeService(settings);

        Assert.Equal("midnight", service.SelectedTheme.Id);

        service.SelectedTheme = ThemeCatalog.Find("titanium-frost")!;

        Assert.Equal("titanium-frost", settings.Current.Theme);
    }

    private sealed class StubSettings(AppSettings initial) : IAppSettingsService
    {
        public AppSettings Current { get; private set; } = initial;

        public void Update(AppSettings settings) => Current = settings;
    }
}

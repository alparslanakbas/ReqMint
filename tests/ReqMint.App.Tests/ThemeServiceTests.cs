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
    public void Catalog_MeetsCoreTextAndFocusContrastThresholds()
    {
        var failures = new List<string>();

        foreach (var theme in ThemeCatalog.All)
        {
            RequireContrast(failures, theme, "primary/app", theme.TextPrimary, theme.AppBackground, 4.5);
            RequireContrast(failures, theme, "primary/surface", theme.TextPrimary, theme.Surface, 4.5);
            RequireContrast(failures, theme, "primary/raised", theme.TextPrimary, theme.SurfaceRaised, 4.5);
            RequireContrast(failures, theme, "secondary/app", theme.TextSecondary, theme.AppBackground, 4.5);
            RequireContrast(failures, theme, "secondary/surface", theme.TextSecondary, theme.Surface, 4.5);
            RequireContrast(failures, theme, "accent/app focus", theme.Accent, theme.AppBackground, 3.0);
            RequireContrast(failures, theme, "accent/surface focus", theme.Accent, theme.Surface, 3.0);
            RequireContrast(failures, theme, "primary action", theme.AppBackground, theme.Accent, 4.5);
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
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

    private static void RequireContrast(
        ICollection<string> failures,
        ThemeOption theme,
        string usage,
        string foreground,
        string background,
        double minimum)
    {
        var ratio = ContrastRatio(foreground, background);
        if (ratio < minimum)
        {
            failures.Add($"{theme.Id}: {usage} is {ratio:F2}:1; expected at least {minimum:F1}:1");
        }
    }

    private static double ContrastRatio(string first, string second)
    {
        var lighter = Math.Max(RelativeLuminance(first), RelativeLuminance(second));
        var darker = Math.Min(RelativeLuminance(first), RelativeLuminance(second));
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double RelativeLuminance(string color)
    {
        var value = color.AsSpan().TrimStart('#');
        if (value.Length == 8)
        {
            value = value[2..];
        }

        var red = Convert.ToByte(value[..2].ToString(), 16) / 255d;
        var green = Convert.ToByte(value.Slice(2, 2).ToString(), 16) / 255d;
        var blue = Convert.ToByte(value.Slice(4, 2).ToString(), 16) / 255d;
        return 0.2126 * Linearize(red) + 0.7152 * Linearize(green) + 0.0722 * Linearize(blue);
    }

    private static double Linearize(double channel) =>
        channel <= 0.04045
            ? channel / 12.92
            : Math.Pow((channel + 0.055) / 1.055, 2.4);
}

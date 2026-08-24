namespace ReqMint.App.Tests;

public sealed class ComboBoxThemeContractTests
{
    [Fact]
    public void ComboBoxes_UseReqMintThemeTokensForPopupStates()
    {
        var app = File.ReadAllText(RepositoryPath("src", "ReqMint.App", "App.axaml"));
        var themeService = File.ReadAllText(
            RepositoryPath("src", "ReqMint.App", "Services", "ThemeService.cs"));

        Assert.Contains("Style Selector=\"ComboBoxItem\"", app, StringComparison.Ordinal);
        Assert.Contains("ComboBoxDropDownBackground", themeService, StringComparison.Ordinal);
        Assert.Contains("ComboBoxItemBackgroundPointerOver", themeService, StringComparison.Ordinal);
        Assert.Contains("ComboBoxItemBackgroundSelected", themeService, StringComparison.Ordinal);
        Assert.Contains("ComboBoxItemBorderBrushSelected", themeService, StringComparison.Ordinal);
        Assert.Contains("theme.SurfaceRaised", themeService, StringComparison.Ordinal);
        Assert.Contains("theme.SurfaceHover", themeService, StringComparison.Ordinal);
        Assert.Contains("theme.AccentMuted", themeService, StringComparison.Ordinal);
        Assert.Contains("theme.Accent", themeService, StringComparison.Ordinal);
    }

    private static string RepositoryPath(params string[] segments)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "ReqMint.slnx")))
        {
            current = current.Parent;
        }

        Assert.NotNull(current);
        return Path.Combine([current.FullName, .. segments]);
    }
}

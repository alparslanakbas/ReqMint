using Avalonia.Media;
using ReqMint.App.Services;

namespace ReqMint.App.Tests;

/// <summary>
/// The very first complaint about the interface was that every HTTP method
/// looked the same. Method colours are drawn from each theme's palette, and a
/// theme can easily hand the same hue to two roles, so this guards the whole
/// class of collision rather than the one instance that was reported.
/// </summary>
public sealed class MethodColourContractTests
{
    /// <summary>
    /// Monochrome is greyscale on purpose: colour is not what separates its
    /// methods, the method label is. It still has to use five distinct shades.
    /// </summary>
    private const string GreyscaleThemeId = "monochrome";

    private const double MinimumSeparation = 60;

    [Fact]
    public void EveryTheme_GivesTheHttpMethodsDistinctColours()
    {
        foreach (var theme in ThemeCatalog.All)
        {
            var colours = MethodColours(theme);
            var names = colours.Keys.ToArray();

            for (var i = 0; i < names.Length; i++)
            {
                for (var j = i + 1; j < names.Length; j++)
                {
                    Assert.False(
                        string.Equals(
                            colours[names[i]],
                            colours[names[j]],
                            StringComparison.OrdinalIgnoreCase),
                        $"{theme.DisplayName}: {names[i]} and {names[j]} share {colours[names[i]]}.");
                }
            }
        }
    }

    [Fact]
    public void ChromaticThemes_KeepTheMethodColoursFarEnoughApart()
    {
        foreach (var theme in ThemeCatalog.All.Where(theme => theme.Id != GreyscaleThemeId))
        {
            var colours = MethodColours(theme);
            var names = colours.Keys.ToArray();

            for (var i = 0; i < names.Length; i++)
            {
                for (var j = i + 1; j < names.Length; j++)
                {
                    var distance = Distance(colours[names[i]], colours[names[j]]);
                    Assert.True(
                        distance >= MinimumSeparation,
                        $"{theme.DisplayName}: {names[i]} and {names[j]} are only "
                            + $"{distance:N0} apart ({colours[names[i]]} / {colours[names[j]]}).");
                }
            }
        }
    }

    private static Dictionary<string, string> MethodColours(ThemeOption theme) => new()
    {
        ["GET"] = theme.MethodGet,
        ["PUT"] = theme.MethodPut,
        ["PATCH"] = theme.MethodPatch ?? theme.Accent,
        ["POST"] = theme.MethodPost ?? theme.Warning,
        ["DELETE"] = theme.MethodDelete ?? theme.DiffRemoved,
    };

    private static double Distance(string first, string second)
    {
        var left = Color.Parse(first);
        var right = Color.Parse(second);
        var red = left.R - right.R;
        var green = left.G - right.G;
        var blue = left.B - right.B;

        return Math.Sqrt((red * red) + (green * green) + (blue * blue));
    }
}

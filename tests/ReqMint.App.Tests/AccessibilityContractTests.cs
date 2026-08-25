using System.Xml.Linq;

namespace ReqMint.App.Tests;

public sealed class AccessibilityContractTests
{
    [Fact]
    public void KeyboardFocus_UsesTheActiveThemeAccent()
    {
        var app = File.ReadAllText(RepositoryPath("src", "ReqMint.App", "App.axaml"));

        Assert.Contains(
            "Button:focus, TextBox:focus, ComboBox:focus, TabItem:focus",
            app,
            StringComparison.Ordinal);
        Assert.Contains("Button.rail:focus", app, StringComparison.Ordinal);
        Assert.Contains(
            "<Setter Property=\"BorderBrush\" Value=\"{DynamicResource AccentBrush}\" />",
            app,
            StringComparison.Ordinal);
    }

    [Fact]
    public void IconOnlyButtons_HaveLocalizedAutomationNames()
    {
        var document = XDocument.Load(
            RepositoryPath("src", "ReqMint.App", "Views", "MainWindow.axaml"));
        var symbolOnlyContent = new HashSet<string>(StringComparer.Ordinal)
        {
            "▣＋",
            "＋",
            "⌫",
            "↻",
            "×",
        };

        var iconOnlyButtons = document
            .Descendants()
            .Where(element => element.Name.LocalName == "Button")
            .Where(button =>
                symbolOnlyContent.Contains(button.Attribute("Content")?.Value ?? string.Empty)
                || button.Elements().Any(child => child.Name.LocalName == "PathIcon"))
            .ToArray();

        Assert.NotEmpty(iconOnlyButtons);
        Assert.All(iconOnlyButtons, button => Assert.Contains(
            button.Attributes(),
            attribute => attribute.Name.LocalName == "AutomationProperties.Name"
                         && attribute.Value.StartsWith("{DynamicResource ", StringComparison.Ordinal)));
    }

    [Fact]
    public void PrimaryWorkspaceControls_HaveLocalizedAutomationNames()
    {
        var view = File.ReadAllText(
            RepositoryPath("src", "ReqMint.App", "Views", "MainWindow.axaml"));

        foreach (var key in new[]
                 {
                     "TooltipOpenWorkspace",
                     "TextSearch",
                     "TooltipActiveEnvironment",
                 })
        {
            Assert.Contains(
                $"AutomationProperties.Name=\"{{DynamicResource {key}}}\"",
                view,
                StringComparison.Ordinal);
        }
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

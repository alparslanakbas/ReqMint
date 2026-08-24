namespace ReqMint.App.Tests;

public sealed class RequestEditorTabStyleContractTests
{
    [Fact]
    public void RequestEditorTabs_UseTheDedicatedVisualStyle()
    {
        var app = File.ReadAllText(RepositoryPath("src", "ReqMint.App", "App.axaml"));
        var view = File.ReadAllText(
            RepositoryPath("src", "ReqMint.App", "Views", "MainWindow.axaml"));

        Assert.Contains("TabItem.requestEditorTab", app, StringComparison.Ordinal);
        Assert.Contains("TabItem.requestEditorTab:pointerover", app, StringComparison.Ordinal);
        Assert.Contains("TabItem.requestEditorTab:selected", app, StringComparison.Ordinal);
        Assert.Contains("{DynamicResource AccentBrush}", app, StringComparison.Ordinal);
        Assert.Equal(5, CountOccurrences(view, "Classes=\"requestEditorTab\""));
    }

    private static int CountOccurrences(string value, string search)
    {
        var count = 0;
        var index = 0;
        while ((index = value.IndexOf(search, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += search.Length;
        }

        return count;
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

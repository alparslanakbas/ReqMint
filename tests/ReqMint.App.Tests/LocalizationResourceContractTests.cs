using System.Text.Json;

namespace ReqMint.App.Tests;

public sealed class LocalizationResourceContractTests
{
    [Fact]
    public void EnglishAndTurkishResources_HaveMatchingKeys()
    {
        var english = ReadResources("en");
        var turkish = ReadResources("tr");

        Assert.Equal(
            english.Keys.Order(StringComparer.Ordinal),
            turkish.Keys.Order(StringComparer.Ordinal));
        Assert.Equal("Response", english["TextResponse"]);
        Assert.Equal("Yanıt", turkish["TextResponse"]);
        Assert.Equal("Copy", english["TextCopy"]);
        Assert.Equal("Kopyala", turkish["TextCopy"]);
    }

    [Fact]
    public void ResponseHeader_UsesLocalizedResources()
    {
        var view = File.ReadAllText(
            RepositoryPath("src", "ReqMint.App", "Views", "MainWindow.axaml"));

        Assert.Contains("{DynamicResource TextResponse}", view, StringComparison.Ordinal);
        Assert.Contains("{DynamicResource TextCopy}", view, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"Response\"", view, StringComparison.Ordinal);
        Assert.DoesNotContain("Content=\"Copy\"", view, StringComparison.Ordinal);
    }

    [Fact]
    public void EnvironmentEditor_UsesLocalizedResources()
    {
        var view = File.ReadAllText(
            RepositoryPath("src", "ReqMint.App", "Views", "MainWindow.axaml"));

        foreach (var key in new[]
                 {
                     "TextVariableName",
                     "TextValue",
                     "TextSecretValue",
                     "TextSecret",
                     "TextSecretStorageHelp",
                 })
        {
            Assert.Contains($"{{DynamicResource {key}}}", view, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("PlaceholderText=\"Variable name\"", view, StringComparison.Ordinal);
        Assert.DoesNotContain("PlaceholderText=\"Secret value\"", view, StringComparison.Ordinal);
        Assert.DoesNotContain("Content=\"Secret\"", view, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadyResponseState_UsesLocalizedResources()
    {
        var viewModel = File.ReadAllText(
            RepositoryPath("src", "ReqMint.App", "ViewModels", "MainViewModel.cs"));

        Assert.Contains(
            "ResponseStatus = Localize(\"StatusReady\", \"Ready\")",
            viewModel,
            StringComparison.Ordinal);
        Assert.Contains("\"ResponseInspectRequest\"", viewModel, StringComparison.Ordinal);
        Assert.Contains("\"ResponseComposeNewRequest\"", viewModel, StringComparison.Ordinal);
        Assert.Contains("\"ResponseInspectSavedRequest\"", viewModel, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "ResponseStatus = \"Ready\"",
            viewModel,
            StringComparison.Ordinal);
    }

    private static IReadOnlyDictionary<string, string> ReadResources(string language)
    {
        var path = RepositoryPath(
            "src",
            "ReqMint.App",
            "Localization",
            $"{language}.json");
        return JsonSerializer.Deserialize<Dictionary<string, string>>(
            File.ReadAllText(path))!;
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

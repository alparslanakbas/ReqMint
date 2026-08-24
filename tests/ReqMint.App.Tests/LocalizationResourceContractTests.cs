using System.Text.Json;
using ReqMint.App.Services;

namespace ReqMint.App.Tests;

public sealed class LocalizationResourceContractTests
{
    [Fact]
    public void LanguageMetadata_DetectsArabicRtlAndSimplifiedChineseLtr()
    {
        var arabic = new LanguageOption("ar", "العربية", "ar");
        var simplifiedChinese = new LanguageOption("zh-Hans", "简体中文", "zh-CN");

        Assert.True(arabic.IsRightToLeft);
        Assert.False(simplifiedChinese.IsRightToLeft);
    }

    [Fact]
    public void AllLocalizationResources_HaveMatchingKeysAndNonEmptyValues()
    {
        var english = ReadResources("en");
        var turkish = ReadResources("tr");
        var localizationDirectory = RepositoryPath("src", "ReqMint.App", "Localization");

        foreach (var path in Directory.GetFiles(localizationDirectory, "*.json"))
        {
            var resources = JsonSerializer.Deserialize<Dictionary<string, string>>(
                File.ReadAllText(path))!;

            Assert.Equal(
                english.Keys.Order(StringComparer.Ordinal),
                resources.Keys.Order(StringComparer.Ordinal));
            Assert.DoesNotContain(resources, resource =>
                string.IsNullOrWhiteSpace(resource.Value));
        }

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

    [Fact]
    public void MainWindow_MirrorsWithTheLanguageAndKeepsTechnicalContentLtr()
    {
        var view = File.ReadAllText(
            RepositoryPath("src", "ReqMint.App", "Views", "MainWindow.axaml"));
        var styles = File.ReadAllText(
            RepositoryPath("src", "ReqMint.App", "App.axaml"));

        Assert.Contains(
            "FlowDirection=\"{Binding Localization.FlowDirection}\"",
            view,
            StringComparison.Ordinal);
        Assert.Contains("Classes=\"technical\"", view, StringComparison.Ordinal);
        Assert.Contains("Selector=\"TextBox.technical\"", styles, StringComparison.Ordinal);
        Assert.Contains("Selector=\"TextBlock.technical\"", styles, StringComparison.Ordinal);
        Assert.Contains(
            "<Setter Property=\"FlowDirection\" Value=\"LeftToRight\" />",
            styles,
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

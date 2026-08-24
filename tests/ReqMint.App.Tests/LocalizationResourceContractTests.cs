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

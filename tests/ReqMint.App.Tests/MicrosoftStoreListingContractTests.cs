using System.Text.Json;

namespace ReqMint.App.Tests;

public sealed class MicrosoftStoreListingContractTests
{
    private static readonly string[] ExpectedLanguages = ["en-US", "tr-TR"];

    [Fact]
    public void Listings_CoverSupportedLanguagesAndPartnerCenterLimits()
    {
        foreach (var language in ExpectedLanguages)
        {
            using var document = JsonDocument.Parse(File.ReadAllText(ListingPath(language)));
            var listing = document.RootElement;

            Assert.Equal(language, RequiredString(listing, "language"));
            Assert.Equal("ReqMint", RequiredString(listing, "productName"));
            Assert.Equal("ReqMint", RequiredString(listing, "shortTitle"));
            Assert.InRange(RequiredString(listing, "shortTitle").Length, 1, 50);
            Assert.InRange(RequiredString(listing, "shortDescription").Length, 1, 270);

            var description = RequiredString(listing, "description");
            Assert.InRange(description.Length, 1, 10_000);
            Assert.DoesNotContain("http://", description, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("https://", description, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain('<', description);
            Assert.DoesNotContain('>', description);

            var whatsNew = listing.GetProperty("whatsNew").GetString();
            Assert.NotNull(whatsNew);
            Assert.Empty(whatsNew);
            Assert.InRange(whatsNew.Length, 0, 1_500);

            var features = RequiredStrings(listing, "features");
            Assert.InRange(features.Length, 1, 20);
            Assert.All(features, feature =>
            {
                Assert.InRange(feature.Length, 1, 200);
                Assert.False(feature.StartsWith('-') || feature.StartsWith('•'));
            });

            var keywords = RequiredStrings(listing, "keywords");
            Assert.InRange(keywords.Length, 1, 7);
            Assert.All(keywords, keyword => Assert.InRange(keyword.Length, 1, 40));
            Assert.InRange(keywords.Sum(WordCount), 1, 21);

            Assert.InRange(RequiredString(listing, "copyrightAndTrademark").Length, 1, 200);
            Assert.InRange(RequiredString(listing, "developedBy").Length, 1, 255);

            AssertHttpsUrl(listing, "websiteUrl", "/");
            AssertHttpsUrl(listing, "privacyPolicyUrl", "/privacy");
            AssertHttpsUrl(listing, "supportUrl", "/support");

            var captions = RequiredStrings(listing, "screenshotCaptions");
            Assert.InRange(captions.Length, 4, 10);
            Assert.All(captions, caption => Assert.InRange(caption.Length, 1, 200));
        }
    }

    [Fact]
    public void SubmissionGuide_BlocksPrivateWebsiteAndDocumentsPrivatePreviewOrder()
    {
        var guide = File.ReadAllText(RepositoryPath("docs", "MICROSOFT_STORE_LISTING.md"));

        Assert.Contains("Private audience", guide, StringComparison.Ordinal);
        Assert.Contains("Do not submit these URLs while anonymous visitors receive an access prompt", guide, StringComparison.Ordinal);
        Assert.Contains("What's new", guide, StringComparison.Ordinal);
        Assert.Contains("Windows App Certification Kit", guide, StringComparison.Ordinal);
    }

    [Fact]
    public void StoreArtworkTools_GenerateRequiredTileAndValidateLocalizedScreenshots()
    {
        var artworkGenerator = File.ReadAllText(
            RepositoryPath("eng", "New-WindowsStoreListingAssets.ps1"));
        var screenshotValidator = File.ReadAllText(
            RepositoryPath("eng", "Test-WindowsStoreScreenshots.ps1"));

        Assert.Contains("ReqMint-Store-Tile-300x300.png", artworkGenerator, StringComparison.Ordinal);
        Assert.Contains("$size = 300", artworkGenerator, StringComparison.Ordinal);
        Assert.Contains("@('en-US', 'tr-TR')", screenshotValidator, StringComparison.Ordinal);
        Assert.Contains("01-request-builder.png", screenshotValidator, StringComparison.Ordinal);
        Assert.Contains("05-settings-support.png", screenshotValidator, StringComparison.Ordinal);
        Assert.Contains("$maximumFileSize = 50MB", screenshotValidator, StringComparison.Ordinal);
        Assert.Contains("$minimumWidth = 1366", screenshotValidator, StringComparison.Ordinal);
        Assert.Contains("$minimumHeight = 768", screenshotValidator, StringComparison.Ordinal);
    }

    private static string ListingPath(string language) =>
        RepositoryPath("packaging", "windows", "store-listing", $"{language}.json");

    private static string RequiredString(JsonElement element, string propertyName)
    {
        var value = element.GetProperty(propertyName).GetString();
        Assert.False(string.IsNullOrWhiteSpace(value));
        return value;
    }

    private static string[] RequiredStrings(JsonElement element, string propertyName) =>
        element.GetProperty(propertyName)
            .EnumerateArray()
            .Select(item => item.GetString())
            .Select(item =>
            {
                Assert.False(string.IsNullOrWhiteSpace(item));
                return item!;
            })
            .ToArray();

    private static int WordCount(string value) =>
        value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;

    private static void AssertHttpsUrl(JsonElement listing, string propertyName, string expectedPath)
    {
        var value = RequiredString(listing, propertyName);
        Assert.True(Uri.TryCreate(value, UriKind.Absolute, out var uri));
        Assert.NotNull(uri);
        Assert.Equal(Uri.UriSchemeHttps, uri.Scheme);
        Assert.Equal("reqmint.alparslanayt.chatgpt.site", uri.Host);
        Assert.Equal(expectedPath, uri.AbsolutePath);
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

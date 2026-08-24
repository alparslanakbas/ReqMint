using System.Xml.Linq;

namespace ReqMint.App.Tests;

public sealed class WindowsPackagingContractTests
{
    private static readonly XNamespace Foundation =
        "http://schemas.microsoft.com/appx/manifest/foundation/windows10";

    private static readonly XNamespace Uap =
        "http://schemas.microsoft.com/appx/manifest/uap/windows10";

    private static readonly XNamespace RestrictedCapabilities =
        "http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities";

    [Fact]
    public void ManifestTemplate_DefinesStoreReadyFullTrustDesktopPackage()
    {
        var manifest = XDocument.Load(RepositoryPath("packaging", "windows", "AppxManifest.xml.in"));
        var package = Assert.IsType<XElement>(manifest.Root);
        var identity = Assert.IsType<XElement>(package.Element(Foundation + "Identity"));
        var application = Assert.Single(
            Assert.IsType<XElement>(package.Element(Foundation + "Applications"))
                .Elements(Foundation + "Application"));

        Assert.Equal("{{IDENTITY_NAME}}", (string?)identity.Attribute("Name"));
        Assert.Equal("{{PUBLISHER}}", (string?)identity.Attribute("Publisher"));
        Assert.Equal("{{VERSION}}", (string?)identity.Attribute("Version"));
        Assert.Equal("{{ARCHITECTURE}}", (string?)identity.Attribute("ProcessorArchitecture"));
        Assert.Equal("ReqMint.App.exe", (string?)application.Attribute("Executable"));
        Assert.Equal("Windows.FullTrustApplication", (string?)application.Attribute("EntryPoint"));

        var deviceFamily = Assert.IsType<XElement>(
            Assert.IsType<XElement>(package.Element(Foundation + "Dependencies"))
                .Element(Foundation + "TargetDeviceFamily"));
        Assert.Equal("Windows.Desktop", (string?)deviceFamily.Attribute("Name"));

        var capability = Assert.Single(
            Assert.IsType<XElement>(package.Element(Foundation + "Capabilities"))
                .Elements(RestrictedCapabilities + "Capability"));
        Assert.Equal("runFullTrust", (string?)capability.Attribute("Name"));

        var visualElements = Assert.IsType<XElement>(application.Element(Uap + "VisualElements"));
        Assert.Equal("ReqMint", (string?)visualElements.Attribute("DisplayName"));
        Assert.Equal(@"Assets\Square150x150Logo.png", (string?)visualElements.Attribute("Square150x150Logo"));
        Assert.Equal(@"Assets\Square44x44Logo.png", (string?)visualElements.Attribute("Square44x44Logo"));
    }

    [Fact]
    public void Workflow_InjectsPublicStoreIdentityFromRepositoryVariables()
    {
        var workflow = File.ReadAllText(RepositoryPath(".github", "workflows", "windows-msix.yml"));

        Assert.Contains("REQMINT_STORE_IDENTITY_NAME", workflow, StringComparison.Ordinal);
        Assert.Contains("REQMINT_STORE_PUBLISHER", workflow, StringComparison.Ordinal);
        Assert.Contains("REQMINT_STORE_PUBLISHER_DISPLAY_NAME", workflow, StringComparison.Ordinal);
        Assert.Contains("./eng/package-windows.ps1", workflow, StringComparison.Ordinal);
        Assert.Contains("actions/upload-artifact@v7", workflow, StringComparison.Ordinal);
    }

    private static string RepositoryPath(params string[] segments)
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);

        while (current is not null && !File.Exists(Path.Combine(current.FullName, "ReqMint.slnx")))
        {
            current = current.Parent;
        }

        if (current is null)
        {
            throw new InvalidOperationException("Could not locate the ReqMint repository root.");
        }

        return Path.Combine([current.FullName, .. segments]);
    }
}

using System.Xml.Linq;

namespace ReqMint.App.Tests;

public sealed class MacOsPackagingContractTests
{
    [Fact]
    public void InfoPlist_DefinesReqMintDesktopApplicationBundle()
    {
        var document = XDocument.Load(RepositoryPath("packaging", "macos", "Info.plist.in"));
        var dictionary = Assert.IsType<XElement>(document.Root?.Element("dict"));
        var entries = dictionary.Elements().ToArray();
        var values = new Dictionary<string, XElement>(StringComparer.Ordinal);

        for (var index = 0; index < entries.Length - 1; index += 2)
        {
            values.Add(entries[index].Value, entries[index + 1]);
        }

        Assert.Equal("ReqMint.App", values["CFBundleExecutable"].Value);
        Assert.Equal("com.alparslanakbas.reqmint", values["CFBundleIdentifier"].Value);
        Assert.Equal("APPL", values["CFBundlePackageType"].Value);
        Assert.Equal("ReqMint.icns", values["CFBundleIconFile"].Value);
        Assert.Equal("{{VERSION}}", values["CFBundleShortVersionString"].Value);
        Assert.Equal("{{BUILD_NUMBER}}", values["CFBundleVersion"].Value);
        Assert.Equal("14.0", values["LSMinimumSystemVersion"].Value);
    }

    [Fact]
    public void Workflow_BuildsAndValidatesBothMacArchitectures()
    {
        var workflow = File.ReadAllText(RepositoryPath(".github", "workflows", "macos-app.yml"));
        var script = File.ReadAllText(RepositoryPath("eng", "package-macos.sh"));

        Assert.Contains("runs-on: macos-latest", workflow, StringComparison.Ordinal);
        Assert.Contains("dotnet test ReqMint.slnx --configuration Release", workflow, StringComparison.Ordinal);
        Assert.Contains("- x64", workflow, StringComparison.Ordinal);
        Assert.Contains("- arm64", workflow, StringComparison.Ordinal);
        Assert.Contains("codesign --verify --deep --strict", workflow, StringComparison.Ordinal);
        Assert.Contains("./eng/package-macos.sh", workflow, StringComparison.Ordinal);
        Assert.Contains("--self-contained true", script, StringComparison.Ordinal);
        Assert.Contains("--options runtime --sign -", script, StringComparison.Ordinal);
        Assert.Contains("Developer ID signing and Apple notarization are still required", script, StringComparison.Ordinal);
        Assert.Contains("ditto -c -k --sequesterRsrc --keepParent", script, StringComparison.Ordinal);
        Assert.Contains("shasum -a 256", script, StringComparison.Ordinal);
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

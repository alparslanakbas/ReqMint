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
        Assert.Contains("Contents/Resources/ReqMint.App.dll", workflow, StringComparison.Ordinal);
        Assert.Contains("Contents/Frameworks/libcoreclr.dylib", workflow, StringComparison.Ordinal);
        Assert.Contains("./eng/package-macos.sh", workflow, StringComparison.Ordinal);
        Assert.Contains("--self-contained true", script, StringComparison.Ordinal);
        Assert.Contains("frameworks_directory=", script, StringComparison.Ordinal);
        Assert.Contains("ln -s \"../Frameworks/$file_name\"", script, StringComparison.Ordinal);
        Assert.Contains("ln -s \"../Resources/$file_name\"", script, StringComparison.Ordinal);
        Assert.Contains("nested_codesign_arguments=(--force --options runtime --sign -)", script, StringComparison.Ordinal);
        Assert.Contains("app_codesign_arguments=(--force --options runtime --sign - --entitlements", script, StringComparison.Ordinal);
        Assert.Contains("app_codesign_arguments=(--force --options runtime --timestamp --sign \"$signing_identity\" --entitlements", script, StringComparison.Ordinal);
        Assert.Contains("Developer ID signing and Apple notarization are still required", script, StringComparison.Ordinal);
        Assert.Contains("ditto -c -k --sequesterRsrc --keepParent", script, StringComparison.Ordinal);
        Assert.Contains("shasum -a 256", script, StringComparison.Ordinal);

        var entitlements = File.ReadAllText(
            RepositoryPath("packaging", "macos", "ReqMint.entitlements"));
        Assert.Contains("com.apple.security.cs.allow-jit", entitlements, StringComparison.Ordinal);
        Assert.DoesNotContain("com.apple.security.get-task-allow", entitlements, StringComparison.Ordinal);
    }

    [Fact]
    public void NotarizedWorkflow_FailsClosedAndRemovesTemporaryCredentials()
    {
        var workflow = File.ReadAllText(RepositoryPath(".github", "workflows", "macos-notarized.yml"));

        Assert.Contains("secrets.REQMINT_APPLE_CERTIFICATE_BASE64", workflow, StringComparison.Ordinal);
        Assert.Contains("secrets.REQMINT_APPLE_CERTIFICATE_PASSWORD", workflow, StringComparison.Ordinal);
        Assert.Contains("secrets.REQMINT_APPLE_SIGNING_IDENTITY", workflow, StringComparison.Ordinal);
        Assert.Contains("secrets.REQMINT_APPLE_NOTARY_KEY_BASE64", workflow, StringComparison.Ordinal);
        Assert.Contains("secrets.REQMINT_APPLE_NOTARY_KEY_ID", workflow, StringComparison.Ordinal);
        Assert.Contains("secrets.REQMINT_APPLE_NOTARY_ISSUER_ID", workflow, StringComparison.Ordinal);
        Assert.Contains("Configure the required Apple release secrets", workflow, StringComparison.Ordinal);
        Assert.Contains("Developer ID Application:", workflow, StringComparison.Ordinal);
        Assert.Contains("xcrun notarytool submit", workflow, StringComparison.Ordinal);
        Assert.Contains("xcrun stapler staple", workflow, StringComparison.Ordinal);
        Assert.Contains("spctl --assess", workflow, StringComparison.Ordinal);
        Assert.Contains("if: always()", workflow, StringComparison.Ordinal);
        Assert.Contains("security delete-keychain", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("vars.REQMINT_APPLE", workflow, StringComparison.Ordinal);
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

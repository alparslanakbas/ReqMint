namespace ReqMint.App.Tests;

public sealed class LinuxPackagingContractTests
{
    [Fact]
    public void PackagingScript_CreatesSelfContainedArchivesForSupportedArchitectures()
    {
        var script = File.ReadAllText(RepositoryPath("eng", "package-linux.sh"));

        Assert.Contains("x64|arm64", script, StringComparison.Ordinal);
        Assert.Contains("runtime_identifier=\"linux-$architecture\"", script, StringComparison.Ordinal);
        Assert.Contains("--self-contained true", script, StringComparison.Ordinal);
        Assert.Contains("-p:DebugSymbols=false", script, StringComparison.Ordinal);
        Assert.Contains("ReqMint.App", script, StringComparison.Ordinal);
        Assert.Contains("chmod 755", script, StringComparison.Ordinal);
        Assert.Contains("sha256sum", script, StringComparison.Ordinal);
        Assert.Contains(".tar.gz", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Workflow_TestsOnLinuxAndPublishesBothArchitectures()
    {
        var workflow = File.ReadAllText(RepositoryPath(".github", "workflows", "linux-portable.yml"));

        Assert.Contains("runs-on: ubuntu-latest", workflow, StringComparison.Ordinal);
        Assert.Contains("dotnet test ReqMint.slnx --configuration Release", workflow, StringComparison.Ordinal);
        Assert.Contains("- x64", workflow, StringComparison.Ordinal);
        Assert.Contains("- arm64", workflow, StringComparison.Ordinal);
        Assert.Contains("./eng/package-linux.sh", workflow, StringComparison.Ordinal);
        Assert.Contains("sha256sum --check", workflow, StringComparison.Ordinal);
        Assert.Matches("actions/upload-artifact@[0-9a-f]{40} # v7", workflow);
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

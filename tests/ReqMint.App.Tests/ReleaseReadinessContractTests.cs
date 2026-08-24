namespace ReqMint.App.Tests;

public sealed class ReleaseReadinessContractTests
{
    [Fact]
    public void QualityWorkflow_CoversSupportedPlatformsAndDependencyAudit()
    {
        var workflow = File.ReadAllText(RepositoryPath(".github", "workflows", "quality-gates.yml"));

        Assert.Contains("permissions:", workflow, StringComparison.Ordinal);
        Assert.Contains("contents: read", workflow, StringComparison.Ordinal);
        Assert.Contains("windows-latest", workflow, StringComparison.Ordinal);
        Assert.Contains("macos-latest", workflow, StringComparison.Ordinal);
        Assert.Contains("ubuntu-latest", workflow, StringComparison.Ordinal);
        Assert.Contains("dotnet test ReqMint.slnx --configuration Release --no-restore", workflow, StringComparison.Ordinal);
        Assert.Contains("package list --project ReqMint.slnx --vulnerable --include-transitive --format json --no-restore", workflow, StringComparison.Ordinal);
        Assert.Contains("known vulnerability", workflow, StringComparison.Ordinal);
    }

    [Fact]
    public void PublicPolicies_StateLocalFirstBehaviorAndPrivateSecurityReporting()
    {
        var privacy = File.ReadAllText(RepositoryPath("PRIVACY.md"));
        var security = File.ReadAllText(RepositoryPath("SECURITY.md"));
        var readiness = File.ReadAllText(RepositoryPath("docs", "RELEASE_READINESS.md"));

        Assert.Contains("does not require an account", privacy, StringComparison.Ordinal);
        Assert.Contains("does not provide a ReqMint-hosted synchronization service", privacy, StringComparison.Ordinal);
        Assert.Contains("private vulnerability reporting", security, StringComparison.Ordinal);
        Assert.Contains("Any failed required gate blocks publication", readiness, StringComparison.Ordinal);
        Assert.Contains("cannot be waived", readiness, StringComparison.Ordinal);
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

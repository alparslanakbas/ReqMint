using System.Text.RegularExpressions;

namespace ReqMint.App.Tests;

public sealed partial class WorkflowSecurityContractTests
{
    [Fact]
    public void Workflows_PinEveryExternalActionToACommitSha()
    {
        foreach (var workflowPath in Directory.EnumerateFiles(
            RepositoryPath(".github", "workflows"),
            "*.yml"))
        {
            var lines = File.ReadAllLines(workflowPath);
            foreach (var line in lines.Where(line => line.TrimStart().StartsWith("uses:", StringComparison.Ordinal)))
            {
                Assert.Matches(PinnedActionPattern(), line.Trim());
            }
        }
    }

    [Fact]
    public void Workflows_UseReadOnlyRepositoryPermissions()
    {
        foreach (var workflowPath in Directory.EnumerateFiles(
            RepositoryPath(".github", "workflows"),
            "*.yml"))
        {
            var workflow = File.ReadAllText(workflowPath);
            Assert.Contains("permissions:\n  contents: read", workflow, StringComparison.Ordinal);
            Assert.DoesNotContain(": write", workflow, StringComparison.Ordinal);
        }
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

    [GeneratedRegex("^uses:\\s+[^@\\s]+@[0-9a-f]{40}(?:\\s+#\\s+v[0-9]+)?$", RegexOptions.CultureInvariant)]
    private static partial Regex PinnedActionPattern();
}

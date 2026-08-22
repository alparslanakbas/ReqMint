using ReqMint.Core.Git;
using ReqMint.Platform.Git;

namespace ReqMint.Platform.Tests;

public sealed class WorkspaceGitSecretScannerTests
{
    [Fact]
    public async Task ScanAsync_AcceptsTemplatesAndEmptySecretDeclarations()
    {
        using var workspace = new TemporaryWorkspace();
        await workspace.WriteAsync(
            "environments/local.json",
            """
            {
              "variables": [
                { "name": "API_TOKEN", "value": null, "isSecret": true },
                { "name": "AUTHORIZATION", "value": "Bearer {{API_TOKEN}}" }
              ]
            }
            """);

        var result = await new WorkspaceGitSecretScanner().ScanAsync(
            workspace.Path,
            ["environments/local.json"]);

        Assert.Empty(result.Findings);
        Assert.True(result.IsComplete);
    }

    [Fact]
    public async Task ScanAsync_FindsPersistedSecretsWithoutReturningTheirValues()
    {
        using var workspace = new TemporaryWorkspace();
        const string secret = "never-return-this-value";
        await workspace.WriteAsync(
            "environments/local.json",
            $$"""
            {
              "variables": [
                { "name": "API_TOKEN", "value": "{{secret}}", "isSecret": true }
              ]
            }
            """);

        var result = await new WorkspaceGitSecretScanner().ScanAsync(
            workspace.Path,
            ["environments/local.json"]);

        var finding = Assert.Single(result.Findings);
        Assert.Equal(GitSecretFindingKind.PersistedSecretValue, finding.Kind);
        Assert.DoesNotContain(secret, finding.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ScanAsync_FindsSensitiveFieldsAndKnownCredentialFormats()
    {
        using var workspace = new TemporaryWorkspace();
        await workspace.WriteAsync(
            "collections/private.json",
            """
            {
              "headers": [{ "name": "Authorization", "value": "Bearer literal-token" }],
              "body": "ghp_123456789012345678901234567890"
            }
            """);

        var result = await new WorkspaceGitSecretScanner().ScanAsync(
            workspace.Path,
            ["collections/private.json"]);

        Assert.Contains(result.Findings, finding =>
            finding.Kind == GitSecretFindingKind.SensitiveNamedValue);
        Assert.Contains(result.Findings, finding =>
            finding.Kind == GitSecretFindingKind.CredentialPattern);
    }

    [Theory]
    [InlineData("{\"password\":\"literal-secret\"}", true)]
    [InlineData("password=literal-secret", true)]
    [InlineData("{\"password\":\"{{PASSWORD}}\"}", false)]
    public async Task ScanAsync_InspectsSensitiveAssignmentsInsideStringBodies(
        string body,
        bool shouldWarn)
    {
        using var workspace = new TemporaryWorkspace();
        var jsonBody = System.Text.Json.JsonSerializer.Serialize(body);
        await workspace.WriteAsync(
            "collections/body.json",
            $$"""{ "body": {{jsonBody}} }""");

        var result = await new WorkspaceGitSecretScanner().ScanAsync(
            workspace.Path,
            ["collections/body.json"]);

        Assert.Equal(shouldWarn, result.HasWarnings);
    }

    [Fact]
    public async Task ScanAsync_FailsClosedForMalformedManagedFiles()
    {
        using var workspace = new TemporaryWorkspace();
        await workspace.WriteAsync("collections/broken.json", "{ not-json");

        var result = await new WorkspaceGitSecretScanner().ScanAsync(
            workspace.Path,
            ["collections/broken.json"]);

        Assert.Empty(result.Findings);
        Assert.False(result.IsComplete);
        Assert.Equal("collections/broken.json", Assert.Single(result.UnscannedFiles));
    }

    private sealed class TemporaryWorkspace : IDisposable
    {
        public TemporaryWorkspace()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "ReqMint.Platform.Tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public async Task WriteAsync(string relativePath, string content)
        {
            var fullPath = System.IO.Path.Combine(
                Path,
                relativePath.Replace('/', System.IO.Path.DirectorySeparatorChar));
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(fullPath)!);
            await File.WriteAllTextAsync(fullPath, content);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}

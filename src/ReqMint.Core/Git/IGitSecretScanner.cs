namespace ReqMint.Core.Git;

public interface IGitSecretScanner
{
    Task<GitSecretScanResult> ScanAsync(
        string workspaceDirectory,
        IReadOnlyList<string> workspaceRelativePaths,
        CancellationToken cancellationToken = default);
}

public sealed record GitSecretScanResult
{
    public static GitSecretScanResult Empty { get; } = new();

    public IReadOnlyList<GitSecretFinding> Findings { get; init; } = [];

    public IReadOnlyList<string> UnscannedFiles { get; init; } = [];

    public bool HasWarnings => Findings.Count > 0;

    public bool IsComplete => UnscannedFiles.Count == 0;
}

public sealed record GitSecretFinding(
    string Path,
    string Location,
    GitSecretFindingKind Kind);

public enum GitSecretFindingKind
{
    PersistedSecretValue,
    SensitiveNamedValue,
    CredentialPattern,
}

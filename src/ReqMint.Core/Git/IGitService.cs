namespace ReqMint.Core.Git;

public interface IGitService
{
    Task<GitRepositoryStatus?> GetStatusAsync(
        string workspaceDirectory,
        CancellationToken cancellationToken = default);

    Task<GitDiffPreview> GetDiffAsync(
        string workspaceDirectory,
        string workspaceRelativePath,
        GitDiffScope scope,
        CancellationToken cancellationToken = default);

    Task<GitStageResult> StageFileAsync(
        string workspaceDirectory,
        string workspaceRelativePath,
        CancellationToken cancellationToken = default);
}

public sealed record GitRepositoryStatus
{
    public required string RepositoryRoot { get; init; }

    public required string Branch { get; init; }

    public bool IsDetached { get; init; }

    public int AheadBy { get; init; }

    public int BehindBy { get; init; }

    public IReadOnlyList<GitFileChange> Changes { get; init; } = [];

    public bool IsClean => Changes.Count == 0;
}

public sealed record GitFileChange(string Path, string Status)
{
    private static readonly HashSet<string> ConflictStatuses = new(StringComparer.Ordinal)
    {
        "DD", "AU", "UD", "UA", "DU", "AA", "UU",
    };

    public bool HasStagedChanges =>
        Status.Length > 0 && Status[0] is not (' ' or '?' or '!');

    public bool HasWorkingTreeChanges =>
        Status == "??" || (Status.Length > 1 && Status[1] != ' ');

    public bool IsConflict => ConflictStatuses.Contains(Status);

    public bool IsStageCandidate =>
        HasWorkingTreeChanges && !HasStagedChanges && !IsConflict;
}

public sealed record GitStageResult
{
    public required string Path { get; init; }

    public GitStageResultState State { get; init; } = GitStageResultState.Staged;

    public int SecurityWarningCount { get; init; }

    public int UnscannedFileCount { get; init; }
}

public enum GitStageResultState
{
    Staged,
    BlockedBySecurity,
    NotEligible,
}

public sealed record GitDiffPreview
{
    public required string Path { get; init; }

    public required GitDiffScope Scope { get; init; }

    public GitDiffPreviewState State { get; init; } = GitDiffPreviewState.Available;

    public string Content { get; init; } = string.Empty;

    public bool IsTruncated { get; init; }

    public int SecurityWarningCount { get; init; }

    public int UnscannedFileCount { get; init; }
}

public enum GitDiffScope
{
    WorkingTree,
    Staged,
}

public enum GitDiffPreviewState
{
    Available,
    BlockedBySecurity,
    Unavailable,
}

public sealed class GitUnavailableException(string message, Exception? innerException = null)
    : Exception(message, innerException);

public sealed class GitCommandException(string message) : Exception(message);

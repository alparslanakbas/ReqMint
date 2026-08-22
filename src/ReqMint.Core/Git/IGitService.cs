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

    Task<GitCommitPreflight> GetCommitPreflightAsync(
        string workspaceDirectory,
        CancellationToken cancellationToken = default);

    Task<GitCommitResult> CommitAsync(
        string workspaceDirectory,
        string message,
        CancellationToken cancellationToken = default);

    Task<GitRemotePreflight> GetRemotePreflightAsync(
        string workspaceDirectory,
        CancellationToken cancellationToken = default);

    Task<GitFetchResult> FetchAsync(
        string workspaceDirectory,
        CancellationToken cancellationToken = default);

    Task<GitFastForwardPreflight> GetFastForwardPreflightAsync(
        string workspaceDirectory,
        CancellationToken cancellationToken = default);

    Task<GitFastForwardResult> FastForwardAsync(
        string workspaceDirectory,
        CancellationToken cancellationToken = default);

    Task<GitPushPreflight> GetPushPreflightAsync(
        string workspaceDirectory,
        CancellationToken cancellationToken = default);

    Task<GitPushResult> PushAsync(
        string workspaceDirectory,
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

public sealed record GitCommitPreflight
{
    public GitCommitPreflightState State { get; init; } =
        GitCommitPreflightState.NoStagedReqMintFiles;

    public IReadOnlyList<string> StagedPaths { get; init; } = [];

    public int OtherStagedFileCount { get; init; }

    public int SecurityWarningCount { get; init; }

    public int UnscannedFileCount { get; init; }

    public bool IsReady => State == GitCommitPreflightState.Ready;
}

public enum GitCommitPreflightState
{
    Ready,
    NoStagedReqMintFiles,
    Conflicts,
    ContainsOtherStagedFiles,
    BlockedBySecurity,
}

public sealed record GitCommitResult
{
    public GitCommitResultState State { get; init; } = GitCommitResultState.PreflightBlocked;

    public GitCommitPreflight Preflight { get; init; } = new();

    public string CommitId { get; init; } = string.Empty;
}

public enum GitCommitResultState
{
    Committed,
    InvalidMessage,
    PreflightBlocked,
}

public static class GitCommitMessageValidator
{
    public const int MinimumLength = 3;
    public const int MaximumLength = 72;

    public static bool IsValid(string? message)
    {
        if (string.IsNullOrWhiteSpace(message) || message != message.Trim())
        {
            return false;
        }

        return message.Length is >= MinimumLength and <= MaximumLength
            && !message.Any(character => character is '\r' or '\n' || char.IsControl(character));
    }
}

public sealed record GitRemotePreflight
{
    public GitRemotePreflightState State { get; init; } = GitRemotePreflightState.NoUpstream;

    public string RemoteName { get; init; } = string.Empty;

    public string Branch { get; init; } = string.Empty;

    public int AheadBy { get; init; }

    public int BehindBy { get; init; }

    public bool IsReady => State == GitRemotePreflightState.Ready;
}

public enum GitRemotePreflightState
{
    Ready,
    NoUpstream,
    DetachedHead,
    UnsupportedRemote,
}

public sealed record GitFetchResult
{
    public GitFetchResultState State { get; init; } = GitFetchResultState.PreflightBlocked;

    public GitRemotePreflight Preflight { get; init; } = new();

    public int AheadBy { get; init; }

    public int BehindBy { get; init; }
}

public enum GitFetchResultState
{
    Fetched,
    PreflightBlocked,
}

public sealed record GitFastForwardPreflight
{
    public GitFastForwardPreflightState State { get; init; } =
        GitFastForwardPreflightState.NoUpdates;

    public GitRemotePreflight Remote { get; init; } = new();

    public IReadOnlyList<string> CommitSummaries { get; init; } = [];

    public IReadOnlyList<string> ChangedPaths { get; init; } = [];

    public bool IsTruncated { get; init; }

    public int OtherChangedFileCount { get; init; }

    public bool IsReady => State == GitFastForwardPreflightState.Ready;
}

public enum GitFastForwardPreflightState
{
    Ready,
    RemoteUnavailable,
    WorkingTreeDirty,
    Conflicts,
    NoUpdates,
    Diverged,
    PreviewUnavailable,
    PreviewTooLarge,
    ContainsOtherFiles,
}

public sealed record GitFastForwardResult
{
    public GitFastForwardResultState State { get; init; } =
        GitFastForwardResultState.PreflightBlocked;

    public GitFastForwardPreflight Preflight { get; init; } = new();

    public string PreviousCommitId { get; init; } = string.Empty;

    public string CurrentCommitId { get; init; } = string.Empty;
}

public enum GitFastForwardResultState
{
    Updated,
    PreflightBlocked,
}

public sealed record GitPushPreflight
{
    public GitPushPreflightState State { get; init; } =
        GitPushPreflightState.NoOutgoingCommits;

    public GitRemotePreflight Remote { get; init; } = new();

    public IReadOnlyList<string> CommitSummaries { get; init; } = [];

    public IReadOnlyList<string> ChangedPaths { get; init; } = [];

    public int SecurityWarningCount { get; init; }

    public int UnscannedSnapshotCount { get; init; }

    public bool IsTruncated { get; init; }

    public int OtherChangedFileCount { get; init; }

    public bool IsReady => State == GitPushPreflightState.Ready;
}

public enum GitPushPreflightState
{
    Ready,
    RemoteUnavailable,
    WorkingTreeDirty,
    Conflicts,
    NoOutgoingCommits,
    BehindRemote,
    Diverged,
    PreviewUnavailable,
    PreviewTooLarge,
    ContainsOtherFiles,
    BlockedBySecurity,
}

public sealed record GitPushResult
{
    public GitPushResultState State { get; init; } = GitPushResultState.PreflightBlocked;

    public GitPushPreflight Preflight { get; init; } = new();

    public string CurrentCommitId { get; init; } = string.Empty;
}

public enum GitPushResultState
{
    Pushed,
    PreflightBlocked,
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

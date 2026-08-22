using System.ComponentModel;
using System.Diagnostics;
using ReqMint.Core.Git;

namespace ReqMint.Platform.Git;

public sealed class SystemGitService : IGitService
{
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan NetworkCommandTimeout = TimeSpan.FromSeconds(60);
    private const int MaximumStatusCharacters = 4 * 1024 * 1024;
    private const int MaximumSnapshotCharacters = 2 * 1024 * 1024;
    private const int MaximumDiffCharacters = 256 * 1024;
    private const int MaximumErrorCharacters = 64 * 1024;
    private const int MaximumFastForwardCommits = 50;
    private const int MaximumFastForwardPaths = 200;
    private const int MaximumPushSnapshots = 500;

    public async Task<GitRepositoryStatus?> GetStatusAsync(
        string workspaceDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceDirectory);
        var fullPath = Path.GetFullPath(workspaceDirectory);

        var rootResult = await RunAsync(
            ["-C", fullPath, "rev-parse", "--show-toplevel"],
            cancellationToken);
        if (rootResult.ExitCode != 0)
        {
            if (rootResult.StandardError.Contains(
                "not a git repository",
                StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            throw new GitCommandException(rootResult.StandardError.Trim());
        }

        var repositoryRoot = rootResult.StandardOutput.Trim();
        var statusResult = await RunAsync(
            [
                "-c", "status.relativePaths=true",
                "-C", fullPath,
                "status", "--porcelain=v1", "--branch", "-z", "--untracked-files=normal",
            ],
            cancellationToken,
            MaximumStatusCharacters);
        if (statusResult.ExitCode != 0)
        {
            throw new GitCommandException(statusResult.StandardError.Trim());
        }

        if (statusResult.StandardOutputTruncated)
        {
            throw new GitCommandException("Git status output exceeded the safe preview limit.");
        }

        return GitPorcelainParser.Parse(repositoryRoot, statusResult.StandardOutput);
    }

    public async Task<GitDiffPreview> GetDiffAsync(
        string workspaceDirectory,
        string workspaceRelativePath,
        GitDiffScope scope,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceDirectory);
        if (!ReqMintGitFileClassifier.IsManaged(workspaceRelativePath))
        {
            throw new ArgumentException(
                "Diff previews are limited to ReqMint-managed workspace files.",
                nameof(workspaceRelativePath));
        }

        var fullPath = Path.GetFullPath(workspaceDirectory);
        var securityScan = scope == GitDiffScope.Staged
            ? await ScanStagedFileAsync(fullPath, workspaceRelativePath, cancellationToken)
            : await new WorkspaceGitSecretScanner().ScanAsync(
                fullPath,
                [workspaceRelativePath],
                cancellationToken);
        if (securityScan.HasWarnings || !securityScan.IsComplete)
        {
            return new GitDiffPreview
            {
                Path = workspaceRelativePath,
                Scope = scope,
                State = GitDiffPreviewState.BlockedBySecurity,
                SecurityWarningCount = securityScan.Findings.Count,
                UnscannedFileCount = securityScan.UnscannedFiles.Count,
            };
        }

        var arguments = new List<string>
        {
            "-C", fullPath,
            "diff",
            "--no-ext-diff",
            "--no-textconv",
            "--no-color",
            "--unified=3",
        };
        if (scope == GitDiffScope.Staged)
        {
            arguments.Add("--cached");
        }

        arguments.Add("--");
        arguments.Add(workspaceRelativePath);
        var diffResult = await RunAsync(
            arguments,
            cancellationToken,
            MaximumDiffCharacters);
        if (diffResult.ExitCode != 0)
        {
            return UnavailableDiff(workspaceRelativePath, scope);
        }

        if (scope == GitDiffScope.WorkingTree
            && string.IsNullOrEmpty(diffResult.StandardOutput))
        {
            diffResult = await GetUntrackedDiffAsync(
                fullPath,
                workspaceRelativePath,
                cancellationToken);
        }

        if (diffResult.ExitCode is not (0 or 1))
        {
            return UnavailableDiff(workspaceRelativePath, scope);
        }

        return new GitDiffPreview
        {
            Path = workspaceRelativePath,
            Scope = scope,
            Content = diffResult.StandardOutput,
            IsTruncated = diffResult.StandardOutputTruncated,
        };
    }

    public async Task<GitStageResult> StageFileAsync(
        string workspaceDirectory,
        string workspaceRelativePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceDirectory);
        if (!ReqMintGitFileClassifier.IsManaged(workspaceRelativePath))
        {
            throw new ArgumentException(
                "Staging is limited to ReqMint-managed workspace files.",
                nameof(workspaceRelativePath));
        }

        var fullPath = Path.GetFullPath(workspaceDirectory);
        var status = await GetStatusAsync(fullPath, cancellationToken);
        var change = status?.Changes.FirstOrDefault(candidate => PathsEqual(
            candidate.Path,
            workspaceRelativePath));
        if (change is null || !change.IsStageCandidate)
        {
            return StageResult(workspaceRelativePath, GitStageResultState.NotEligible);
        }

        var workingTreeScan = await new WorkspaceGitSecretScanner().ScanAsync(
            fullPath,
            [workspaceRelativePath],
            cancellationToken);
        if (workingTreeScan.HasWarnings || !workingTreeScan.IsComplete)
        {
            return BlockedStageResult(workspaceRelativePath, workingTreeScan);
        }

        var stage = await RunAsync(
            ["-C", fullPath, "add", "--", workspaceRelativePath],
            cancellationToken);
        if (stage.ExitCode != 0)
        {
            throw new GitCommandException(stage.StandardError.Trim());
        }

        var stagedScan = await ScanStagedFileAsync(
            fullPath,
            workspaceRelativePath,
            cancellationToken);
        if (stagedScan.HasWarnings || !stagedScan.IsComplete)
        {
            await UnstageFileAsync(fullPath, workspaceRelativePath, cancellationToken);
            return BlockedStageResult(workspaceRelativePath, stagedScan);
        }

        return StageResult(workspaceRelativePath, GitStageResultState.Staged);
    }

    public async Task<GitCommitPreflight> GetCommitPreflightAsync(
        string workspaceDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceDirectory);
        var fullPath = Path.GetFullPath(workspaceDirectory);
        var status = await GetStatusAsync(fullPath, cancellationToken);
        if (status is null)
        {
            return CommitPreflight(GitCommitPreflightState.NoStagedReqMintFiles);
        }

        var stagedChanges = status.Changes
            .Where(change => change.HasStagedChanges)
            .ToArray();
        var stagedReqMintChanges = stagedChanges
            .Where(change => ReqMintGitFileClassifier.IsManaged(change.Path))
            .ToArray();
        var stagedPaths = stagedReqMintChanges
            .Select(change => change.Path)
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (status.Changes.Any(change => change.IsConflict))
        {
            return CommitPreflight(
                GitCommitPreflightState.Conflicts,
                stagedPaths);
        }

        if (stagedReqMintChanges.Length == 0)
        {
            return CommitPreflight(GitCommitPreflightState.NoStagedReqMintFiles);
        }

        var otherStagedFileCount = stagedChanges.Length - stagedReqMintChanges.Length;
        if (otherStagedFileCount > 0)
        {
            return CommitPreflight(
                GitCommitPreflightState.ContainsOtherStagedFiles,
                stagedPaths,
                otherStagedFileCount);
        }

        var warningCount = 0;
        var unscannedCount = 0;
        foreach (var path in stagedPaths)
        {
            var scan = await ScanStagedFileAsync(fullPath, path, cancellationToken);
            warningCount += scan.Findings.Count;
            unscannedCount += scan.UnscannedFiles.Count;
        }

        return warningCount > 0 || unscannedCount > 0
            ? new GitCommitPreflight
            {
                State = GitCommitPreflightState.BlockedBySecurity,
                StagedPaths = stagedPaths,
                SecurityWarningCount = warningCount,
                UnscannedFileCount = unscannedCount,
            }
            : CommitPreflight(GitCommitPreflightState.Ready, stagedPaths);
    }

    public async Task<GitCommitResult> CommitAsync(
        string workspaceDirectory,
        string message,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceDirectory);
        if (!GitCommitMessageValidator.IsValid(message))
        {
            return new GitCommitResult { State = GitCommitResultState.InvalidMessage };
        }

        var fullPath = Path.GetFullPath(workspaceDirectory);
        var preflight = await GetCommitPreflightAsync(fullPath, cancellationToken);
        if (!preflight.IsReady)
        {
            return new GitCommitResult
            {
                State = GitCommitResultState.PreflightBlocked,
                Preflight = preflight,
            };
        }

        var commit = await RunWithHooksDisabledAsync(
            ["-C", fullPath, "commit", "--quiet", "-m", message],
            cancellationToken);

        if (commit.ExitCode != 0)
        {
            throw new GitCommandException(commit.StandardError.Trim());
        }

        var revision = await RunAsync(
            ["-C", fullPath, "rev-parse", "--short=12", "HEAD"],
            cancellationToken,
            maximumOutputCharacters: 256);
        return new GitCommitResult
        {
            State = GitCommitResultState.Committed,
            Preflight = preflight,
            CommitId = revision.ExitCode == 0 ? revision.StandardOutput.Trim() : string.Empty,
        };
    }

    public async Task<GitRemotePreflight> GetRemotePreflightAsync(
        string workspaceDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceDirectory);
        var fullPath = Path.GetFullPath(workspaceDirectory);
        var status = await GetStatusAsync(fullPath, cancellationToken);
        if (status is null)
        {
            return new GitRemotePreflight();
        }

        if (status.IsDetached)
        {
            return new GitRemotePreflight
            {
                State = GitRemotePreflightState.DetachedHead,
            };
        }

        var remote = await RunAsync(
            ["-C", fullPath, "config", "--get", $"branch.{status.Branch}.remote"],
            cancellationToken,
            maximumOutputCharacters: 1024);
        var merge = await RunAsync(
            ["-C", fullPath, "config", "--get", $"branch.{status.Branch}.merge"],
            cancellationToken,
            maximumOutputCharacters: 4096);
        if (remote.ExitCode != 0 || merge.ExitCode != 0)
        {
            return new GitRemotePreflight
            {
                State = GitRemotePreflightState.NoUpstream,
                Branch = status.Branch,
                AheadBy = status.AheadBy,
                BehindBy = status.BehindBy,
            };
        }

        var remoteName = remote.StandardOutput.Trim();
        var mergeReference = merge.StandardOutput.Trim();
        if (!IsSafeRemoteName(remoteName)
            || !mergeReference.StartsWith("refs/heads/", StringComparison.Ordinal)
            || mergeReference.Length == "refs/heads/".Length)
        {
            return new GitRemotePreflight
            {
                State = GitRemotePreflightState.UnsupportedRemote,
                Branch = status.Branch,
                AheadBy = status.AheadBy,
                BehindBy = status.BehindBy,
            };
        }

        return new GitRemotePreflight
        {
            State = GitRemotePreflightState.Ready,
            RemoteName = remoteName,
            Branch = mergeReference["refs/heads/".Length..],
            AheadBy = status.AheadBy,
            BehindBy = status.BehindBy,
        };
    }

    public async Task<GitFetchResult> FetchAsync(
        string workspaceDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceDirectory);
        var fullPath = Path.GetFullPath(workspaceDirectory);
        var preflight = await GetRemotePreflightAsync(fullPath, cancellationToken);
        if (!preflight.IsReady)
        {
            return new GitFetchResult
            {
                State = GitFetchResultState.PreflightBlocked,
                Preflight = preflight,
            };
        }

        var fetch = await RunAsync(
            [
                "-C", fullPath,
                "fetch", "--no-tags", "--no-recurse-submodules", "--", preflight.RemoteName,
            ],
            cancellationToken,
            maximumOutputCharacters: 64 * 1024,
            commandTimeout: NetworkCommandTimeout);
        if (fetch.ExitCode != 0)
        {
            throw new GitCommandException("The remote check could not be completed.");
        }

        var status = await GetStatusAsync(fullPath, cancellationToken);
        return new GitFetchResult
        {
            State = GitFetchResultState.Fetched,
            Preflight = preflight,
            AheadBy = status?.AheadBy ?? 0,
            BehindBy = status?.BehindBy ?? 0,
        };
    }

    public async Task<GitFastForwardPreflight> GetFastForwardPreflightAsync(
        string workspaceDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceDirectory);
        var fullPath = Path.GetFullPath(workspaceDirectory);
        var remote = await GetRemotePreflightAsync(fullPath, cancellationToken);
        if (!remote.IsReady)
        {
            return FastForwardPreflight(
                GitFastForwardPreflightState.RemoteUnavailable,
                remote);
        }

        var status = await GetStatusAsync(fullPath, cancellationToken);
        if (status is null)
        {
            return FastForwardPreflight(
                GitFastForwardPreflightState.RemoteUnavailable,
                remote);
        }

        if (status.Changes.Any(change => change.IsConflict))
        {
            return FastForwardPreflight(GitFastForwardPreflightState.Conflicts, remote);
        }

        if (!status.IsClean)
        {
            return FastForwardPreflight(
                GitFastForwardPreflightState.WorkingTreeDirty,
                remote);
        }

        if (status.BehindBy <= 0)
        {
            return FastForwardPreflight(GitFastForwardPreflightState.NoUpdates, remote);
        }

        if (status.AheadBy > 0)
        {
            return FastForwardPreflight(GitFastForwardPreflightState.Diverged, remote);
        }

        var commits = await RunAsync(
            [
                "-C", fullPath,
                "log", $"--max-count={MaximumFastForwardCommits + 1}",
                "--format=%h%x09%s", "HEAD..@{upstream}",
            ],
            cancellationToken,
            maximumOutputCharacters: 128 * 1024);
        var paths = await RunAsync(
            ["-C", fullPath, "diff", "--name-only", "-z", "HEAD..@{upstream}"],
            cancellationToken,
            maximumOutputCharacters: 256 * 1024);
        if (commits.ExitCode != 0 || paths.ExitCode != 0)
        {
            return FastForwardPreflight(
                GitFastForwardPreflightState.PreviewUnavailable,
                remote);
        }

        var commitLines = commits.StandardOutput
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        var changedPaths = paths.StandardOutput
            .Split('\0', StringSplitOptions.RemoveEmptyEntries);
        var isTruncated = commits.StandardOutputTruncated
            || paths.StandardOutputTruncated
            || commitLines.Length > MaximumFastForwardCommits
            || changedPaths.Length > MaximumFastForwardPaths;
        if (isTruncated)
        {
            return new GitFastForwardPreflight
            {
                State = GitFastForwardPreflightState.PreviewTooLarge,
                Remote = remote,
                IsTruncated = true,
            };
        }

        var workspacePrefix = Path.GetRelativePath(status.RepositoryRoot, fullPath)
            .Replace('\\', '/')
            .Trim('/');
        if (workspacePrefix == ".")
        {
            workspacePrefix = string.Empty;
        }

        var managedPaths = new List<string>(changedPaths.Length);
        var otherChangedFileCount = 0;
        foreach (var changedPath in changedPaths)
        {
            var normalizedPath = changedPath.Replace('\\', '/');
            var workspacePath = string.IsNullOrEmpty(workspacePrefix)
                ? normalizedPath
                : normalizedPath.StartsWith(
                    workspacePrefix + "/",
                    OperatingSystem.IsWindows()
                        ? StringComparison.OrdinalIgnoreCase
                        : StringComparison.Ordinal)
                    ? normalizedPath[(workspacePrefix.Length + 1)..]
                    : string.Empty;
            if (!ReqMintGitFileClassifier.IsManaged(workspacePath))
            {
                otherChangedFileCount++;
                continue;
            }

            managedPaths.Add(workspacePath);
        }

        if (otherChangedFileCount > 0)
        {
            return new GitFastForwardPreflight
            {
                State = GitFastForwardPreflightState.ContainsOtherFiles,
                Remote = remote,
                OtherChangedFileCount = otherChangedFileCount,
            };
        }

        return new GitFastForwardPreflight
        {
            State = GitFastForwardPreflightState.Ready,
            Remote = remote,
            CommitSummaries = commitLines
                .Take(MaximumFastForwardCommits)
                .Select(FormatCommitSummary)
                .ToArray(),
            ChangedPaths = managedPaths
                .Select(SanitizeDisplayText)
                .ToArray(),
        };
    }

    public async Task<GitFastForwardResult> FastForwardAsync(
        string workspaceDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceDirectory);
        var fullPath = Path.GetFullPath(workspaceDirectory);
        var preflight = await GetFastForwardPreflightAsync(fullPath, cancellationToken);
        if (!preflight.IsReady)
        {
            return new GitFastForwardResult
            {
                State = GitFastForwardResultState.PreflightBlocked,
                Preflight = preflight,
            };
        }

        var previous = await GetShortHeadAsync(fullPath, cancellationToken);
        var merge = await RunWithHooksDisabledAsync(
            ["-C", fullPath, "merge", "--ff-only", "--no-edit", "@{upstream}"],
            cancellationToken);
        if (merge.ExitCode != 0)
        {
            throw new GitCommandException("The fast-forward update could not be completed.");
        }

        return new GitFastForwardResult
        {
            State = GitFastForwardResultState.Updated,
            Preflight = preflight,
            PreviousCommitId = previous,
            CurrentCommitId = await GetShortHeadAsync(fullPath, cancellationToken),
        };
    }

    public async Task<GitPushPreflight> GetPushPreflightAsync(
        string workspaceDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceDirectory);
        var fullPath = Path.GetFullPath(workspaceDirectory);
        var remote = await GetRemotePreflightAsync(fullPath, cancellationToken);
        if (!remote.IsReady)
        {
            return PushPreflight(GitPushPreflightState.RemoteUnavailable, remote);
        }

        var branchCheck = await RunAsync(
            ["check-ref-format", $"refs/heads/{remote.Branch}"],
            cancellationToken,
            maximumOutputCharacters: 1024);
        if (branchCheck.ExitCode != 0)
        {
            return PushPreflight(GitPushPreflightState.RemoteUnavailable, remote);
        }

        var status = await GetStatusAsync(fullPath, cancellationToken);
        if (status is null)
        {
            return PushPreflight(GitPushPreflightState.RemoteUnavailable, remote);
        }

        if (status.Changes.Any(change => change.IsConflict))
        {
            return PushPreflight(GitPushPreflightState.Conflicts, remote);
        }

        if (!status.IsClean)
        {
            return PushPreflight(GitPushPreflightState.WorkingTreeDirty, remote);
        }

        if (status.BehindBy > 0 && status.AheadBy > 0)
        {
            return PushPreflight(GitPushPreflightState.Diverged, remote);
        }

        if (status.BehindBy > 0)
        {
            return PushPreflight(GitPushPreflightState.BehindRemote, remote);
        }

        if (status.AheadBy <= 0)
        {
            return PushPreflight(GitPushPreflightState.NoOutgoingCommits, remote);
        }

        var revisions = await RunAsync(
            [
                "-C", fullPath,
                "rev-list", $"--max-count={MaximumFastForwardCommits + 1}",
                "@{upstream}..HEAD",
            ],
            cancellationToken,
            maximumOutputCharacters: 16 * 1024);
        var commits = await RunAsync(
            [
                "-C", fullPath,
                "log", $"--max-count={MaximumFastForwardCommits + 1}",
                "--format=%h%x09%s", "@{upstream}..HEAD",
            ],
            cancellationToken,
            maximumOutputCharacters: 128 * 1024);
        var paths = await RunAsync(
            ["-C", fullPath, "diff", "--name-only", "-z", "@{upstream}..HEAD"],
            cancellationToken,
            maximumOutputCharacters: 256 * 1024);
        if (revisions.ExitCode != 0 || commits.ExitCode != 0 || paths.ExitCode != 0)
        {
            return PushPreflight(GitPushPreflightState.PreviewUnavailable, remote);
        }

        var revisionIds = revisions.StandardOutput
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        var commitLines = commits.StandardOutput
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        var changedRepositoryPaths = paths.StandardOutput
            .Split('\0', StringSplitOptions.RemoveEmptyEntries);
        if (revisions.StandardOutputTruncated
            || commits.StandardOutputTruncated
            || paths.StandardOutputTruncated
            || revisionIds.Length > MaximumFastForwardCommits
            || commitLines.Length > MaximumFastForwardCommits
            || changedRepositoryPaths.Length > MaximumFastForwardPaths)
        {
            return new GitPushPreflight
            {
                State = GitPushPreflightState.PreviewTooLarge,
                Remote = remote,
                IsTruncated = true,
            };
        }

        var changedPaths = new List<string>(changedRepositoryPaths.Length);
        var otherChangedFileCount = 0;
        foreach (var repositoryPath in changedRepositoryPaths)
        {
            var workspacePath = GetManagedWorkspacePath(
                status.RepositoryRoot,
                fullPath,
                repositoryPath);
            if (workspacePath is null)
            {
                otherChangedFileCount++;
            }
            else
            {
                changedPaths.Add(workspacePath);
            }
        }

        if (otherChangedFileCount > 0)
        {
            return new GitPushPreflight
            {
                State = GitPushPreflightState.ContainsOtherFiles,
                Remote = remote,
                OtherChangedFileCount = otherChangedFileCount,
            };
        }

        var warningCount = 0;
        var unscannedCount = 0;
        var snapshotCount = 0;
        foreach (var revision in revisionIds)
        {
            var revisionPaths = await RunAsync(
                [
                    "-C", fullPath,
                    "diff-tree", "--root", "--no-commit-id", "--name-only", "-r", "-z",
                    revision,
                ],
                cancellationToken,
                maximumOutputCharacters: 256 * 1024);
            if (revisionPaths.ExitCode != 0 || revisionPaths.StandardOutputTruncated)
            {
                return PushPreflight(GitPushPreflightState.PreviewUnavailable, remote);
            }

            foreach (var repositoryPath in revisionPaths.StandardOutput
                .Split('\0', StringSplitOptions.RemoveEmptyEntries))
            {
                var workspacePath = GetManagedWorkspacePath(
                    status.RepositoryRoot,
                    fullPath,
                    repositoryPath);
                if (workspacePath is null)
                {
                    return new GitPushPreflight
                    {
                        State = GitPushPreflightState.ContainsOtherFiles,
                        Remote = remote,
                        OtherChangedFileCount = 1,
                    };
                }

                snapshotCount++;
                if (snapshotCount > MaximumPushSnapshots)
                {
                    return new GitPushPreflight
                    {
                        State = GitPushPreflightState.PreviewTooLarge,
                        Remote = remote,
                        IsTruncated = true,
                    };
                }

                var scan = await ScanCommittedFileAsync(
                    fullPath,
                    revision,
                    workspacePath,
                    cancellationToken);
                warningCount += scan.Findings.Count;
                unscannedCount += scan.UnscannedFiles.Count;
            }
        }

        if (warningCount > 0 || unscannedCount > 0)
        {
            return new GitPushPreflight
            {
                State = GitPushPreflightState.BlockedBySecurity,
                Remote = remote,
                SecurityWarningCount = warningCount,
                UnscannedSnapshotCount = unscannedCount,
            };
        }

        return new GitPushPreflight
        {
            State = GitPushPreflightState.Ready,
            Remote = remote,
            CommitSummaries = commitLines.Select(FormatCommitSummary).ToArray(),
            ChangedPaths = changedPaths
                .Distinct(OperatingSystem.IsWindows()
                    ? StringComparer.OrdinalIgnoreCase
                    : StringComparer.Ordinal)
                .Select(SanitizeDisplayText)
                .ToArray(),
        };
    }

    public async Task<GitPushResult> PushAsync(
        string workspaceDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceDirectory);
        var fullPath = Path.GetFullPath(workspaceDirectory);
        var preflight = await GetPushPreflightAsync(fullPath, cancellationToken);
        if (!preflight.IsReady)
        {
            return new GitPushResult
            {
                State = GitPushResultState.PreflightBlocked,
                Preflight = preflight,
            };
        }

        var push = await RunWithHooksDisabledAsync(
            [
                "-c", "push.followTags=false",
                "-C", fullPath,
                "push", "--porcelain", "--no-verify", "--no-follow-tags", "--",
                preflight.Remote.RemoteName,
                $"HEAD:refs/heads/{preflight.Remote.Branch}",
            ],
            cancellationToken,
            NetworkCommandTimeout,
            64 * 1024);
        if (push.ExitCode != 0)
        {
            throw new GitCommandException("The push could not be completed.");
        }

        return new GitPushResult
        {
            State = GitPushResultState.Pushed,
            Preflight = preflight,
            CurrentCommitId = await GetShortHeadAsync(fullPath, cancellationToken),
        };
    }

    private static Task<string> GetShortHeadAsync(
        string workspaceDirectory,
        CancellationToken cancellationToken) => GetRevisionAsync(
            workspaceDirectory,
            "HEAD",
            cancellationToken);

    private static async Task<string> GetRevisionAsync(
        string workspaceDirectory,
        string revision,
        CancellationToken cancellationToken)
    {
        var result = await RunAsync(
            ["-C", workspaceDirectory, "rev-parse", "--short=12", revision],
            cancellationToken,
            maximumOutputCharacters: 256);
        return result.ExitCode == 0 ? result.StandardOutput.Trim() : string.Empty;
    }

    private static GitFastForwardPreflight FastForwardPreflight(
        GitFastForwardPreflightState state,
        GitRemotePreflight remote) => new()
        {
            State = state,
            Remote = remote,
        };

    private static GitPushPreflight PushPreflight(
        GitPushPreflightState state,
        GitRemotePreflight remote) => new()
        {
            State = state,
            Remote = remote,
        };

    private static string? GetManagedWorkspacePath(
        string repositoryRoot,
        string workspaceDirectory,
        string repositoryRelativePath)
    {
        var workspacePrefix = Path.GetRelativePath(repositoryRoot, workspaceDirectory)
            .Replace('\\', '/')
            .Trim('/');
        if (workspacePrefix == ".")
        {
            workspacePrefix = string.Empty;
        }

        var normalizedPath = repositoryRelativePath.Replace('\\', '/');
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        var workspacePath = string.IsNullOrEmpty(workspacePrefix)
            ? normalizedPath
            : normalizedPath.StartsWith(workspacePrefix + "/", comparison)
                ? normalizedPath[(workspacePrefix.Length + 1)..]
                : string.Empty;
        return ReqMintGitFileClassifier.IsManaged(workspacePath) ? workspacePath : null;
    }

    private static string FormatCommitSummary(string value)
    {
        var separator = value.IndexOf('\t');
        return separator < 0
            ? SanitizeDisplayText(value)
            : $"{SanitizeDisplayText(value[..separator])} · " +
                SanitizeDisplayText(value[(separator + 1)..]);
    }

    private static string SanitizeDisplayText(string value) => new(
        value.Select(character => char.IsControl(character) ? '�' : character).ToArray());

    private static bool IsSafeRemoteName(string remoteName) =>
        remoteName.Length is > 0 and <= 128
        && remoteName[0] != '-'
        && remoteName != "."
        && remoteName.All(character =>
            char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-');

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            Directory.Delete(path, recursive: false);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static async Task<GitCommandResult> RunWithHooksDisabledAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken,
        TimeSpan? commandTimeout = null,
        int maximumOutputCharacters = 16 * 1024)
    {
        var emptyHooksDirectory = Path.Combine(
            Path.GetTempPath(),
            $"ReqMint.GitHooks.{Guid.NewGuid():N}");
        Directory.CreateDirectory(emptyHooksDirectory);
        try
        {
            var safeArguments = new List<string>
            {
                "-c", $"core.hooksPath={emptyHooksDirectory}",
            };
            safeArguments.AddRange(arguments);
            return await RunAsync(
                safeArguments,
                cancellationToken,
                maximumOutputCharacters,
                commandTimeout);
        }
        finally
        {
            TryDeleteDirectory(emptyHooksDirectory);
        }
    }

    private static GitCommitPreflight CommitPreflight(
        GitCommitPreflightState state,
        IReadOnlyList<string>? stagedPaths = null,
        int otherStagedFileCount = 0) => new()
        {
            State = state,
            StagedPaths = stagedPaths ?? [],
            OtherStagedFileCount = otherStagedFileCount,
        };

    private static bool PathsEqual(string first, string second) => string.Equals(
        NormalizeRelativePath(first),
        NormalizeRelativePath(second),
        OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal);

    private static string NormalizeRelativePath(string path)
    {
        var normalized = path.Replace('\\', '/');
        while (normalized.StartsWith("./", StringComparison.Ordinal))
        {
            normalized = normalized[2..];
        }

        return normalized;
    }

    private static GitStageResult StageResult(string path, GitStageResultState state) => new()
    {
        Path = path,
        State = state,
    };

    private static GitStageResult BlockedStageResult(
        string path,
        GitSecretScanResult scan) => new()
        {
            Path = path,
            State = GitStageResultState.BlockedBySecurity,
            SecurityWarningCount = scan.Findings.Count,
            UnscannedFileCount = scan.UnscannedFiles.Count,
        };

    private static async Task UnstageFileAsync(
        string workspaceDirectory,
        string relativePath,
        CancellationToken cancellationToken)
    {
        var restore = await RunAsync(
            ["-C", workspaceDirectory, "restore", "--staged", "--", relativePath],
            cancellationToken);
        if (restore.ExitCode == 0)
        {
            return;
        }

        var reset = await RunAsync(
            ["-C", workspaceDirectory, "reset", "--quiet", "--", relativePath],
            cancellationToken);
        if (reset.ExitCode == 0)
        {
            return;
        }

        var removeFromUnbornIndex = await RunAsync(
            ["-C", workspaceDirectory, "rm", "--cached", "--force", "--", relativePath],
            cancellationToken);
        if (removeFromUnbornIndex.ExitCode != 0)
        {
            throw new GitCommandException(
                "The staged file failed its security check and could not be removed from the Git index.");
        }
    }

    private static async Task<GitSecretScanResult> ScanStagedFileAsync(
        string workspaceDirectory,
        string relativePath,
        CancellationToken cancellationToken)
    {
        var snapshot = await RunAsync(
            ["-C", workspaceDirectory, "show", $":./{relativePath}"],
            cancellationToken,
            MaximumSnapshotCharacters);
        if (snapshot.ExitCode == 0 && !snapshot.StandardOutputTruncated)
        {
            return WorkspaceGitSecretScanner.ScanText(relativePath, snapshot.StandardOutput);
        }

        if (snapshot.StandardOutputTruncated)
        {
            return new GitSecretScanResult { UnscannedFiles = [relativePath] };
        }

        var deletion = await RunAsync(
            [
                "-C", workspaceDirectory,
                "diff", "--cached", "--name-only", "--diff-filter=D", "--", relativePath,
            ],
            cancellationToken,
            maximumOutputCharacters: 4096);
        return deletion.ExitCode == 0 && !string.IsNullOrWhiteSpace(deletion.StandardOutput)
            ? GitSecretScanResult.Empty
            : new GitSecretScanResult { UnscannedFiles = [relativePath] };
    }

    private static async Task<GitSecretScanResult> ScanCommittedFileAsync(
        string workspaceDirectory,
        string revision,
        string relativePath,
        CancellationToken cancellationToken)
    {
        var snapshot = await RunAsync(
            ["-C", workspaceDirectory, "show", $"{revision}:./{relativePath}"],
            cancellationToken,
            MaximumSnapshotCharacters);
        if (snapshot.ExitCode == 0 && !snapshot.StandardOutputTruncated)
        {
            return WorkspaceGitSecretScanner.ScanText(relativePath, snapshot.StandardOutput);
        }

        if (snapshot.StandardOutputTruncated)
        {
            return new GitSecretScanResult { UnscannedFiles = [relativePath] };
        }

        var exists = await RunAsync(
            [
                "-C", workspaceDirectory,
                "ls-tree", "-r", "--name-only", "-z", revision, "--", relativePath,
            ],
            cancellationToken,
            maximumOutputCharacters: 4096);
        return exists.ExitCode == 0 && string.IsNullOrEmpty(exists.StandardOutput)
            ? GitSecretScanResult.Empty
            : new GitSecretScanResult { UnscannedFiles = [relativePath] };
    }

    private static Task<GitCommandResult> GetUntrackedDiffAsync(
        string workspaceDirectory,
        string relativePath,
        CancellationToken cancellationToken)
    {
        var nullDevice = OperatingSystem.IsWindows() ? "NUL" : "/dev/null";
        return RunAsync(
            [
                "-C", workspaceDirectory,
                "diff", "--no-index", "--no-ext-diff", "--no-textconv", "--no-color",
                "--unified=3", "--", nullDevice, relativePath,
            ],
            cancellationToken,
            MaximumDiffCharacters);
    }

    private static GitDiffPreview UnavailableDiff(string path, GitDiffScope scope) => new()
    {
        Path = path,
        Scope = scope,
        State = GitDiffPreviewState.Unavailable,
    };

    private static async Task<GitCommandResult> RunAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken,
        int maximumOutputCharacters = 16 * 1024,
        TimeSpan? commandTimeout = null)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.Environment["GIT_OPTIONAL_LOCKS"] = "0";
        startInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";
        startInfo.Environment["GCM_INTERACTIVE"] = "Never";
        startInfo.Environment["SSH_ASKPASS_REQUIRE"] = "never";
        startInfo.Environment["GIT_PAGER"] = "cat";
        startInfo.Environment["PAGER"] = "cat";
        startInfo.Environment["LC_ALL"] = "C";

        using var process = new Process { StartInfo = startInfo };
        try
        {
            process.Start();
        }
        catch (Win32Exception exception)
        {
            throw new GitUnavailableException("Git is not installed or could not be started.", exception);
        }

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(commandTimeout ?? CommandTimeout);
        var standardOutput = ReadBoundedAsync(
            process.StandardOutput,
            maximumOutputCharacters,
            timeoutSource.Token);
        var standardError = ReadBoundedAsync(
            process.StandardError,
            MaximumErrorCharacters,
            timeoutSource.Token);

        try
        {
            await process.WaitForExitAsync(timeoutSource.Token);
            var output = await standardOutput;
            var error = await standardError;
            return new GitCommandResult(
                process.ExitCode,
                output.Content,
                error.Content,
                output.IsTruncated);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            TryKill(process);
            throw new TimeoutException("Git command timed out.");
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }
    }

    private static async Task<BoundedText> ReadBoundedAsync(
        StreamReader reader,
        int maximumCharacters,
        CancellationToken cancellationToken)
    {
        var content = new System.Text.StringBuilder(
            Math.Min(maximumCharacters, 16 * 1024));
        var buffer = new char[4096];
        var truncated = false;
        while (true)
        {
            var read = await reader.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                break;
            }

            var remaining = maximumCharacters - content.Length;
            if (remaining > 0)
            {
                content.Append(buffer, 0, Math.Min(read, remaining));
            }

            truncated |= read > remaining;
        }

        return new BoundedText(content.ToString(), truncated);
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
        }
    }

    private sealed record GitCommandResult(
        int ExitCode,
        string StandardOutput,
        string StandardError,
        bool StandardOutputTruncated);

    private sealed record BoundedText(string Content, bool IsTruncated);
}

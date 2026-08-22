using System.ComponentModel;
using System.Diagnostics;
using ReqMint.Core.Git;

namespace ReqMint.Platform.Git;

public sealed class SystemGitService : IGitService
{
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(10);
    private const int MaximumStatusCharacters = 4 * 1024 * 1024;
    private const int MaximumSnapshotCharacters = 2 * 1024 * 1024;
    private const int MaximumDiffCharacters = 256 * 1024;
    private const int MaximumErrorCharacters = 64 * 1024;

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

        var emptyHooksDirectory = Path.Combine(
            Path.GetTempPath(),
            $"ReqMint.GitHooks.{Guid.NewGuid():N}");
        Directory.CreateDirectory(emptyHooksDirectory);
        GitCommandResult commit;
        try
        {
            commit = await RunAsync(
                [
                    "-c", $"core.hooksPath={emptyHooksDirectory}",
                    "-C", fullPath,
                    "commit", "--quiet", "-m", message,
                ],
                cancellationToken);
        }
        finally
        {
            TryDeleteDirectory(emptyHooksDirectory);
        }

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
        int maximumOutputCharacters = 16 * 1024)
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
        timeoutSource.CancelAfter(CommandTimeout);
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

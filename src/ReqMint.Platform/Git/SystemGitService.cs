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

using System.ComponentModel;
using System.Diagnostics;
using ReqMint.Core.Git;

namespace ReqMint.Platform.Git;

public sealed class SystemGitService : IGitService
{
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(10);

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
            cancellationToken);
        if (statusResult.ExitCode != 0)
        {
            throw new GitCommandException(statusResult.StandardError.Trim());
        }

        return GitPorcelainParser.Parse(repositoryRoot, statusResult.StandardOutput);
    }

    private static async Task<GitCommandResult> RunAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
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
        var standardOutput = process.StandardOutput.ReadToEndAsync(timeoutSource.Token);
        var standardError = process.StandardError.ReadToEndAsync(timeoutSource.Token);

        try
        {
            await process.WaitForExitAsync(timeoutSource.Token);
            return new GitCommandResult(
                process.ExitCode,
                await standardOutput,
                await standardError);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            TryKill(process);
            throw new TimeoutException("Git status inspection timed out.");
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }
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
        string StandardError);
}

using System.Diagnostics;
using ReqMint.Core.Git;
using ReqMint.Platform.Git;

namespace ReqMint.Platform.Tests;

public sealed class SystemGitServiceDiffTests
{
    [Fact]
    public async Task GetDiffAsync_ReturnsWorkingTreePreviewForManagedFiles()
    {
        using var repository = await TemporaryGitRepository.CreateAsync();
        await repository.WriteAsync(
            "collections/orders.json",
            "{\"name\":\"Orders\",\"requests\":[]}");
        await repository.CommitAllAsync("initial workspace");
        await repository.WriteAsync(
            "collections/orders.json",
            "{\"name\":\"Commerce orders\",\"requests\":[]}");

        var preview = await new SystemGitService().GetDiffAsync(
            repository.Path,
            "collections/orders.json",
            GitDiffScope.WorkingTree);

        Assert.Equal(GitDiffPreviewState.Available, preview.State);
        Assert.Contains("-\u007b\"name\":\"Orders\"", preview.Content, StringComparison.Ordinal);
        Assert.Contains("+\u007b\"name\":\"Commerce orders\"", preview.Content, StringComparison.Ordinal);
        Assert.False(preview.IsTruncated);
    }

    [Fact]
    public async Task GetDiffAsync_ReturnsPreviewForUntrackedManagedFiles()
    {
        using var repository = await TemporaryGitRepository.CreateAsync();
        await repository.WriteAsync("reqmint.workspace.json", "{\"name\":\"Workspace\"}");
        await repository.CommitAllAsync("initial workspace");
        await repository.WriteAsync(
            "collections/new.json",
            "{\"name\":\"New collection\",\"requests\":[]}");

        var preview = await new SystemGitService().GetDiffAsync(
            repository.Path,
            "collections/new.json",
            GitDiffScope.WorkingTree);

        Assert.Equal(GitDiffPreviewState.Available, preview.State);
        Assert.Contains("collections/new.json", preview.Content, StringComparison.Ordinal);
        Assert.Contains("+\u007b\"name\":\"New collection\"", preview.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetDiffAsync_ReturnsStagedPreviewAfterExactIndexScan()
    {
        using var repository = await TemporaryGitRepository.CreateAsync();
        await repository.WriteAsync(
            "collections/orders.json",
            "{\"name\":\"Orders\",\"requests\":[]}");
        await repository.CommitAllAsync("initial workspace");
        await repository.WriteAsync(
            "collections/orders.json",
            "{\"name\":\"Staged orders\",\"requests\":[]}");
        await repository.RunGitAsync("add", "--", "collections/orders.json");

        var preview = await new SystemGitService().GetDiffAsync(
            repository.Path,
            "collections/orders.json",
            GitDiffScope.Staged);

        Assert.Equal(GitDiffPreviewState.Available, preview.State);
        Assert.Contains("+\u007b\"name\":\"Staged orders\"", preview.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetDiffAsync_BlocksSecretThatExistsOnlyInGitIndex()
    {
        using var repository = await TemporaryGitRepository.CreateAsync();
        const string safeEnvironment =
            "{\"variables\":[{\"name\":\"API_TOKEN\",\"value\":null,\"isSecret\":true}]}";
        const string secret = "must-never-reach-preview";
        await repository.WriteAsync("environments/local.json", safeEnvironment);
        await repository.CommitAllAsync("initial workspace");
        await repository.WriteAsync(
            "environments/local.json",
            $"{{\"variables\":[{{\"name\":\"API_TOKEN\",\"value\":\"{secret}\",\"isSecret\":true}}]}}");
        await repository.RunGitAsync("add", "--", "environments/local.json");
        await repository.WriteAsync("environments/local.json", safeEnvironment);

        var preview = await new SystemGitService().GetDiffAsync(
            repository.Path,
            "environments/local.json",
            GitDiffScope.Staged);

        Assert.Equal(GitDiffPreviewState.BlockedBySecurity, preview.State);
        Assert.True(preview.SecurityWarningCount > 0);
        Assert.Empty(preview.Content);
        Assert.DoesNotContain(secret, preview.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task StageFileAsync_StagesOnlyTheRequestedManagedFile()
    {
        using var repository = await TemporaryGitRepository.CreateAsync();
        await repository.WriteAsync(
            "collections/orders.json",
            "{\"name\":\"Orders\",\"requests\":[]}");
        await repository.WriteAsync("notes.txt", "initial");
        await repository.CommitAllAsync("initial files");
        await repository.WriteAsync(
            "collections/orders.json",
            "{\"name\":\"Commerce orders\",\"requests\":[]}");
        await repository.WriteAsync("notes.txt", "changed");

        var service = new SystemGitService();
        var result = await service.StageFileAsync(
            repository.Path,
            "collections/orders.json");
        var status = await service.GetStatusAsync(repository.Path);

        Assert.Equal(GitStageResultState.Staged, result.State);
        Assert.Contains(
            status!.Changes,
            change => change.Path == "collections/orders.json" && change.Status == "M ");
        Assert.Contains(
            status.Changes,
            change => change.Path == "notes.txt" && change.Status == " M");
    }

    [Fact]
    public async Task StageFileAsync_BlocksSecretsWithoutChangingTheIndex()
    {
        using var repository = await TemporaryGitRepository.CreateAsync();
        const string safeEnvironment =
            "{\"variables\":[{\"name\":\"API_TOKEN\",\"value\":null,\"isSecret\":true}]}";
        await repository.WriteAsync("environments/local.json", safeEnvironment);
        await repository.CommitAllAsync("initial workspace");
        await repository.WriteAsync(
            "environments/local.json",
            "{\"variables\":[{\"name\":\"API_TOKEN\",\"value\":\"do-not-stage\",\"isSecret\":true}]}");

        var service = new SystemGitService();
        var result = await service.StageFileAsync(
            repository.Path,
            "environments/local.json");
        var status = await service.GetStatusAsync(repository.Path);

        Assert.Equal(GitStageResultState.BlockedBySecurity, result.State);
        Assert.True(result.SecurityWarningCount > 0);
        Assert.Contains(
            status!.Changes,
            change => change.Path == "environments/local.json" && change.Status == " M");
    }

    [Fact]
    public async Task StageFileAsync_PreservesAnExistingPartialStage()
    {
        using var repository = await TemporaryGitRepository.CreateAsync();
        await repository.WriteAsync(
            "collections/orders.json",
            "{\"name\":\"Original\",\"requests\":[]}");
        await repository.CommitAllAsync("initial workspace");
        await repository.WriteAsync(
            "collections/orders.json",
            "{\"name\":\"Staged version\",\"requests\":[]}");
        await repository.RunGitAsync("add", "--", "collections/orders.json");
        await repository.WriteAsync(
            "collections/orders.json",
            "{\"name\":\"Working version\",\"requests\":[]}");

        var service = new SystemGitService();
        var result = await service.StageFileAsync(
            repository.Path,
            "collections/orders.json");
        var staged = await service.GetDiffAsync(
            repository.Path,
            "collections/orders.json",
            GitDiffScope.Staged);
        var working = await service.GetDiffAsync(
            repository.Path,
            "collections/orders.json",
            GitDiffScope.WorkingTree);

        Assert.Equal(GitStageResultState.NotEligible, result.State);
        Assert.Contains("Staged version", staged.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("Working version", staged.Content, StringComparison.Ordinal);
        Assert.Contains("Working version", working.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetCommitPreflightAsync_BlocksStagedFilesOutsideReqMintScope()
    {
        using var repository = await TemporaryGitRepository.CreateAsync();
        await repository.WriteAsync(
            "collections/orders.json",
            "{\"name\":\"Original\",\"requests\":[]}");
        await repository.WriteAsync("notes.txt", "original");
        await repository.CommitAllAsync("initial workspace");
        await repository.WriteAsync(
            "collections/orders.json",
            "{\"name\":\"Changed\",\"requests\":[]}");
        await repository.WriteAsync("notes.txt", "changed");
        await repository.RunGitAsync("add", "--", "collections/orders.json", "notes.txt");

        var preflight = await new SystemGitService().GetCommitPreflightAsync(repository.Path);

        Assert.Equal(GitCommitPreflightState.ContainsOtherStagedFiles, preflight.State);
        Assert.Equal(1, preflight.OtherStagedFileCount);
        Assert.Equal(["collections/orders.json"], preflight.StagedPaths);
    }

    [Fact]
    public async Task GetCommitPreflightAsync_BlocksSecretsInTheGitIndex()
    {
        using var repository = await TemporaryGitRepository.CreateAsync();
        await repository.WriteAsync(
            "environments/local.json",
            "{\"variables\":[]}");
        await repository.CommitAllAsync("initial workspace");
        await repository.WriteAsync(
            "environments/local.json",
            "{\"variables\":[{\"name\":\"API_TOKEN\",\"value\":\"never-commit\",\"isSecret\":true}]}");
        await repository.RunGitAsync("add", "--", "environments/local.json");

        var preflight = await new SystemGitService().GetCommitPreflightAsync(repository.Path);

        Assert.Equal(GitCommitPreflightState.BlockedBySecurity, preflight.State);
        Assert.True(preflight.SecurityWarningCount > 0);
    }

    [Fact]
    public async Task CommitAsync_CommitsOnlyTheReviewedIndexAndDoesNotPush()
    {
        using var repository = await TemporaryGitRepository.CreateAsync();
        await repository.WriteAsync(
            "collections/orders.json",
            "{\"name\":\"Original\",\"requests\":[]}");
        await repository.WriteAsync("notes.txt", "original");
        await repository.CommitAllAsync("initial workspace");
        await repository.WriteAsync(
            "collections/orders.json",
            "{\"name\":\"Changed\",\"requests\":[]}");
        await repository.WriteAsync("notes.txt", "working-only change");
        await repository.RunGitAsync("add", "--", "collections/orders.json");

        var service = new SystemGitService();
        var result = await service.CommitAsync(
            repository.Path,
            "chore: update orders workspace");
        var status = await service.GetStatusAsync(repository.Path);

        Assert.Equal(GitCommitResultState.Committed, result.State);
        Assert.NotEmpty(result.CommitId);
        Assert.DoesNotContain(
            status!.Changes,
            change => change.Path == "collections/orders.json");
        Assert.Contains(
            status.Changes,
            change => change.Path == "notes.txt" && change.Status == " M");
    }

    [Fact]
    public async Task CommitAsync_RechecksScopeAfterTheReview()
    {
        using var repository = await TemporaryGitRepository.CreateAsync();
        await repository.WriteAsync(
            "collections/orders.json",
            "{\"name\":\"Original\",\"requests\":[]}");
        await repository.WriteAsync("notes.txt", "original");
        await repository.CommitAllAsync("initial workspace");
        await repository.WriteAsync(
            "collections/orders.json",
            "{\"name\":\"Changed\",\"requests\":[]}");
        await repository.RunGitAsync("add", "--", "collections/orders.json");

        var service = new SystemGitService();
        var review = await service.GetCommitPreflightAsync(repository.Path);
        Assert.True(review.IsReady);

        await repository.WriteAsync("notes.txt", "staged after review");
        await repository.RunGitAsync("add", "--", "notes.txt");
        var result = await service.CommitAsync(
            repository.Path,
            "chore: update orders workspace");

        Assert.Equal(GitCommitResultState.PreflightBlocked, result.State);
        Assert.Equal(
            GitCommitPreflightState.ContainsOtherStagedFiles,
            result.Preflight.State);
    }

    [Fact]
    public async Task GetRemotePreflightAsync_FailsClosedWithoutAnUpstream()
    {
        using var repository = await TemporaryGitRepository.CreateAsync();
        await repository.WriteAsync(
            "reqmint.workspace.json",
            "{\"name\":\"Local only\"}");
        await repository.CommitAllAsync("initial workspace");

        var preflight = await new SystemGitService().GetRemotePreflightAsync(repository.Path);

        Assert.Equal(GitRemotePreflightState.NoUpstream, preflight.State);
        Assert.False(preflight.IsReady);
        Assert.Empty(preflight.RemoteName);
    }

    [Fact]
    public async Task FetchAsync_UpdatesTrackingRefsWithoutChangingLocalFilesOrHead()
    {
        using var upstream = await TemporaryGitRepository.CreateAsync();
        await upstream.WriteAsync(
            "collections/orders.json",
            "{\"name\":\"Original\",\"requests\":[]}");
        await upstream.CommitAllAsync("initial workspace");
        using var local = await TemporaryGitRepository.CloneAsync(upstream.Path);
        var originalHead = await local.GetHeadAsync();
        await local.WriteAsync("notes.txt", "local draft must survive fetch");

        await upstream.WriteAsync(
            "collections/orders.json",
            "{\"name\":\"Remote update\",\"requests\":[]}");
        await upstream.CommitAllAsync("remote workspace update");

        var service = new SystemGitService();
        var review = await service.GetRemotePreflightAsync(local.Path);
        var result = await service.FetchAsync(local.Path);
        var localHead = await local.GetHeadAsync();
        var localContent = await local.ReadAsync("collections/orders.json");
        var localDraft = await local.ReadAsync("notes.txt");

        Assert.True(review.IsReady);
        Assert.Equal("origin", review.RemoteName);
        Assert.Equal(GitFetchResultState.Fetched, result.State);
        Assert.Equal(1, result.BehindBy);
        Assert.Equal(0, result.AheadBy);
        Assert.Equal(originalHead, localHead);
        Assert.Contains("Original", localContent, StringComparison.Ordinal);
        Assert.DoesNotContain("Remote update", localContent, StringComparison.Ordinal);
        Assert.Equal("local draft must survive fetch", localDraft);
    }

    private sealed class TemporaryGitRepository : IDisposable
    {
        private TemporaryGitRepository(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static async Task<TemporaryGitRepository> CreateAsync()
        {
            var path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "ReqMint.Git.Tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            var repository = new TemporaryGitRepository(path);
            await repository.RunGitAsync("init", "--quiet");
            await repository.RunGitAsync("config", "user.name", "ReqMint Tests");
            await repository.RunGitAsync("config", "user.email", "reqmint-tests@example.invalid");
            return repository;
        }

        public static async Task<TemporaryGitRepository> CloneAsync(string sourcePath)
        {
            var parent = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "ReqMint.Git.Tests",
                Guid.NewGuid().ToString("N"));
            var path = System.IO.Path.Combine(parent, "clone");
            Directory.CreateDirectory(parent);
            await RunGitProcessAsync(parent, "clone", "--quiet", "--", sourcePath, path);
            var repository = new TemporaryGitRepository(path);
            await repository.RunGitAsync("config", "user.name", "ReqMint Tests");
            await repository.RunGitAsync("config", "user.email", "reqmint-tests@example.invalid");
            return repository;
        }

        public async Task WriteAsync(string relativePath, string content)
        {
            var fullPath = System.IO.Path.Combine(
                Path,
                relativePath.Replace('/', System.IO.Path.DirectorySeparatorChar));
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(fullPath)!);
            await File.WriteAllTextAsync(fullPath, content);
        }

        public Task<string> ReadAsync(string relativePath) => File.ReadAllTextAsync(
            System.IO.Path.Combine(
                Path,
                relativePath.Replace('/', System.IO.Path.DirectorySeparatorChar)));

        public async Task<string> GetHeadAsync()
        {
            var result = await RunGitProcessAsync(Path, "rev-parse", "HEAD");
            return result.Trim();
        }

        public async Task CommitAllAsync(string message)
        {
            await RunGitAsync("add", "--all");
            await RunGitAsync("commit", "--quiet", "-m", message);
        }

        public async Task RunGitAsync(params string[] arguments)
        {
            await RunGitProcessAsync(Path, arguments);
        }

        private static async Task<string> RunGitProcessAsync(
            string workingDirectory,
            params string[] arguments)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "git",
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            foreach (var argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Git could not be started for the test.");
            var standardOutput = process.StandardOutput.ReadToEndAsync();
            var standardError = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"Git test command failed: {await standardError}\n{await standardOutput}");
            }

            return await standardOutput;
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                foreach (var file in Directory.EnumerateFiles(
                    Path,
                    "*",
                    SearchOption.AllDirectories))
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                }

                Directory.Delete(Path, recursive: true);
                var parent = Directory.GetParent(Path)?.FullName;
                if (parent is not null
                    && Directory.Exists(parent)
                    && !Directory.EnumerateFileSystemEntries(parent).Any())
                {
                    Directory.Delete(parent);
                }
            }
        }
    }
}

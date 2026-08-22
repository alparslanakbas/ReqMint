using CommunityToolkit.Mvvm.Input;
using ReqMint.Core.Git;

namespace ReqMint.App.ViewModels;

public partial class MainViewModel
{
    private const int MaximumDiffPreviewLines = 5000;
    private GitFileChange? _selectedGitChange;
    private int _gitDiffLoadVersion;

    [RelayCommand]
    private async Task ShowGitAsync(CancellationToken cancellationToken)
    {
        IsHistoryVisible = false;
        IsGitVisible = true;

        if (_workspaceDirectory is not null)
        {
            await RefreshGitStatusAsync(_workspaceDirectory, cancellationToken);
        }
    }

    private async Task RefreshGitStatusAsync(
        string workspaceDirectory,
        CancellationToken cancellationToken)
    {
        try
        {
            var status = await _gitService.GetStatusAsync(workspaceDirectory, cancellationToken);
            if (status is null)
            {
                ResetGitStatus(Localize("GitNotRepository", "Workspace is not inside a Git repository"));
                return;
            }

            GitBranch = status.IsDetached
                ? Localize("GitDetachedHead", "Detached HEAD")
                : status.Branch;
            GitRepositoryRoot = status.RepositoryRoot;
            CloseGitDiff();
            GitChanges.Clear();
            var managedChanges = status.Changes
                .Where(change => ReqMintGitFileClassifier.IsManaged(change.Path))
                .ToArray();
            foreach (var change in managedChanges)
            {
                GitChanges.Add(new GitChangeItemViewModel(change, OpenGitDiffAsync));
            }

            var secretScan = managedChanges.Length == 0
                ? GitSecretScanResult.Empty
                : await _gitSecretScanner.ScanAsync(
                    workspaceDirectory,
                    managedChanges.Select(change => change.Path).ToArray(),
                    cancellationToken);
            UpdateGitSecuritySummary(secretScan, managedChanges.Length);

            GitOtherChangeCount = status.Changes.Count - managedChanges.Length;
            GitSummary = status.IsClean
                ? Localize("GitClean", "Working tree clean")
                : managedChanges.Length > 0
                    ? Localize("GitReqMintChangesCount", "{0} ReqMint file changes", managedChanges.Length)
                    : Localize("GitNoReqMintChanges", "No ReqMint file changes");
            if (GitOtherChangeCount > 0)
            {
                GitSummary += " · " + Localize(
                    "GitOtherChangesCount",
                    "{0} other repository changes",
                    GitOtherChangeCount);
            }

            if (status.AheadBy > 0)
            {
                GitSummary += " · " + Localize("GitAheadBy", "ahead {0}", status.AheadBy);
            }

            if (status.BehindBy > 0)
            {
                GitSummary += " · " + Localize("GitBehindBy", "behind {0}", status.BehindBy);
            }

            OnPropertyChanged(nameof(IsGitChangeListEmpty));
        }
        catch (OperationCanceledException)
        {
        }
        catch (GitUnavailableException)
        {
            ResetGitStatus(Localize("GitNotInstalled", "Git is not installed or available"));
        }
        catch (Exception exception)
        {
            ResetGitStatus(Localize("GitStatusFailed", "Git status could not be loaded"));
            System.Diagnostics.Debug.WriteLine(exception);
        }
    }

    private void ResetGitStatus(string summary)
    {
        GitBranch = "—";
        GitSummary = summary;
        GitRepositoryRoot = string.Empty;
        GitOtherChangeCount = 0;
        GitSecuritySummary = string.Empty;
        HasGitSecurityWarning = false;
        GitSecretWarningCount = 0;
        GitChanges.Clear();
        CloseGitDiff();
        OnPropertyChanged(nameof(IsGitChangeListEmpty));
    }

    private async Task OpenGitDiffAsync(GitFileChange change)
    {
        _selectedGitChange = change;
        GitDiffPath = change.Path;
        HasGitWorkingTreeDiff = change.HasWorkingTreeChanges;
        HasGitStagedDiff = change.HasStagedChanges;
        IsGitDiffVisible = true;
        var scope = change.HasWorkingTreeChanges
            ? GitDiffScope.WorkingTree
            : GitDiffScope.Staged;
        await LoadGitDiffAsync(scope, CancellationToken.None);
    }

    [RelayCommand]
    private Task ShowWorkingTreeDiffAsync(CancellationToken cancellationToken) =>
        LoadGitDiffAsync(GitDiffScope.WorkingTree, cancellationToken);

    [RelayCommand]
    private Task ShowStagedDiffAsync(CancellationToken cancellationToken) =>
        LoadGitDiffAsync(GitDiffScope.Staged, cancellationToken);

    [RelayCommand]
    private void CloseGitDiff()
    {
        Interlocked.Increment(ref _gitDiffLoadVersion);
        IsGitDiffVisible = false;
        GitDiffPath = string.Empty;
        GitDiffSummary = string.Empty;
        GitDiffMessage = string.Empty;
        IsGitDiffSecurityBlocked = false;
        HasGitWorkingTreeDiff = false;
        HasGitStagedDiff = false;
        GitDiffLines.Clear();
        _selectedGitChange = null;
        OnPropertyChanged(nameof(IsGitDiffLineListEmpty));
    }

    private async Task LoadGitDiffAsync(
        GitDiffScope scope,
        CancellationToken cancellationToken)
    {
        var change = _selectedGitChange;
        if (_workspaceDirectory is null
            || change is null
            || (scope == GitDiffScope.WorkingTree && !change.HasWorkingTreeChanges)
            || (scope == GitDiffScope.Staged && !change.HasStagedChanges))
        {
            return;
        }

        var loadVersion = Interlocked.Increment(ref _gitDiffLoadVersion);
        GitDiffLines.Clear();
        GitDiffMessage = Localize("GitDiffLoading", "Loading diff preview...");
        GitDiffSummary = scope == GitDiffScope.Staged
            ? Localize("GitDiffStaged", "Staged")
            : Localize("GitDiffWorkingTree", "Working tree");
        IsGitDiffSecurityBlocked = false;
        OnPropertyChanged(nameof(IsGitDiffLineListEmpty));

        try
        {
            var preview = await _gitService.GetDiffAsync(
                _workspaceDirectory,
                change.Path,
                scope,
                cancellationToken);
            if (loadVersion != _gitDiffLoadVersion)
            {
                return;
            }

            if (preview.State == GitDiffPreviewState.BlockedBySecurity)
            {
                IsGitDiffSecurityBlocked = true;
                GitDiffMessage = preview.SecurityWarningCount > 0
                    ? Localize(
                        "GitDiffBlockedBySecrets",
                        "Preview blocked because this version may contain a secret.")
                    : Localize(
                        "GitDiffBlockedByScan",
                        "Preview blocked because this version could not be inspected safely.");
                return;
            }

            if (preview.State == GitDiffPreviewState.Unavailable)
            {
                GitDiffMessage = Localize(
                    "GitDiffUnavailable",
                    "Diff preview is unavailable for this file.");
                return;
            }

            var normalizedContent = preview.Content.Replace(
                "\r\n",
                "\n",
                StringComparison.Ordinal);
            var lines = string.IsNullOrEmpty(normalizedContent)
                ? []
                : normalizedContent.Split('\n');
            if (lines.Length > 0 && lines[^1].Length == 0)
            {
                lines = lines[..^1];
            }

            var visibleLineCount = Math.Min(lines.Length, MaximumDiffPreviewLines);
            foreach (var line in lines.Take(visibleLineCount))
            {
                GitDiffLines.Add(new GitDiffLineViewModel(line));
            }

            var isTruncated = preview.IsTruncated || lines.Length > MaximumDiffPreviewLines;
            GitDiffMessage = GitDiffLines.Count == 0
                ? Localize("GitDiffEmpty", "No textual differences to display.")
                : string.Empty;
            GitDiffSummary += " · " + Localize(
                "GitDiffLinesCount",
                "{0} lines",
                GitDiffLines.Count);
            if (isTruncated)
            {
                GitDiffSummary += " · " + Localize("GitDiffTruncated", "preview limited");
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            if (loadVersion == _gitDiffLoadVersion)
            {
                GitDiffMessage = Localize(
                    "GitDiffUnavailable",
                    "Diff preview is unavailable for this file.");
            }

            System.Diagnostics.Debug.WriteLine(exception);
        }
        finally
        {
            if (loadVersion == _gitDiffLoadVersion)
            {
                OnPropertyChanged(nameof(IsGitDiffLineListEmpty));
            }
        }
    }

    private void UpdateGitSecuritySummary(GitSecretScanResult result, int managedChangeCount)
    {
        GitSecretWarningCount = result.Findings.Count;
        HasGitSecurityWarning = result.HasWarnings || !result.IsComplete;
        if (managedChangeCount == 0)
        {
            GitSecuritySummary = string.Empty;
            return;
        }

        if (result.HasWarnings)
        {
            var affectedFileCount = result.Findings
                .Select(finding => finding.Path)
                .Distinct(OperatingSystem.IsWindows()
                    ? StringComparer.OrdinalIgnoreCase
                    : StringComparer.Ordinal)
                .Count();
            GitSecuritySummary = Localize(
                "GitSecretWarnings",
                "Security check found {0} possible secret findings across {1} files",
                result.Findings.Count,
                affectedFileCount);
        }
        else
        {
            GitSecuritySummary = Localize("GitSecretScanClean", "Security check passed");
        }

        if (!result.IsComplete)
        {
            GitSecuritySummary += " · " + Localize(
                "GitSecretScanIncomplete",
                "{0} files could not be inspected",
                result.UnscannedFiles.Count);
        }
    }
}

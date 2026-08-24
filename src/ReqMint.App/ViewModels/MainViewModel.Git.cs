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
            GitRepositoryRoot = IsActiveTutorialWorkspace(workspaceDirectory)
                ? Localize("TutorialWorkspaceLocation", "Temporary local workspace")
                : status.RepositoryRoot;
            CloseGitDiff();
            CloseGitCommit();
            CloseGitRemote();
            CloseGitFastForward();
            CloseGitPush();
            GitChanges.Clear();
            var managedChanges = status.Changes
                .Where(change => ReqMintGitFileClassifier.IsManaged(change.Path))
                .ToArray();
            foreach (var change in managedChanges)
            {
                GitChanges.Add(new GitChangeItemViewModel(change, OpenGitDiffAsync));
            }

            GitConflictCount = managedChanges.Count(change => change.IsConflict);
            IsGitRemoteReviewAvailable = true;
            GitAheadCount = status.AheadBy;
            GitBehindCount = status.BehindBy;
            IsGitFastForwardReviewAvailable = status.BehindBy > 0;
            IsGitPushReviewAvailable = status.AheadBy > 0;
            var stagedReqMintCount = managedChanges.Count(change => change.HasStagedChanges);
            IsGitCommitReviewAvailable = stagedReqMintCount > 0;

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

            if (GitConflictCount > 0)
            {
                GitSummary += " · " + Localize(
                    "GitConflictsCount",
                    "Conflicts: {0}",
                    GitConflictCount);
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
        GitConflictCount = 0;
        IsGitCommitReviewAvailable = false;
        IsGitRemoteReviewAvailable = false;
        IsGitFastForwardReviewAvailable = false;
        IsGitPushReviewAvailable = false;
        GitAheadCount = 0;
        GitBehindCount = 0;
        CloseGitCommit();
        CloseGitRemote();
        CloseGitFastForward();
        CloseGitPush();
        GitChanges.Clear();
        CloseGitDiff();
        OnPropertyChanged(nameof(IsGitChangeListEmpty));
    }

    private async Task OpenGitDiffAsync(GitFileChange change)
    {
        CloseGitCommit();
        CloseGitRemote();
        CloseGitFastForward();
        CloseGitPush();
        _selectedGitChange = change;
        GitDiffPath = change.Path;
        HasGitWorkingTreeDiff = change.HasWorkingTreeChanges;
        HasGitStagedDiff = change.HasStagedChanges;
        IsGitDiffVisible = true;
        if (change.IsConflict)
        {
            HasGitWorkingTreeDiff = false;
            HasGitStagedDiff = false;
            IsGitConflictGuidanceVisible = true;
            IsGitDiffSecurityBlocked = false;
            GitDiffLines.Clear();
            GitDiffSummary = Localize("GitConflictDetected", "Merge conflict detected");
            GitDiffMessage = Localize(
                "GitConflictIntroduction",
                "ReqMint will not modify this conflicted file automatically. Resolve it explicitly, then refresh Git status.");
            OnPropertyChanged(nameof(IsGitDiffLineListEmpty));
            return;
        }

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
        IsGitConflictGuidanceVisible = false;
        IsGitStageAvailable = false;
        IsGitStageReviewVisible = false;
        IsGitStageBusy = false;
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
        IsGitConflictGuidanceVisible = false;
        IsGitStageAvailable = false;
        IsGitStageReviewVisible = false;
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

            IsGitStageAvailable = scope == GitDiffScope.WorkingTree
                && change.IsStageCandidate;

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

    [RelayCommand]
    private void ReviewGitStage()
    {
        if (!IsGitStageAvailable || IsGitStageBusy)
        {
            return;
        }

        IsGitStageReviewVisible = true;
    }

    [RelayCommand]
    private void CancelGitStageReview() => IsGitStageReviewVisible = false;

    [RelayCommand]
    private async Task ConfirmGitStageAsync(CancellationToken cancellationToken)
    {
        var change = _selectedGitChange;
        var workspaceDirectory = _workspaceDirectory;
        if (!IsGitStageReviewVisible
            || !IsGitStageAvailable
            || IsGitStageBusy
            || change is null
            || workspaceDirectory is null)
        {
            return;
        }

        IsGitStageBusy = true;
        try
        {
            var result = await _gitService.StageFileAsync(
                workspaceDirectory,
                change.Path,
                cancellationToken);
            IsGitStageReviewVisible = false;
            IsGitStageAvailable = false;
            if (result.State == GitStageResultState.BlockedBySecurity)
            {
                IsGitDiffSecurityBlocked = true;
                GitDiffLines.Clear();
                GitDiffMessage = result.SecurityWarningCount > 0
                    ? Localize(
                        "GitStageBlockedBySecrets",
                        "Staging blocked because this file may contain a secret.")
                    : Localize(
                        "GitStageBlockedByScan",
                        "Staging blocked because this file could not be inspected safely.");
                OnPropertyChanged(nameof(IsGitDiffLineListEmpty));
                return;
            }

            if (result.State == GitStageResultState.NotEligible)
            {
                WorkspaceStatus = Localize(
                    "GitStageNoLongerEligible",
                    "The file changed and was not staged. Git status was refreshed.");
                await RefreshGitStatusAsync(workspaceDirectory, cancellationToken);
                return;
            }

            WorkspaceStatus = Localize("GitStageCompleted", "File staged safely");
            await RefreshGitStatusAsync(workspaceDirectory, cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            IsGitStageReviewVisible = false;
            WorkspaceStatus = Localize("GitStageFailed", "File could not be staged");
            System.Diagnostics.Debug.WriteLine(exception);
        }
        finally
        {
            IsGitStageBusy = false;
        }
    }

    [RelayCommand]
    private async Task ReviewGitCommitAsync(CancellationToken cancellationToken)
    {
        var workspaceDirectory = _workspaceDirectory;
        if (!IsGitCommitReviewAvailable
            || IsGitCommitBusy
            || workspaceDirectory is null)
        {
            return;
        }

        IsGitCommitBusy = true;
        GitCommitValidationMessage = string.Empty;
        try
        {
            var preflight = await _gitService.GetCommitPreflightAsync(
                workspaceDirectory,
                cancellationToken);
            if (!preflight.IsReady)
            {
                ShowCommitPreflightFailure(preflight);
                await RefreshGitStatusAsync(workspaceDirectory, cancellationToken);
                return;
            }

            CloseGitDiff();
            CloseGitRemote();
            CloseGitFastForward();
            CloseGitPush();
            GitCommitFiles.Clear();
            foreach (var path in preflight.StagedPaths)
            {
                GitCommitFiles.Add(path);
            }

            GitCommitSummary = Localize(
                "GitCommitReadySummary",
                "{0} staged ReqMint files passed the final security check",
                preflight.StagedPaths.Count);
            IsGitCommitVisible = true;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            WorkspaceStatus = Localize(
                "GitCommitPreflightFailed",
                "Commit safety check could not be completed");
            System.Diagnostics.Debug.WriteLine(exception);
        }
        finally
        {
            IsGitCommitBusy = false;
        }
    }

    [RelayCommand]
    private void CancelGitCommit() => CloseGitCommit();

    [RelayCommand]
    private async Task ConfirmGitCommitAsync(CancellationToken cancellationToken)
    {
        var workspaceDirectory = _workspaceDirectory;
        if (!IsGitCommitVisible || IsGitCommitBusy || workspaceDirectory is null)
        {
            return;
        }

        if (!GitCommitMessageValidator.IsValid(GitCommitMessage))
        {
            GitCommitValidationMessage = Localize(
                "GitCommitMessageInvalid",
                "Use a single-line commit message between 3 and 72 characters.");
            return;
        }

        IsGitCommitBusy = true;
        GitCommitValidationMessage = string.Empty;
        try
        {
            var result = await _gitService.CommitAsync(
                workspaceDirectory,
                GitCommitMessage,
                cancellationToken);
            if (result.State == GitCommitResultState.InvalidMessage)
            {
                GitCommitValidationMessage = Localize(
                    "GitCommitMessageInvalid",
                    "Use a single-line commit message between 3 and 72 characters.");
                return;
            }

            if (result.State == GitCommitResultState.PreflightBlocked)
            {
                CloseGitCommit();
                ShowCommitPreflightFailure(result.Preflight);
                await RefreshGitStatusAsync(workspaceDirectory, cancellationToken);
                return;
            }

            CloseGitCommit();
            WorkspaceStatus = string.IsNullOrEmpty(result.CommitId)
                ? Localize("GitCommitCompleted", "Commit created safely")
                : Localize(
                    "GitCommitCompletedWithId",
                    "Commit {0} created safely",
                    result.CommitId);
            await RefreshGitStatusAsync(workspaceDirectory, cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            WorkspaceStatus = Localize("GitCommitFailed", "Commit could not be created");
            System.Diagnostics.Debug.WriteLine(exception);
        }
        finally
        {
            IsGitCommitBusy = false;
        }
    }

    private void CloseGitCommit()
    {
        IsGitCommitVisible = false;
        IsGitCommitBusy = false;
        GitCommitSummary = string.Empty;
        GitCommitValidationMessage = string.Empty;
        GitCommitFiles.Clear();
    }

    private void ShowCommitPreflightFailure(GitCommitPreflight preflight)
    {
        WorkspaceStatus = preflight.State switch
        {
            GitCommitPreflightState.Conflicts => Localize(
                "GitCommitBlockedByConflicts",
                "Commit blocked until all merge conflicts are resolved"),
            GitCommitPreflightState.ContainsOtherStagedFiles => Localize(
                "GitCommitBlockedByScope",
                "Commit blocked because non-ReqMint files are staged"),
            GitCommitPreflightState.BlockedBySecurity when preflight.SecurityWarningCount > 0 =>
                Localize(
                    "GitCommitBlockedBySecrets",
                    "Commit blocked because the Git index may contain a secret"),
            GitCommitPreflightState.BlockedBySecurity => Localize(
                "GitCommitBlockedByScan",
                "Commit blocked because the Git index could not be inspected safely"),
            _ => Localize(
                "GitCommitNoStagedFiles",
                "There are no staged ReqMint files to commit"),
        };
    }

    [RelayCommand]
    private async Task ReviewGitRemoteAsync(CancellationToken cancellationToken)
    {
        var workspaceDirectory = _workspaceDirectory;
        if (!IsGitRemoteReviewAvailable
            || IsGitRemoteBusy
            || workspaceDirectory is null)
        {
            return;
        }

        IsGitRemoteBusy = true;
        try
        {
            var preflight = await _gitService.GetRemotePreflightAsync(
                workspaceDirectory,
                cancellationToken);
            if (!preflight.IsReady)
            {
                ShowRemotePreflightFailure(preflight);
                return;
            }

            CloseGitDiff();
            CloseGitCommit();
            CloseGitFastForward();
            CloseGitPush();
            GitRemoteName = preflight.RemoteName;
            GitRemoteBranch = preflight.Branch;
            GitRemoteSummary = Localize(
                "GitRemoteReviewMessage",
                "Fetch remote-tracking updates from {0}/{1} without changing your working tree or current branch.",
                preflight.RemoteName,
                preflight.Branch);
            IsGitRemoteVisible = true;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            WorkspaceStatus = Localize(
                "GitRemotePreflightFailed",
                "Remote configuration could not be inspected");
            System.Diagnostics.Debug.WriteLine(exception);
        }
        finally
        {
            IsGitRemoteBusy = false;
        }
    }

    [RelayCommand]
    private void CancelGitRemote() => CloseGitRemote();

    [RelayCommand]
    private async Task ConfirmGitFetchAsync(CancellationToken cancellationToken)
    {
        var workspaceDirectory = _workspaceDirectory;
        if (!IsGitRemoteVisible || IsGitRemoteBusy || workspaceDirectory is null)
        {
            return;
        }

        IsGitRemoteBusy = true;
        try
        {
            var result = await _gitService.FetchAsync(workspaceDirectory, cancellationToken);
            if (result.State == GitFetchResultState.PreflightBlocked)
            {
                CloseGitRemote();
                ShowRemotePreflightFailure(result.Preflight);
                return;
            }

            CloseGitRemote();
            WorkspaceStatus = Localize(
                "GitFetchCompleted",
                "Remote check complete · behind {0} · ahead {1}",
                result.BehindBy,
                result.AheadBy);
            await RefreshGitStatusAsync(workspaceDirectory, cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            WorkspaceStatus = Localize(
                "GitFetchFailed",
                "Remote check failed; no local files were changed");
            System.Diagnostics.Debug.WriteLine(exception);
        }
        finally
        {
            IsGitRemoteBusy = false;
        }
    }

    private void CloseGitRemote()
    {
        IsGitRemoteVisible = false;
        IsGitRemoteBusy = false;
        GitRemoteName = string.Empty;
        GitRemoteBranch = string.Empty;
        GitRemoteSummary = string.Empty;
    }

    private void ShowRemotePreflightFailure(GitRemotePreflight preflight)
    {
        WorkspaceStatus = preflight.State switch
        {
            GitRemotePreflightState.DetachedHead => Localize(
                "GitRemoteDetachedHead",
                "Remote check requires an active local branch"),
            GitRemotePreflightState.UnsupportedRemote => Localize(
                "GitRemoteUnsupported",
                "This upstream configuration is not supported safely"),
            _ => Localize(
                "GitRemoteNoUpstream",
                "The current branch has no upstream remote"),
        };
    }

    [RelayCommand]
    private async Task ReviewGitFastForwardAsync(CancellationToken cancellationToken)
    {
        var workspaceDirectory = _workspaceDirectory;
        if (!IsGitFastForwardReviewAvailable
            || IsGitFastForwardBusy
            || IsWorkspaceBusy
            || IsSending
            || workspaceDirectory is null)
        {
            return;
        }

        if (HasUnsavedWorkspaceChanges())
        {
            WorkspaceStatus = Localize(
                "GitFastForwardUnsavedDraft",
                "Save or discard current workspace edits before updating");
            return;
        }

        IsGitFastForwardBusy = true;
        try
        {
            var preflight = await _gitService.GetFastForwardPreflightAsync(
                workspaceDirectory,
                cancellationToken);
            if (!preflight.IsReady)
            {
                ShowFastForwardPreflightFailure(preflight);
                return;
            }

            CloseGitDiff();
            CloseGitCommit();
            CloseGitRemote();
            CloseGitPush();
            GitFastForwardCommits.Clear();
            foreach (var summary in preflight.CommitSummaries)
            {
                GitFastForwardCommits.Add(summary);
            }

            GitFastForwardPaths.Clear();
            foreach (var path in preflight.ChangedPaths)
            {
                GitFastForwardPaths.Add(path);
            }

            GitFastForwardSummary = Localize(
                "GitFastForwardReviewSummary",
                "Review {0} incoming commits from {1}",
                preflight.Remote.BehindBy,
                $"{preflight.Remote.RemoteName}/{preflight.Remote.Branch}");
            IsGitFastForwardVisible = true;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            WorkspaceStatus = Localize(
                "GitFastForwardPreviewFailed",
                "Incoming update preview could not be created");
            System.Diagnostics.Debug.WriteLine(exception);
        }
        finally
        {
            IsGitFastForwardBusy = false;
        }
    }

    [RelayCommand]
    private void CancelGitFastForward() => CloseGitFastForward();

    [RelayCommand]
    private async Task ConfirmGitFastForwardAsync(CancellationToken cancellationToken)
    {
        var workspaceDirectory = _workspaceDirectory;
        if (!IsGitFastForwardVisible
            || IsGitFastForwardBusy
            || IsWorkspaceBusy
            || IsSending
            || workspaceDirectory is null)
        {
            return;
        }

        if (HasUnsavedWorkspaceChanges())
        {
            WorkspaceStatus = Localize(
                "GitFastForwardUnsavedDraft",
                "Save or discard current workspace edits before updating");
            return;
        }

        IsGitFastForwardBusy = true;
        IsWorkspaceBusy = true;
        try
        {
            var result = await _gitService.FastForwardAsync(
                workspaceDirectory,
                cancellationToken);
            if (result.State == GitFastForwardResultState.PreflightBlocked)
            {
                CloseGitFastForward();
                ShowFastForwardPreflightFailure(result.Preflight);
                await RefreshGitStatusAsync(workspaceDirectory, cancellationToken);
                return;
            }

            CloseGitFastForward();
            try
            {
                var snapshot = await _workspaceStore.LoadAsync(
                    workspaceDirectory,
                    CancellationToken.None);
                ApplyWorkspace(snapshot, workspaceDirectory);
                ResetRequestDraft();
                await RefreshGitStatusAsync(workspaceDirectory, CancellationToken.None);
                WorkspaceStatus = Localize(
                    "GitFastForwardCompleted",
                    "Workspace updated {0} → {1}",
                    result.PreviousCommitId,
                    result.CurrentCommitId);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                WorkspaceStatus = Localize(
                    "GitFastForwardReloadFailed",
                    "Git update completed, but the workspace must be reopened");
                System.Diagnostics.Debug.WriteLine(exception);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            await RefreshGitStatusAsync(workspaceDirectory, CancellationToken.None);
            WorkspaceStatus = Localize(
                "GitFastForwardFailed",
                "Workspace update could not be completed; check Git status");
            System.Diagnostics.Debug.WriteLine(exception);
        }
        finally
        {
            IsGitFastForwardBusy = false;
            IsWorkspaceBusy = false;
        }
    }

    private void CloseGitFastForward()
    {
        IsGitFastForwardVisible = false;
        IsGitFastForwardBusy = false;
        GitFastForwardSummary = string.Empty;
        GitFastForwardCommits.Clear();
        GitFastForwardPaths.Clear();
    }

    private void ShowFastForwardPreflightFailure(GitFastForwardPreflight preflight)
    {
        WorkspaceStatus = preflight.State switch
        {
            GitFastForwardPreflightState.WorkingTreeDirty => Localize(
                "GitFastForwardDirty",
                "Update blocked because the repository has local file changes"),
            GitFastForwardPreflightState.Conflicts => Localize(
                "GitFastForwardConflicts",
                "Update blocked until all merge conflicts are resolved"),
            GitFastForwardPreflightState.Diverged => Localize(
                "GitFastForwardDiverged",
                "Update blocked because local and remote branches have diverged"),
            GitFastForwardPreflightState.PreviewUnavailable => Localize(
                "GitFastForwardPreviewFailed",
                "Incoming update preview could not be created"),
            GitFastForwardPreflightState.PreviewTooLarge => Localize(
                "GitFastForwardPreviewTooLarge",
                "Update blocked because the incoming scope is too large to review safely"),
            GitFastForwardPreflightState.ContainsOtherFiles => Localize(
                "GitFastForwardContainsOtherFiles",
                "Update blocked because incoming commits change files outside ReqMint's scope"),
            GitFastForwardPreflightState.RemoteUnavailable => Localize(
                "GitFastForwardRemoteUnavailable",
                "Update blocked because the upstream remote is unavailable"),
            _ => Localize(
                "GitFastForwardNoUpdates",
                "There are no incoming fast-forward updates"),
        };
    }

    [RelayCommand]
    private async Task ReviewGitPushAsync(CancellationToken cancellationToken)
    {
        var workspaceDirectory = _workspaceDirectory;
        if (!IsGitPushReviewAvailable
            || IsGitPushBusy
            || IsSending
            || workspaceDirectory is null)
        {
            return;
        }

        IsGitPushBusy = true;
        try
        {
            var preflight = await _gitService.GetPushPreflightAsync(
                workspaceDirectory,
                cancellationToken);
            if (!preflight.IsReady)
            {
                ShowPushPreflightFailure(preflight);
                return;
            }

            CloseGitDiff();
            CloseGitCommit();
            CloseGitRemote();
            CloseGitFastForward();
            GitPushCommits.Clear();
            foreach (var summary in preflight.CommitSummaries)
            {
                GitPushCommits.Add(summary);
            }

            GitPushPaths.Clear();
            foreach (var path in preflight.ChangedPaths)
            {
                GitPushPaths.Add(path);
            }

            GitPushSummary = Localize(
                "GitPushReviewSummary",
                "Review {0} outgoing commits to {1}",
                preflight.CommitSummaries.Count,
                $"{preflight.Remote.RemoteName}/{preflight.Remote.Branch}");
            IsGitPushVisible = true;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            WorkspaceStatus = Localize(
                "GitPushPreviewFailed",
                "Outgoing push preview could not be created");
            System.Diagnostics.Debug.WriteLine(exception);
        }
        finally
        {
            IsGitPushBusy = false;
        }
    }

    [RelayCommand]
    private void CancelGitPush() => CloseGitPush();

    [RelayCommand]
    private async Task ConfirmGitPushAsync(CancellationToken cancellationToken)
    {
        var workspaceDirectory = _workspaceDirectory;
        if (!IsGitPushVisible
            || IsGitPushBusy
            || IsSending
            || workspaceDirectory is null)
        {
            return;
        }

        IsGitPushBusy = true;
        try
        {
            var result = await _gitService.PushAsync(workspaceDirectory, cancellationToken);
            if (result.State == GitPushResultState.PreflightBlocked)
            {
                CloseGitPush();
                ShowPushPreflightFailure(result.Preflight);
                await RefreshGitStatusAsync(workspaceDirectory, cancellationToken);
                return;
            }

            var pushedCommitCount = result.Preflight.CommitSummaries.Count;
            var destination =
                $"{result.Preflight.Remote.RemoteName}/{result.Preflight.Remote.Branch}";
            CloseGitPush();
            WorkspaceStatus = Localize(
                "GitPushCompleted",
                "Pushed {0} commits to {1}",
                pushedCommitCount,
                destination);
            await RefreshGitStatusAsync(workspaceDirectory, cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            await RefreshGitStatusAsync(workspaceDirectory, CancellationToken.None);
            WorkspaceStatus = Localize(
                "GitPushFailed",
                "Push could not be completed; no force push was attempted");
            System.Diagnostics.Debug.WriteLine(exception);
        }
        finally
        {
            IsGitPushBusy = false;
        }
    }

    private void CloseGitPush()
    {
        IsGitPushVisible = false;
        IsGitPushBusy = false;
        GitPushSummary = string.Empty;
        GitPushCommits.Clear();
        GitPushPaths.Clear();
    }

    private void ShowPushPreflightFailure(GitPushPreflight preflight)
    {
        WorkspaceStatus = preflight.State switch
        {
            GitPushPreflightState.WorkingTreeDirty => Localize(
                "GitPushDirty",
                "Push blocked because the repository has local file changes"),
            GitPushPreflightState.Conflicts => Localize(
                "GitPushConflicts",
                "Push blocked until all merge conflicts are resolved"),
            GitPushPreflightState.BehindRemote => Localize(
                "GitPushBehind",
                "Push blocked because the remote branch has newer commits"),
            GitPushPreflightState.Diverged => Localize(
                "GitPushDiverged",
                "Push blocked because local and remote branches have diverged"),
            GitPushPreflightState.PreviewUnavailable => Localize(
                "GitPushPreviewFailed",
                "Outgoing push preview could not be created"),
            GitPushPreflightState.PreviewTooLarge => Localize(
                "GitPushPreviewTooLarge",
                "Push blocked because the outgoing scope is too large to inspect safely"),
            GitPushPreflightState.ContainsOtherFiles => Localize(
                "GitPushContainsOtherFiles",
                "Push blocked because outgoing commits include files outside ReqMint's scope"),
            GitPushPreflightState.BlockedBySecurity when preflight.SecurityWarningCount > 0 =>
                Localize(
                    "GitPushBlockedBySecrets",
                    "Push blocked because an outgoing commit snapshot may contain a secret"),
            GitPushPreflightState.BlockedBySecurity => Localize(
                "GitPushBlockedByScan",
                "Push blocked because every outgoing snapshot could not be inspected safely"),
            GitPushPreflightState.RemoteUnavailable => Localize(
                "GitPushRemoteUnavailable",
                "Push blocked because the upstream remote is unavailable"),
            _ => Localize(
                "GitPushNoCommits",
                "There are no outgoing commits to push"),
        };
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

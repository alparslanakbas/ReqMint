using CommunityToolkit.Mvvm.Input;
using ReqMint.Core.Git;

namespace ReqMint.App.ViewModels;

public partial class MainViewModel
{
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
            GitChanges.Clear();
            var managedChanges = status.Changes
                .Where(change => ReqMintGitFileClassifier.IsManaged(change.Path))
                .ToArray();
            foreach (var change in managedChanges)
            {
                GitChanges.Add(new GitChangeItemViewModel(change));
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
        OnPropertyChanged(nameof(IsGitChangeListEmpty));
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

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
            foreach (var change in status.Changes)
            {
                GitChanges.Add(new GitChangeItemViewModel(change));
            }

            GitSummary = status.IsClean
                ? Localize("GitClean", "Working tree clean")
                : Localize("GitChangesCount", "{0} changed files", status.Changes.Count);
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
        GitChanges.Clear();
        OnPropertyChanged(nameof(IsGitChangeListEmpty));
    }
}

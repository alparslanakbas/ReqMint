using CommunityToolkit.Mvvm.Input;
using ReqMint.Core.Git;

namespace ReqMint.App.ViewModels;

public sealed class GitChangeItemViewModel : ViewModelBase
{
    public GitChangeItemViewModel(
        GitFileChange change,
        Func<GitFileChange, Task> openDiff)
    {
        Change = change;
        OpenCommand = new AsyncRelayCommand(() => openDiff(Change));
    }

    public GitFileChange Change { get; }

    public string Path => Change.Path;

    public string Status => Change.IsConflict ? "!" : Change.Status.Trim() switch
    {
        "??" => "?",
        "M" => "M",
        "A" => "A",
        "D" => "D",
        "R" => "R",
        "C" => "C",
        "U" or "UU" => "!",
        var status => status,
    };

    public bool HasStagedChanges => Change.HasStagedChanges;

    public bool HasWorkingTreeChanges => Change.HasWorkingTreeChanges;

    public bool IsConflict => Change.IsConflict;

    public IAsyncRelayCommand OpenCommand { get; }
}

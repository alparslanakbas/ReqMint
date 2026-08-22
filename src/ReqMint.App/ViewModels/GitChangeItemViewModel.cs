using ReqMint.Core.Git;

namespace ReqMint.App.ViewModels;

public sealed class GitChangeItemViewModel(GitFileChange change) : ViewModelBase
{
    public string Path { get; } = change.Path;

    public string Status { get; } = change.Status.Trim() switch
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
}

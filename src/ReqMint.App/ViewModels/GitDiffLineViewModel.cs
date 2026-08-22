namespace ReqMint.App.ViewModels;

public sealed class GitDiffLineViewModel(string text) : ViewModelBase
{
    public string Text { get; } = text;

    public bool IsAdded => Text.StartsWith('+') && !Text.StartsWith("+++", StringComparison.Ordinal);

    public bool IsRemoved => Text.StartsWith('-') && !Text.StartsWith("---", StringComparison.Ordinal);

    public bool IsHunk => Text.StartsWith("@@", StringComparison.Ordinal);

    public bool IsHeader => Text.StartsWith("diff ", StringComparison.Ordinal)
        || Text.StartsWith("index ", StringComparison.Ordinal)
        || Text.StartsWith("---", StringComparison.Ordinal)
        || Text.StartsWith("+++", StringComparison.Ordinal)
        || Text.StartsWith("new file ", StringComparison.Ordinal)
        || Text.StartsWith("deleted file ", StringComparison.Ordinal);
}

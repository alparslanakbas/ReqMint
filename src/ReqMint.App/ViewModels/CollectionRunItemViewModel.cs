namespace ReqMint.App.ViewModels;

public sealed record CollectionRunItemViewModel(
    string Name,
    string Status,
    string Detail,
    string Duration,
    string Assertions,
    ReqMint.Core.Runner.CollectionRequestRunState State);

public enum CollectionRunResultFilter
{
    All,
    Passed,
    Failed,
    Skipped,
}

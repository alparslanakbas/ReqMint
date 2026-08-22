using ReqMint.Core.Runner;

namespace ReqMint.App.ViewModels;

public sealed record CollectionRunHistoryItemViewModel(
    string RecordedAt,
    string Summary,
    CollectionRunHistoryEntry Entry);

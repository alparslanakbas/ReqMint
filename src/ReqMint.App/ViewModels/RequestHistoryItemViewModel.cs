using CommunityToolkit.Mvvm.Input;
using ReqMint.Core.History;

namespace ReqMint.App.ViewModels;

public sealed class RequestHistoryItemViewModel : ViewModelBase
{
    public RequestHistoryItemViewModel(
        RequestHistoryEntry entry,
        Func<RequestHistoryEntry, Task> openEntry)
    {
        Entry = entry;
        OpenCommand = new AsyncRelayCommand(() => openEntry(Entry));
    }

    public RequestHistoryEntry Entry { get; }

    public string Name => Entry.Request.Name;

    public string Method => Entry.Request.Method;

    public string Url => Entry.Request.Url;

    public string SentAt => Entry.SentAtUtc.ToLocalTime().ToString("g");

    public string Status => Entry.StatusCode?.ToString() ?? Entry.Outcome;

    public IAsyncRelayCommand OpenCommand { get; }
}

using CommunityToolkit.Mvvm.Input;
using ReqMint.Core.History;

namespace ReqMint.App.ViewModels;

public partial class MainViewModel
{
    [RelayCommand]
    private void ShowCollections() => IsHistoryVisible = false;

    [RelayCommand]
    private async Task ShowHistoryAsync(CancellationToken cancellationToken)
    {
        IsHistoryVisible = true;
        await LoadHistoryAsync(_workspaceSnapshot?.Workspace.Id ?? Guid.Empty, cancellationToken);
    }

    private async Task LoadHistoryAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        try
        {
            var entries = await _historyStore.ListAsync(workspaceId, cancellationToken: cancellationToken);
            History.Clear();
            foreach (var entry in entries)
            {
                History.Add(new RequestHistoryItemViewModel(entry, OpenHistoryEntryAsync));
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            History.Clear();
            WorkspaceStatus = Localize("StatusHistoryUnavailable", "History is unavailable");
            ResponseBody = exception.Message;
        }
    }

    private async Task OpenHistoryEntryAsync(RequestHistoryEntry entry)
    {
        if (!await ConfirmNavigationAsync(CancellationToken.None))
        {
            return;
        }

        _selectedRequestId = null;
        LoadRequestDraft(entry.Request);
        ResponseStatus = entry.StatusCode is null
            ? entry.Outcome
            : $"{entry.StatusCode} {entry.ReasonPhrase}".TrimEnd();
        ResponseTime = entry.DurationMilliseconds is null
            ? "—"
            : $"{entry.DurationMilliseconds:N0} ms";
        ResponseBody = Localize(
            "HistoryResponseNotStored",
            "Response bodies are not stored in history. Send the request again to inspect its response.");
        WorkspaceStatus = Localize("StatusOpenedHistory", "Opened history entry");
        MarkRequestClean();
    }
}

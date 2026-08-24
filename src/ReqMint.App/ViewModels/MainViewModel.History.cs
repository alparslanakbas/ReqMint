using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReqMint.App.Services;
using ReqMint.Core.History;

namespace ReqMint.App.ViewModels;

public partial class MainViewModel
{
    private readonly List<RequestHistoryEntry> _historyEntries = [];

    [ObservableProperty]
    public partial string HistorySearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial decimal HistoryRetentionLimit { get; set; } = 200;

    public bool IsHistoryEmpty => History.Count == 0;

    partial void OnHistorySearchTextChanged(string value) => ApplyHistoryFilter();

    partial void OnHistoryRetentionLimitChanged(decimal value)
    {
        var limit = (int)Math.Clamp(
            value,
            JsonAppSettingsService.MinimumHistoryRetentionLimit,
            JsonAppSettingsService.MaximumHistoryRetentionLimit);
        if (value != limit)
        {
            HistoryRetentionLimit = limit;
            return;
        }

        if (_appSettings is not null && _appSettings.Current.HistoryRetentionLimit != limit)
        {
            _appSettings.Update(_appSettings.Current with { HistoryRetentionLimit = limit });
        }
    }

    [RelayCommand]
    private void ShowCollections()
    {
        IsApplicationSettingsVisible = false;
        IsHistoryVisible = false;
        IsGitVisible = false;
        CloseGitDiff();
    }

    [RelayCommand]
    private async Task ShowHistoryAsync(CancellationToken cancellationToken)
    {
        IsApplicationSettingsVisible = false;
        IsHistoryVisible = true;
        IsGitVisible = false;
        CloseGitDiff();
        await LoadHistoryAsync(_workspaceSnapshot?.Workspace.Id ?? Guid.Empty, cancellationToken);
    }

    private async Task LoadHistoryAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        try
        {
            var entries = await _historyStore.ListAsync(
                workspaceId,
                (int)HistoryRetentionLimit,
                cancellationToken);
            _historyEntries.Clear();
            _historyEntries.AddRange(entries);
            ApplyHistoryFilter();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            _historyEntries.Clear();
            History.Clear();
            OnPropertyChanged(nameof(IsHistoryEmpty));
            WorkspaceStatus = Localize("StatusHistoryUnavailable", "History is unavailable");
            ResponseBody = exception.Message;
        }
    }

    [RelayCommand]
    private async Task ClearHistoryAsync(CancellationToken cancellationToken)
    {
        if (_historyEntries.Count == 0)
        {
            return;
        }

        var confirmed = await _historyClearPrompt.ShowAsync(WorkspaceName, _historyEntries.Count);
        if (!confirmed)
        {
            return;
        }

        try
        {
            var workspaceId = _workspaceSnapshot?.Workspace.Id ?? Guid.Empty;
            await _historyStore.ClearAsync(workspaceId, cancellationToken);
            _historyEntries.Clear();
            ApplyHistoryFilter();
            WorkspaceStatus = Localize("StatusHistoryCleared", "Request history cleared");
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            WorkspaceStatus = Localize("StatusHistoryClearFailed", "Request history could not be cleared");
            ResponseBody = exception.Message;
        }
    }

    private void ApplyHistoryFilter()
    {
        var terms = HistorySearchText.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var entries = terms.Length == 0
            ? _historyEntries
            : _historyEntries.Where(entry => terms.All(term => MatchesHistory(entry, term)));

        History.Clear();
        foreach (var entry in entries)
        {
            History.Add(new RequestHistoryItemViewModel(entry, OpenHistoryEntryAsync));
        }

        OnPropertyChanged(nameof(IsHistoryEmpty));
    }

    private static bool MatchesHistory(RequestHistoryEntry entry, string term) =>
        entry.Request.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
        entry.Request.Method.Contains(term, StringComparison.OrdinalIgnoreCase) ||
        entry.Request.Url.Contains(term, StringComparison.OrdinalIgnoreCase) ||
        entry.Outcome.Contains(term, StringComparison.OrdinalIgnoreCase) ||
        entry.ReasonPhrase?.Contains(term, StringComparison.OrdinalIgnoreCase) == true ||
        entry.StatusCode?.ToString().Contains(term, StringComparison.OrdinalIgnoreCase) == true;

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

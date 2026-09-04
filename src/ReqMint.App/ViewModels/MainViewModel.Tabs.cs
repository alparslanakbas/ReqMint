using ReqMint.App.Services;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReqMint.Core.Requests;
using ReqMint.Core.Workspaces;

namespace ReqMint.App.ViewModels;

public partial class MainViewModel
{
    /// <summary>
    /// Composer properties that change a tab's title, method colour or dirty
    /// marker. Kept in one place so <see cref="OnPropertyChanged"/> stays cheap.
    /// </summary>
    private static readonly HashSet<string> TabAffectingProperties = new(StringComparer.Ordinal)
    {
        nameof(RequestName),
        nameof(SelectedMethod),
        nameof(Url),
        nameof(SelectedBodyType),
        nameof(RequestBody),
        nameof(TimeoutSeconds),
        nameof(IsStatusAssertionEnabled),
        nameof(AssertionExpectedStatusCode),
        nameof(IsDurationAssertionEnabled),
        nameof(AssertionMaximumDurationMilliseconds),
        nameof(IsJsonFieldAssertionEnabled),
        nameof(AssertionJsonPointer),
    };

    public ObservableCollection<RequestTabViewModel> Tabs { get; } = [];

    [ObservableProperty]
    public partial RequestTabViewModel? ActiveTab { get; set; }

    public bool HasMultipleTabs => Tabs.Count > 1;

    private bool _isSwitchingTabs;

    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs args)
    {
        base.OnPropertyChanged(args);

        if (_isSwitchingTabs || args.PropertyName is null)
        {
            return;
        }

        if (TabAffectingProperties.Contains(args.PropertyName))
        {
            RefreshActiveTabHeader();
        }
    }

    [RelayCommand]
    private async Task NewTabAsync()
    {
        CaptureActiveTab();
        var tab = CreateTab();
        Tabs.Add(tab);
        await ActivateTabAsync(tab, captureCurrent: false);
        ResetRequestDraft();
        RefreshActiveTabHeader();
        await Task.CompletedTask;
    }

    private RequestTabViewModel CreateTab() => new(
        SelectTabAsync,
        CloseTabAsync,
        MoveTab);

    private async Task SelectTabAsync(RequestTabViewModel tab)
    {
        if (ReferenceEquals(tab, ActiveTab))
        {
            return;
        }

        await ActivateTabAsync(tab, captureCurrent: true);
    }

    private Task ActivateTabAsync(RequestTabViewModel tab, bool captureCurrent)
    {
        if (captureCurrent)
        {
            CaptureActiveTab();
        }

        ActiveTab = tab;
        foreach (var candidate in Tabs)
        {
            candidate.IsSelected = ReferenceEquals(candidate, tab);
        }

        RestoreTab(tab);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Closes a tab, asking about unsaved work first. Closing the last tab
    /// leaves a fresh empty one behind so the composer is never orphaned.
    /// </summary>
    private async Task CloseTabAsync(RequestTabViewModel tab)
    {
        var isActive = ReferenceEquals(tab, ActiveTab);
        if (isActive)
        {
            CaptureActiveTab();
        }

        if (tab.HasUnsavedChanges && !await ConfirmTabCloseAsync(tab))
        {
            return;
        }

        var index = Tabs.IndexOf(tab);
        if (index < 0)
        {
            return;
        }

        Tabs.Remove(tab);
        OnPropertyChanged(nameof(HasMultipleTabs));

        if (Tabs.Count == 0)
        {
            var replacement = CreateTab();
            Tabs.Add(replacement);
            await ActivateTabAsync(replacement, captureCurrent: false);
            ResetRequestDraft();
            RefreshActiveTabHeader();
            return;
        }

        if (isActive)
        {
            var next = Tabs[Math.Min(index, Tabs.Count - 1)];
            await ActivateTabAsync(next, captureCurrent: false);
        }

        RefreshTabSubtitles();
    }

    private async Task<bool> ConfirmTabCloseAsync(RequestTabViewModel tab)
    {
        var choice = await _unsavedChangesPrompt.ShowAsync(tab.Title, _workspaceSnapshot is not null);
        if (choice == UnsavedChangesChoice.Discard)
        {
            return true;
        }

        if (choice != UnsavedChangesChoice.Save || _workspaceSnapshot is null)
        {
            return false;
        }

        await SaveRequestAsync(CancellationToken.None);
        return !HasUnsavedRequestChanges();
    }

    private void MoveTab(RequestTabViewModel tab, int offset)
    {
        var index = Tabs.IndexOf(tab);
        var target = index + offset;
        if (index < 0 || target < 0 || target >= Tabs.Count)
        {
            return;
        }

        Tabs.Move(index, target);
    }

    /// <summary>Reorders a tab, used by the drag and drop handler in the view.</summary>
    public void MoveTabTo(RequestTabViewModel tab, int targetIndex)
    {
        var index = Tabs.IndexOf(tab);
        if (index < 0 || targetIndex < 0 || targetIndex >= Tabs.Count || index == targetIndex)
        {
            return;
        }

        Tabs.Move(index, targetIndex);
    }

    /// <summary>
    /// Opens a saved request in a tab, reusing the tab that already holds it.
    /// </summary>
    private async Task OpenRequestInTabAsync(RequestDocument request, Guid collectionId)
    {
        var existing = Tabs.FirstOrDefault(tab => tab.RequestId == request.Id);
        if (existing is not null)
        {
            await ActivateTabAsync(existing, captureCurrent: true);
            return;
        }

        CaptureActiveTab();

        // An untouched, unsaved tab is a scratch pad: reuse it instead of piling
        // up empty tabs every time a request is opened.
        var target = ActiveTab is { RequestId: null, HasUnsavedChanges: false } scratch
            ? scratch
            : AddTab();

        target.RequestId = request.Id;
        target.CollectionId = collectionId;
        target.State = new RequestTabState();
        await ActivateTabAsync(target, captureCurrent: false);
    }

    private RequestTabViewModel AddTab()
    {
        var tab = CreateTab();
        Tabs.Add(tab);
        OnPropertyChanged(nameof(HasMultipleTabs));
        return tab;
    }

    private void CaptureActiveTab()
    {
        if (ActiveTab is not { } tab)
        {
            return;
        }

        tab.State = new RequestTabState
        {
            RequestName = RequestName,
            Method = SelectedMethod,
            Url = Url,
            BodyType = SelectedBodyType,
            Body = RequestBody,
            AuthenticationTypeIndex = SelectedAuthenticationTypeIndex,
            AuthenticationBearerToken = AuthenticationBearerToken,
            AuthenticationBasicUsername = AuthenticationBasicUsername,
            AuthenticationBasicPassword = AuthenticationBasicPassword,
            AuthenticationApiKeyName = AuthenticationApiKeyName,
            AuthenticationApiKeyValue = AuthenticationApiKeyValue,
            AuthenticationApiKeyLocationIndex = AuthenticationApiKeyLocationIndex,
            TimeoutSeconds = TimeoutSeconds,
            IsStatusAssertionEnabled = IsStatusAssertionEnabled,
            AssertionExpectedStatusCode = AssertionExpectedStatusCode,
            IsDurationAssertionEnabled = IsDurationAssertionEnabled,
            AssertionMaximumDurationMilliseconds = AssertionMaximumDurationMilliseconds,
            IsJsonFieldAssertionEnabled = IsJsonFieldAssertionEnabled,
            AssertionJsonPointer = AssertionJsonPointer,
            QueryParameters = QueryParameters
                .Select(field => new RequestField(field.Name, field.Value, field.IsEnabled))
                .ToArray(),
            Headers = Headers
                .Select(field => new RequestField(field.Name, field.Value, field.IsEnabled))
                .ToArray(),
            FormBodyFields = FormBodyFields
                .Select(field => new RequestField(field.Name, field.Value, field.IsEnabled))
                .ToArray(),
            ResponseBody = ResponseBody,
            ResponseStatus = ResponseStatus,
            ResponseTime = ResponseTime,
            HasResponse = HasResponse,
            ResponseStatusKind = ResponseStatusKind,
            CleanDraft = _cleanRequestDraft,
        };
        tab.RequestId = _selectedRequestId;
        tab.CollectionId = _selectedCollectionId;
    }

    private void RestoreTab(RequestTabViewModel tab)
    {
        _isSwitchingTabs = true;
        try
        {
            var state = tab.State;
            _selectedRequestId = tab.RequestId;
            _selectedCollectionId = tab.CollectionId ?? _selectedCollectionId;

            if (tab.RequestId is { } requestId &&
                _workspaceSnapshot?.Collections
                    .SelectMany(collection => collection.Requests)
                    .FirstOrDefault(request => request.Id == requestId) is { } document &&
                string.IsNullOrEmpty(state.CleanDraft))
            {
                // First activation of a saved request: load it from the workspace.
                LoadRequestDraft(document);
                ResponseStatus = Localize("StatusReady", "Ready");
                ResponseStatusKind = ResponseStatusKind.Neutral;
                ResponseTime = "—";
                ResponseBody = Localize(
                    "ResponseInspectSavedRequest",
                    "Send the saved request to inspect its response.");
                HasResponse = false;
                MarkRequestClean();
                WorkspaceStatus = Localize("StatusOpenedItem", "Opened {0}", document.Name);
            }
            else
            {
                RequestName = state.RequestName;
                SelectedMethod = state.Method;
                Url = state.Url;
                SelectedBodyType = state.BodyType;
                RequestBody = state.Body;
                SelectedAuthenticationTypeIndex = state.AuthenticationTypeIndex;
                AuthenticationBearerToken = state.AuthenticationBearerToken;
                AuthenticationBasicUsername = state.AuthenticationBasicUsername;
                AuthenticationBasicPassword = state.AuthenticationBasicPassword;
                AuthenticationApiKeyName = state.AuthenticationApiKeyName;
                AuthenticationApiKeyValue = state.AuthenticationApiKeyValue;
                AuthenticationApiKeyLocationIndex = state.AuthenticationApiKeyLocationIndex;
                TimeoutSeconds = state.TimeoutSeconds;
                IsStatusAssertionEnabled = state.IsStatusAssertionEnabled;
                AssertionExpectedStatusCode = state.AssertionExpectedStatusCode;
                IsDurationAssertionEnabled = state.IsDurationAssertionEnabled;
                AssertionMaximumDurationMilliseconds = state.AssertionMaximumDurationMilliseconds;
                IsJsonFieldAssertionEnabled = state.IsJsonFieldAssertionEnabled;
                AssertionJsonPointer = state.AssertionJsonPointer;

                QueryParameters.Clear();
                foreach (var field in state.QueryParameters)
                {
                    QueryParameters.Add(new RequestFieldViewModel(field.Name, field.Value)
                    {
                        IsEnabled = field.IsEnabled,
                    });
                }

                Headers.Clear();
                foreach (var field in state.Headers)
                {
                    Headers.Add(new RequestFieldViewModel(field.Name, field.Value)
                    {
                        IsEnabled = field.IsEnabled,
                    });
                }

                FormBodyFields.Clear();
                foreach (var field in state.FormBodyFields)
                {
                    FormBodyFields.Add(new RequestFieldViewModel(field.Name, field.Value)
                    {
                        IsEnabled = field.IsEnabled,
                    });
                }

                ResponseBody = state.ResponseBody;
                ResponseStatus = state.ResponseStatus;
                ResponseTime = state.ResponseTime;
                HasResponse = state.HasResponse;
                ResponseStatusKind = state.ResponseStatusKind;
                _cleanRequestDraft = string.IsNullOrEmpty(state.CleanDraft)
                    ? CaptureRequestDraft()
                    : state.CleanDraft;
            }
        }
        finally
        {
            _isSwitchingTabs = false;
        }

        RefreshActiveTabHeader();
    }

    /// <summary>
    /// Keeps the tab strip in step with the composer: title, method colour and
    /// the unsaved marker.
    /// </summary>
    private void RefreshActiveTabHeader()
    {
        if (ActiveTab is not { } tab)
        {
            return;
        }

        tab.Title = string.IsNullOrWhiteSpace(RequestName)
            ? Localize("StatusNewRequest", "New request")
            : RequestName;
        tab.Method = SelectedMethod;
        tab.HasUnsavedChanges = HasUnsavedRequestChanges();
        RefreshTabSubtitles();
    }

    private void RefreshTabSubtitles()
    {
        foreach (var tab in Tabs)
        {
            var duplicated = Tabs.Count(other =>
                string.Equals(other.Title, tab.Title, StringComparison.Ordinal)) > 1;
            tab.Subtitle = duplicated ? DescribeTabOrigin(tab) : string.Empty;
        }
    }

    private string DescribeTabOrigin(RequestTabViewModel tab)
    {
        var collection = _workspaceSnapshot?.Collections.FirstOrDefault(
            item => item.Id == tab.CollectionId);
        if (collection is not null)
        {
            var position = collection.Requests
                .Select((request, index) => (request, index))
                .FirstOrDefault(entry => entry.request.Id == tab.RequestId);
            return position.request is null
                ? collection.Name
                : $"{collection.Name} · {position.index + 1}";
        }

        return Localize("TabUnsavedOrigin", "Not saved yet");
    }

    /// <summary>
    /// Drops tabs whose request no longer exists and refreshes the rest, after a
    /// workspace reload. Switching to a different workspace clears them all.
    /// </summary>
    private void SyncTabsWithWorkspace(WorkspaceSnapshot snapshot, bool workspaceChanged)
    {
        if (workspaceChanged)
        {
            Tabs.Clear();
            ActiveTab = null;
            OnPropertyChanged(nameof(HasMultipleTabs));
            return;
        }

        var known = snapshot.Collections
            .SelectMany(collection => collection.Requests)
            .Select(request => request.Id)
            .ToHashSet();

        foreach (var tab in Tabs.Where(tab => tab.RequestId is { } id && !known.Contains(id)).ToArray())
        {
            Tabs.Remove(tab);
            if (ReferenceEquals(tab, ActiveTab))
            {
                ActiveTab = null;
            }
        }

        OnPropertyChanged(nameof(HasMultipleTabs));
    }

    private void EnsureActiveTab()
    {
        if (Tabs.Count == 0)
        {
            var tab = CreateTab();
            Tabs.Add(tab);
            OnPropertyChanged(nameof(HasMultipleTabs));
        }

        if (ActiveTab is null || !Tabs.Contains(ActiveTab))
        {
            var tab = Tabs[0];
            ActiveTab = tab;
            foreach (var candidate in Tabs)
            {
                candidate.IsSelected = ReferenceEquals(candidate, tab);
            }
        }

        RefreshActiveTabHeader();
    }
}

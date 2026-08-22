using System.Collections.ObjectModel;
using System.Text;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReqMint.App.Services;
using ReqMint.Core.Git;
using ReqMint.Core.History;
using ReqMint.Core.Requests;
using ReqMint.Core.Runner;
using ReqMint.Core.Security;
using ReqMint.Core.Templates;
using ReqMint.Core.Workspaces;

namespace ReqMint.App.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    public IReadOnlyList<string> Methods { get; } =
        ["GET", "POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS"];

    public IReadOnlyList<string> BodyTypes { get; } =
        ["None", "JSON", "Text", "XML", "Form URL Encoded"];

    public ObservableCollection<RequestFieldViewModel> QueryParameters { get; } =
    [
        new("include", "items"),
        new("locale", "en-US") { IsEnabled = false },
    ];

    public ObservableCollection<RequestFieldViewModel> Headers { get; } =
    [
        new("Accept", "application/json"),
        new("X-Client", "ReqMint") { IsEnabled = false },
    ];

    public ObservableCollection<CollectionItemViewModel> Collections { get; } = [];

    public ObservableCollection<RequestHistoryItemViewModel> History { get; } = [];

    public ObservableCollection<GitChangeItemViewModel> GitChanges { get; } = [];

    public ObservableCollection<GitDiffLineViewModel> GitDiffLines { get; } = [];

    public ObservableCollection<string> GitCommitFiles { get; } = [];

    public ObservableCollection<string> GitFastForwardCommits { get; } = [];

    public ObservableCollection<string> GitFastForwardPaths { get; } = [];

    public ObservableCollection<string> GitPushCommits { get; } = [];

    public ObservableCollection<string> GitPushPaths { get; } = [];

    public ObservableCollection<CollectionRunItemViewModel> CollectionRunResults { get; } = [];

    public ObservableCollection<string> EnvironmentNames { get; } = ["No environment"];

    public ObservableCollection<EnvironmentVariableViewModel> EnvironmentVariables { get; } = [];

    public LocalizationService Localization { get; }

    [ObservableProperty]
    public partial string WorkspaceName { get; set; } = "No workspace";

    [ObservableProperty]
    public partial string WorkspaceLocation { get; set; } = "Choose a local workspace to begin";

    [ObservableProperty]
    public partial string EnvironmentName { get; set; } = "No environment";

    [ObservableProperty]
    public partial string EnvironmentDraftName { get; set; } = "Development";

    [ObservableProperty]
    public partial string WorkspaceStatus { get; set; } = "Ready";

    [ObservableProperty]
    public partial string RequestName { get; set; } = "New request";

    [ObservableProperty]
    public partial string CollectionDraftName { get; set; } = "Requests";

    [ObservableProperty]
    public partial string SelectedMethod { get; set; } = "GET";

    [ObservableProperty]
    public partial string Url { get; set; } = "https://api.example.com/v1/orders/42";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBodyEnabled))]
    public partial string SelectedBodyType { get; set; } = "None";

    [ObservableProperty]
    public partial string RequestBody { get; set; } = "{\n  \"name\": \"Sample order\"\n}";

    [ObservableProperty]
    public partial decimal TimeoutSeconds { get; set; } = 30;

    [ObservableProperty]
    public partial decimal ResponsePreviewLimitMegabytes { get; set; } = 2;

    [ObservableProperty]
    public partial string ResponseBody { get; set; } = "Send a request to inspect its response.";

    [ObservableProperty]
    public partial string ResponseStatus { get; set; } = "Ready";

    [ObservableProperty]
    public partial string ResponseTime { get; set; } = "—";

    [ObservableProperty]
    public partial bool HasResponse { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCollectionsVisible))]
    public partial bool IsHistoryVisible { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCollectionsVisible))]
    public partial bool IsGitVisible { get; set; }

    public bool IsCollectionsVisible => !IsHistoryVisible && !IsGitVisible;

    [ObservableProperty]
    public partial string GitBranch { get; set; } = "—";

    [ObservableProperty]
    public partial string GitSummary { get; set; } = "Git status unavailable";

    [ObservableProperty]
    public partial string GitRepositoryRoot { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int GitOtherChangeCount { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGitSecuritySummaryVisible))]
    public partial string GitSecuritySummary { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasGitSecurityWarning { get; set; }

    [ObservableProperty]
    public partial int GitSecretWarningCount { get; set; }

    [ObservableProperty]
    public partial int GitConflictCount { get; set; }

    public bool IsGitChangeListEmpty => GitChanges.Count == 0;

    public bool IsGitSecuritySummaryVisible => !string.IsNullOrEmpty(GitSecuritySummary);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRequestWorkspaceVisible))]
    public partial bool IsGitDiffVisible { get; set; }

    [ObservableProperty]
    public partial string GitDiffPath { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string GitDiffSummary { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string GitDiffMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasGitWorkingTreeDiff { get; set; }

    [ObservableProperty]
    public partial bool HasGitStagedDiff { get; set; }

    [ObservableProperty]
    public partial bool IsGitDiffSecurityBlocked { get; set; }

    [ObservableProperty]
    public partial bool IsGitConflictGuidanceVisible { get; set; }

    [ObservableProperty]
    public partial bool IsGitStageAvailable { get; set; }

    [ObservableProperty]
    public partial bool IsGitStageReviewVisible { get; set; }

    [ObservableProperty]
    public partial bool IsGitStageBusy { get; set; }

    [ObservableProperty]
    public partial bool IsGitCommitReviewAvailable { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRequestWorkspaceVisible))]
    public partial bool IsGitCommitVisible { get; set; }

    [ObservableProperty]
    public partial bool IsGitCommitBusy { get; set; }

    [ObservableProperty]
    public partial string GitCommitMessage { get; set; } = "chore: update ReqMint workspace";

    [ObservableProperty]
    public partial string GitCommitSummary { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGitCommitValidationVisible))]
    public partial string GitCommitValidationMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsGitRemoteReviewAvailable { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRequestWorkspaceVisible))]
    public partial bool IsGitRemoteVisible { get; set; }

    [ObservableProperty]
    public partial bool IsGitRemoteBusy { get; set; }

    [ObservableProperty]
    public partial string GitRemoteName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string GitRemoteBranch { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string GitRemoteSummary { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int GitAheadCount { get; set; }

    [ObservableProperty]
    public partial int GitBehindCount { get; set; }

    [ObservableProperty]
    public partial bool IsGitFastForwardReviewAvailable { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRequestWorkspaceVisible))]
    public partial bool IsGitFastForwardVisible { get; set; }

    [ObservableProperty]
    public partial bool IsGitFastForwardBusy { get; set; }

    [ObservableProperty]
    public partial string GitFastForwardSummary { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsGitPushReviewAvailable { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRequestWorkspaceVisible))]
    public partial bool IsGitPushVisible { get; set; }

    [ObservableProperty]
    public partial bool IsGitPushBusy { get; set; }

    [ObservableProperty]
    public partial string GitPushSummary { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsCollectionRunAvailable { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRequestWorkspaceVisible))]
    public partial bool IsCollectionRunnerVisible { get; set; }

    [ObservableProperty]
    public partial bool IsCollectionRunnerBusy { get; set; }

    [ObservableProperty]
    public partial bool CollectionRunStopOnFailure { get; set; }

    [ObservableProperty]
    public partial string CollectionRunTitle { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string CollectionRunSummary { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string CollectionRunProgress { get; set; } = string.Empty;

    public bool IsRequestWorkspaceVisible =>
        !IsGitDiffVisible
        && !IsGitCommitVisible
        && !IsGitRemoteVisible
        && !IsGitFastForwardVisible
        && !IsGitPushVisible
        && !IsCollectionRunnerVisible;

    public bool IsGitCommitValidationVisible =>
        !string.IsNullOrEmpty(GitCommitValidationMessage);

    public bool IsGitDiffLineListEmpty => GitDiffLines.Count == 0;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    [NotifyCanExecuteChangedFor(nameof(CreateWorkspaceCommand))]
    [NotifyCanExecuteChangedFor(nameof(OpenWorkspaceCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveRequestCommand))]
    [NotifyCanExecuteChangedFor(nameof(NewRequestCommand))]
    [NotifyCanExecuteChangedFor(nameof(NewEnvironmentCommand))]
    [NotifyCanExecuteChangedFor(nameof(AddEnvironmentVariableCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveEnvironmentCommand))]
    [NotifyCanExecuteChangedFor(nameof(CreateCollectionCommand))]
    [NotifyCanExecuteChangedFor(nameof(RenameCollectionCommand))]
    public partial bool IsSending { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    [NotifyCanExecuteChangedFor(nameof(CreateWorkspaceCommand))]
    [NotifyCanExecuteChangedFor(nameof(OpenWorkspaceCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveRequestCommand))]
    [NotifyCanExecuteChangedFor(nameof(NewRequestCommand))]
    [NotifyCanExecuteChangedFor(nameof(NewEnvironmentCommand))]
    [NotifyCanExecuteChangedFor(nameof(AddEnvironmentVariableCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveEnvironmentCommand))]
    [NotifyCanExecuteChangedFor(nameof(CreateCollectionCommand))]
    [NotifyCanExecuteChangedFor(nameof(RenameCollectionCommand))]
    public partial bool IsWorkspaceBusy { get; set; }

    private readonly IRequestExecutor _requestExecutor;
    private readonly ICollectionRunner _collectionRunner;
    private readonly IWorkspaceStore _workspaceStore;
    private readonly IWorkspaceFolderPicker _folderPicker;
    private readonly RequestTemplateResolver _templateResolver;
    private readonly ISecretVault _secretVault;
    private readonly IUnsavedChangesPrompt _unsavedChangesPrompt;
    private readonly IRequestHistoryStore _historyStore;
    private readonly IHistoryClearPrompt _historyClearPrompt;
    private readonly IAppSettingsService _appSettings;
    private readonly IGitService _gitService;
    private readonly IGitSecretScanner _gitSecretScanner;
    private WorkspaceSnapshot? _workspaceSnapshot;
    private string? _workspaceDirectory;
    private Guid? _selectedRequestId;
    private Guid? _selectedCollectionId;
    private EnvironmentDocument? _activeEnvironment;
    private Guid? _editingEnvironmentId;
    private string _cleanRequestDraft;

    public bool IsBodyEnabled => SelectedBodyType != "None";

    public MainViewModel(
        IRequestExecutor requestExecutor,
        ICollectionRunner collectionRunner,
        IWorkspaceStore workspaceStore,
        IWorkspaceFolderPicker folderPicker,
        RequestTemplateResolver templateResolver,
        ISecretVault secretVault,
        LocalizationService localization,
        IUnsavedChangesPrompt unsavedChangesPrompt,
        IRequestHistoryStore historyStore,
        IHistoryClearPrompt historyClearPrompt,
        IAppSettingsService appSettings,
        IGitService gitService,
        IGitSecretScanner gitSecretScanner)
    {
        _requestExecutor = requestExecutor;
        _collectionRunner = collectionRunner;
        _workspaceStore = workspaceStore;
        _folderPicker = folderPicker;
        _templateResolver = templateResolver;
        _secretVault = secretVault;
        Localization = localization;
        GitSummary = Localize("GitNoWorkspace", "Open a workspace to inspect Git status");
        _unsavedChangesPrompt = unsavedChangesPrompt;
        _historyStore = historyStore;
        _historyClearPrompt = historyClearPrompt;
        _appSettings = appSettings;
        _gitService = gitService;
        _gitSecretScanner = gitSecretScanner;
        HistoryRetentionLimit = appSettings.Current.HistoryRetentionLimit;
        ResponsePreviewLimitMegabytes = appSettings.Current.ResponsePreviewLimitMegabytes;
        _cleanRequestDraft = CaptureRequestDraft();
    }

    partial void OnResponsePreviewLimitMegabytesChanged(decimal value)
    {
        var limit = (int)Math.Clamp(
            value,
            JsonAppSettingsService.MinimumResponsePreviewLimitMegabytes,
            JsonAppSettingsService.MaximumResponsePreviewLimitMegabytes);
        if (value != limit)
        {
            ResponsePreviewLimitMegabytes = limit;
            return;
        }

        if (_appSettings is not null && _appSettings.Current.ResponsePreviewLimitMegabytes != limit)
        {
            _appSettings.Update(_appSettings.Current with { ResponsePreviewLimitMegabytes = limit });
        }
    }

    partial void OnEnvironmentNameChanged(string value)
    {
        _activeEnvironment = _workspaceSnapshot?.Environments.FirstOrDefault(
            environment => string.Equals(environment.Name, value, StringComparison.Ordinal));
        LoadEnvironmentEditor(_activeEnvironment);
    }

    private bool CanSend() => !IsSending && !IsWorkspaceBusy;

    private bool CanManageWorkspace() => !IsWorkspaceBusy && !IsSending;

    private bool CanSaveRequest() =>
        !IsWorkspaceBusy && !IsSending && _workspaceSnapshot is not null;

    private bool CanEditEnvironment() =>
        !IsWorkspaceBusy && !IsSending && _workspaceSnapshot is not null;

    private bool CanManageCollection() =>
        !IsWorkspaceBusy && !IsSending && _workspaceSnapshot is not null;

    private string Localize(string key, string fallback) =>
        Localization?.GetString(key) ?? fallback;

    private string Localize(string key, string fallback, object value) =>
        string.Format(Localize(key, fallback), value);

    private string Localize(string key, string fallback, object first, object second) =>
        string.Format(Localize(key, fallback), first, second);

    [RelayCommand]
    private void AddQueryParameter() => QueryParameters.Add(new RequestFieldViewModel());

    [RelayCommand]
    private void AddHeader() => Headers.Add(new RequestFieldViewModel());

    [RelayCommand(CanExecute = nameof(CanSaveRequest))]
    private async Task NewRequestAsync(CancellationToken cancellationToken)
    {
        if (!await ConfirmNavigationAsync(cancellationToken))
        {
            return;
        }

        ResetRequestDraft();
    }

    private void ResetRequestDraft()
    {
        _selectedRequestId = null;
        RequestName = "New request";
        SelectedMethod = "GET";
        Url = string.Empty;
        TimeoutSeconds = 30;

        QueryParameters.Clear();
        QueryParameters.Add(new RequestFieldViewModel());
        Headers.Clear();
        Headers.Add(new RequestFieldViewModel("Accept", "application/json"));

        SelectedBodyType = "None";
        RequestBody = string.Empty;
        ResponseStatus = "Ready";
        ResponseTime = "—";
        ResponseBody = "Compose and send a new request.";
        WorkspaceStatus = Localize("StatusNewRequest", "New request");
        MarkRequestClean();
    }

    [RelayCommand(CanExecute = nameof(CanManageWorkspace))]
    private async Task CreateWorkspaceAsync(CancellationToken cancellationToken)
    {
        if (!await ConfirmNavigationAsync(cancellationToken))
        {
            return;
        }

        IsWorkspaceBusy = true;
        WorkspaceStatus = "Choose a workspace folder";

        try
        {
            var directory = await _folderPicker.PickFolderAsync(
                "Choose a folder for the ReqMint workspace");
            if (directory is null)
            {
                WorkspaceStatus = "Ready";
                return;
            }

            WorkspaceStatus = "Creating workspace...";
            var existingWorkspace = Path.Combine(directory, "reqmint.workspace.json");
            if (File.Exists(existingWorkspace))
            {
                var existingSnapshot = await _workspaceStore.LoadAsync(directory, cancellationToken);
                ApplyWorkspace(existingSnapshot, directory);
                await LoadHistoryAsync(existingSnapshot.Workspace.Id, cancellationToken);
                await RefreshGitStatusAsync(directory, cancellationToken);
                ResetRequestDraft();
                WorkspaceStatus = "Existing workspace opened";
                return;
            }

            var workspaceName = GetWorkspaceName(directory);
            var collectionId = Guid.NewGuid();
            var collection = new CollectionDocument
            {
                Id = collectionId,
                Name = "Requests",
            };
            var workspace = new WorkspaceDocument
            {
                Id = Guid.NewGuid(),
                Name = workspaceName,
                Collections =
                [
                    new WorkspaceFileReference(
                        collectionId,
                        collection.Name,
                        "collections/requests.json"),
                ],
            };
            var snapshot = new WorkspaceSnapshot(workspace, [collection], []);

            await _workspaceStore.SaveAsync(directory, snapshot, cancellationToken);
            ApplyWorkspace(snapshot, directory);
            await LoadHistoryAsync(snapshot.Workspace.Id, cancellationToken);
            await RefreshGitStatusAsync(directory, cancellationToken);
            ResetRequestDraft();
            WorkspaceStatus = Localize("StatusWorkspaceCreated", "Workspace created");
        }
        catch (OperationCanceledException)
        {
            WorkspaceStatus = "Workspace creation cancelled";
        }
        catch (Exception exception)
        {
            ShowWorkspaceError("Could not create workspace", exception);
        }
        finally
        {
            IsWorkspaceBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanManageWorkspace))]
    private async Task OpenWorkspaceAsync(CancellationToken cancellationToken)
    {
        if (!await ConfirmNavigationAsync(cancellationToken))
        {
            return;
        }

        IsWorkspaceBusy = true;
        WorkspaceStatus = "Choose a workspace folder";

        try
        {
            var directory = await _folderPicker.PickFolderAsync("Open a ReqMint workspace");
            if (directory is null)
            {
                WorkspaceStatus = "Ready";
                return;
            }

            WorkspaceStatus = "Opening workspace...";
            var snapshot = await _workspaceStore.LoadAsync(directory, cancellationToken);
            ApplyWorkspace(snapshot, directory);
            await LoadHistoryAsync(snapshot.Workspace.Id, cancellationToken);
            await RefreshGitStatusAsync(directory, cancellationToken);
            ResetRequestDraft();
            WorkspaceStatus = Localize("StatusWorkspaceOpened", "Workspace opened");
        }
        catch (OperationCanceledException)
        {
            WorkspaceStatus = "Opening workspace cancelled";
        }
        catch (Exception exception)
        {
            ShowWorkspaceError("Could not open workspace", exception);
        }
        finally
        {
            IsWorkspaceBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanSaveRequest))]
    private async Task SaveRequestAsync(CancellationToken cancellationToken)
    {
        if (_workspaceSnapshot is null || _workspaceDirectory is null)
        {
            return;
        }

        IsWorkspaceBusy = true;
        WorkspaceStatus = Localize("StatusSavingRequest", "Saving request...");

        try
        {
            var document = CreateCurrentRequestDocument(_selectedRequestId ?? Guid.NewGuid());

            var workspace = _workspaceSnapshot.Workspace;
            var collections = _workspaceSnapshot.Collections.ToList();
            if (collections.Count == 0)
            {
                var collectionId = Guid.NewGuid();
                var defaultCollection = new CollectionDocument
                {
                    Id = collectionId,
                    Name = "Requests",
                };
                collections.Add(defaultCollection);
                workspace = workspace with
                {
                    Collections =
                    [
                        new WorkspaceFileReference(
                            collectionId,
                            defaultCollection.Name,
                            "collections/requests.json"),
                    ],
                };
            }

            var collectionIndex = FindTargetCollectionIndex(collections);
            var collection = collections[collectionIndex];
            var requests = collection.Requests.ToList();
            var requestIndex = requests.FindIndex(item => item.Id == document.Id);

            if (requestIndex >= 0)
            {
                requests[requestIndex] = document;
            }
            else
            {
                requests.Add(document);
            }

            collections[collectionIndex] = collection with { Requests = requests };
            var updatedSnapshot = _workspaceSnapshot with
            {
                Workspace = workspace,
                Collections = collections,
            };

            await _workspaceStore.SaveAsync(_workspaceDirectory, updatedSnapshot, cancellationToken);
            ApplyWorkspace(updatedSnapshot, _workspaceDirectory, document.Id, collection.Id);
            MarkRequestClean();
            WorkspaceStatus = Localize("StatusSavedItem", "Saved {0}", document.Name);
        }
        catch (OperationCanceledException)
        {
            WorkspaceStatus = Localize("StatusSaveCancelled", "Save cancelled");
        }
        catch (Exception exception)
        {
            ShowWorkspaceError("Could not save request", exception);
        }
        finally
        {
            IsWorkspaceBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanSend), IncludeCancelCommand = true)]
    private async Task SendAsync(CancellationToken cancellationToken)
    {
        RequestDocument requestDocument;
        ApiRequest request;

        try
        {
            requestDocument = CreateCurrentRequestDocument(
                _selectedRequestId ?? Guid.NewGuid());
            request = await _templateResolver.ResolveAsync(
                _workspaceSnapshot?.Workspace.Id ?? Guid.Empty,
                _activeEnvironment,
                requestDocument,
                cancellationToken);
            request = request with
            {
                ResponsePreviewLimitBytes = checked(
                    (int)ResponsePreviewLimitMegabytes * 1024 * 1024),
            };
        }
        catch (ArgumentException exception)
        {
            ResponseStatus = Localize("StatusInvalidRequest", "Invalid request");
            ResponseBody = exception.Message;
            HasResponse = true;
            return;
        }
        catch (RequestTemplateResolutionException exception)
        {
            ResponseStatus = Localize("StatusMissingVariables", "Missing variables");
            ResponseBody = exception.Message;
            HasResponse = true;
            return;
        }
        catch (SecretVaultUnavailableException exception)
        {
            ResponseStatus = Localize("StatusSecretVaultUnavailable", "Secret vault unavailable");
            ResponseBody = exception.Message;
            HasResponse = true;
            return;
        }

        IsSending = true;
        ResponseStatus = Localize("StatusSending", "Sending...");
        ResponseTime = "—";

        try
        {
            var response = await _requestExecutor.ExecuteAsync(request, cancellationToken);

            ResponseStatus = $"{response.StatusCode} {response.ReasonPhrase}".TrimEnd();
            ResponseTime = $"{response.Duration.TotalMilliseconds:N0} ms";
            ResponseBody = FormatBody(response.Body, response.ContentType);

            if (response.IsBodyTruncated)
            {
                ResponseBody += Localize(
                    "ResponsePreviewLimited",
                    "\n\n— Preview limited to {0} MB —",
                    ResponsePreviewLimitMegabytes);
            }

            HasResponse = true;
            await RecordHistoryAsync(requestDocument, response, "completed");
        }
        catch (OperationCanceledException)
        {
            ResponseStatus = Localize("StatusCancelled", "Cancelled");
            ResponseBody = "The request was cancelled.";
            HasResponse = true;
            await RecordHistoryAsync(requestDocument, response: null, "cancelled");
        }
        catch (TimeoutException exception)
        {
            ResponseStatus = Localize("StatusTimedOut", "Timed out");
            ResponseBody = exception.Message;
            HasResponse = true;
            await RecordHistoryAsync(requestDocument, response: null, "timed-out");
        }
        catch (HttpRequestException exception)
        {
            ResponseStatus = Localize("StatusConnectionFailed", "Connection failed");
            ResponseBody = exception.Message;
            HasResponse = true;
            await RecordHistoryAsync(requestDocument, response: null, "failed");
        }
        finally
        {
            IsSending = false;
        }
    }

    private RequestDocument CreateCurrentRequestDocument(Guid requestId)
    {
        if (TimeoutSeconds is < 1 or > 600)
        {
            throw new ArgumentException("Request timeout must be between 1 and 600 seconds.");
        }

        if (string.IsNullOrWhiteSpace(SelectedMethod))
        {
            throw new ArgumentException("An HTTP method is required.");
        }

        if (string.IsNullOrWhiteSpace(Url))
        {
            throw new ArgumentException("A request URL is required.");
        }

        var isHttpUrl = Uri.TryCreate(Url, UriKind.Absolute, out var parsedUrl) &&
            (parsedUrl.Scheme == Uri.UriSchemeHttp || parsedUrl.Scheme == Uri.UriSchemeHttps);
        if (!isHttpUrl && !RequestTemplate.ContainsVariables(Url))
        {
            throw new ArgumentException("A valid HTTP URL or URL template is required.");
        }

        var name = string.IsNullOrWhiteSpace(RequestName)
            ? $"{SelectedMethod.Trim().ToUpperInvariant()} request"
            : RequestName.Trim();

        return new RequestDocument
        {
            Id = requestId,
            Name = name,
            Method = SelectedMethod.Trim().ToUpperInvariant(),
            Url = Url.Trim(),
            QueryParameters = QueryParameters
                .Where(field => field.IsEnabled && !string.IsNullOrWhiteSpace(field.Name))
                .Select(field => new RequestField(field.Name.Trim(), field.Value))
                .ToArray(),
            Headers = Headers
                .Where(field => field.IsEnabled && !string.IsNullOrWhiteSpace(field.Name))
                .Select(field => new RequestField(field.Name.Trim(), field.Value))
                .ToArray(),
            Body = CreateBody(),
            TimeoutSeconds = (int)TimeoutSeconds,
        };
    }

    private int FindTargetCollectionIndex(IReadOnlyList<CollectionDocument> collections)
    {
        var index = _selectedCollectionId is null
            ? -1
            : collections.ToList().FindIndex(collection => collection.Id == _selectedCollectionId);

        return index >= 0 ? index : 0;
    }

    private void ApplyWorkspace(
        WorkspaceSnapshot snapshot,
        string directory,
        Guid? selectedRequestId = null,
        Guid? selectedCollectionId = null,
        Guid? selectedEnvironmentId = null)
    {
        CloseCollectionRunner();
        _workspaceSnapshot = snapshot;
        _workspaceDirectory = directory;
        _selectedRequestId = selectedRequestId;
        _selectedCollectionId = selectedCollectionId ?? snapshot.Collections.FirstOrDefault()?.Id;
        CollectionDraftName = snapshot.Collections.FirstOrDefault(
            collection => collection.Id == _selectedCollectionId)?.Name ?? "Requests";

        WorkspaceName = snapshot.Workspace.Name;
        WorkspaceLocation = directory;
        EnvironmentNames.Clear();
        if (snapshot.Environments.Count == 0)
        {
            EnvironmentNames.Add("No environment");
        }
        else
        {
            foreach (var environment in snapshot.Environments)
            {
                EnvironmentNames.Add(environment.Name);
            }
        }

        _activeEnvironment = selectedEnvironmentId is null
            ? snapshot.Environments.FirstOrDefault()
            : snapshot.Environments.FirstOrDefault(
                environment => environment.Id == selectedEnvironmentId);
        EnvironmentName = _activeEnvironment?.Name ?? EnvironmentNames[0];
        LoadEnvironmentEditor(_activeEnvironment);

        Collections.Clear();
        foreach (var collection in snapshot.Collections)
        {
            Collections.Add(new CollectionItemViewModel(
                collection.Id,
                collection.Name,
                collection.Requests.Select(request =>
                    new SavedRequestItemViewModel(
                        request,
                        selected => OpenRequest(selected, collection.Id))),
                SelectCollection));
        }

        SaveRequestCommand.NotifyCanExecuteChanged();
        NewRequestCommand.NotifyCanExecuteChanged();
        NewEnvironmentCommand.NotifyCanExecuteChanged();
        AddEnvironmentVariableCommand.NotifyCanExecuteChanged();
        SaveEnvironmentCommand.NotifyCanExecuteChanged();
        CreateCollectionCommand.NotifyCanExecuteChanged();
        RenameCollectionCommand.NotifyCanExecuteChanged();
        UpdateCollectionRunAvailability();
    }

    private async Task SelectCollection(Guid collectionId)
    {
        var collection = _workspaceSnapshot?.Collections.FirstOrDefault(item => item.Id == collectionId);
        if (collection is null)
        {
            return;
        }

        if (!await ConfirmNavigationAsync(CancellationToken.None))
        {
            return;
        }

        _selectedCollectionId = collection.Id;
        CollectionDraftName = collection.Name;
        UpdateCollectionRunAvailability();
        WorkspaceStatus = collection.Name;
        ResetRequestDraft();
    }

    private async Task OpenRequest(RequestDocument request, Guid collectionId)
    {
        if (!await ConfirmNavigationAsync(CancellationToken.None))
        {
            return;
        }

        _selectedRequestId = request.Id;
        _selectedCollectionId = collectionId;

        LoadRequestDraft(request);
        ResponseStatus = "Ready";
        ResponseTime = "—";
        ResponseBody = "Send the saved request to inspect its response.";
        WorkspaceStatus = Localize("StatusOpenedItem", "Opened {0}", request.Name);
        MarkRequestClean();
    }

    private void LoadRequestDraft(RequestDocument request)
    {
        RequestName = request.Name;
        SelectedMethod = request.Method;
        Url = request.Url;
        TimeoutSeconds = request.TimeoutSeconds;

        QueryParameters.Clear();
        foreach (var field in request.QueryParameters)
        {
            QueryParameters.Add(new RequestFieldViewModel(field.Name, field.Value));
        }

        Headers.Clear();
        foreach (var field in request.Headers)
        {
            Headers.Add(new RequestFieldViewModel(field.Name, field.Value));
        }

        SelectedBodyType = GetBodyType(request.Body?.ContentType);
        RequestBody = request.Body?.Content ?? string.Empty;
    }

    private async Task RecordHistoryAsync(
        RequestDocument request,
        ApiResponse? response,
        string outcome)
    {
        var workspaceId = _workspaceSnapshot?.Workspace.Id ?? Guid.Empty;
        var entry = new RequestHistoryEntry
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            SentAtUtc = DateTimeOffset.UtcNow,
            Request = RequestHistoryPrivacy.CreateSafeSnapshot(request),
            Outcome = outcome,
            StatusCode = response?.StatusCode,
            ReasonPhrase = response?.ReasonPhrase,
            DurationMilliseconds = response?.Duration.TotalMilliseconds,
            ContentType = response?.ContentType,
            ResponseBytes = response is null ? null : Encoding.UTF8.GetByteCount(response.Body),
        };

        try
        {
            await _historyStore.AddAsync(
                entry,
                (int)HistoryRetentionLimit,
                CancellationToken.None);
            _historyEntries.Insert(0, entry);
            while (_historyEntries.Count > HistoryRetentionLimit)
            {
                _historyEntries.RemoveAt(_historyEntries.Count - 1);
            }

            ApplyHistoryFilter();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            WorkspaceStatus = Localize("StatusHistoryUnavailable", "Request completed; history could not be saved");
            System.Diagnostics.Debug.WriteLine(exception);
        }
    }

    private async Task<bool> ConfirmNavigationAsync(CancellationToken cancellationToken)
    {
        if (string.Equals(_cleanRequestDraft, CaptureRequestDraft(), StringComparison.Ordinal))
        {
            return true;
        }

        var choice = await _unsavedChangesPrompt.ShowAsync(
            RequestName,
            _workspaceSnapshot is not null);
        if (choice == UnsavedChangesChoice.Save)
        {
            await SaveRequestAsync(cancellationToken);
            return string.Equals(_cleanRequestDraft, CaptureRequestDraft(), StringComparison.Ordinal);
        }

        return choice == UnsavedChangesChoice.Discard;
    }

    private void MarkRequestClean() => _cleanRequestDraft = CaptureRequestDraft();

    private bool HasUnsavedRequestChanges() => !string.Equals(
        _cleanRequestDraft,
        CaptureRequestDraft(),
        StringComparison.Ordinal);

    private bool HasUnsavedWorkspaceChanges()
    {
        if (HasUnsavedRequestChanges())
        {
            return true;
        }

        var selectedCollection = _workspaceSnapshot?.Collections.FirstOrDefault(
            collection => collection.Id == _selectedCollectionId);
        if (selectedCollection is not null
            && !string.Equals(
                CollectionDraftName,
                selectedCollection.Name,
                StringComparison.Ordinal))
        {
            return true;
        }

        var environment = _workspaceSnapshot?.Environments.FirstOrDefault(
            item => item.Id == _editingEnvironmentId);
        if (environment is null)
        {
            return _editingEnvironmentId is null
                && (EnvironmentVariables.Count > 0
                    || !string.Equals(
                        EnvironmentDraftName,
                        "Development",
                        StringComparison.Ordinal));
        }

        if (!string.Equals(EnvironmentDraftName, environment.Name, StringComparison.Ordinal)
            || EnvironmentVariables.Count != environment.Variables.Count)
        {
            return true;
        }

        for (var index = 0; index < environment.Variables.Count; index++)
        {
            var editor = EnvironmentVariables[index];
            var stored = environment.Variables[index];
            if (!string.Equals(editor.Name, stored.Name, StringComparison.Ordinal)
                || editor.IsSecret != stored.IsSecret
                || (stored.IsSecret
                    ? !string.IsNullOrEmpty(editor.Value)
                    : !string.Equals(editor.Value, stored.Value ?? string.Empty, StringComparison.Ordinal)))
            {
                return true;
            }
        }

        return false;
    }

    private string CaptureRequestDraft() => JsonSerializer.Serialize(new
    {
        RequestName,
        SelectedMethod,
        Url,
        SelectedBodyType,
        RequestBody,
        TimeoutSeconds,
        Query = QueryParameters.Select(field => new { field.IsEnabled, field.Name, field.Value }),
        Headers = Headers.Select(field => new { field.IsEnabled, field.Name, field.Value }),
    });

    private void ShowWorkspaceError(string title, Exception exception)
    {
        WorkspaceStatus = title;
        ResponseStatus = title;
        ResponseBody = exception.Message;
        HasResponse = true;
    }

    private static string GetWorkspaceName(string directory)
    {
        var trimmedDirectory = Path.TrimEndingDirectorySeparator(directory);
        var name = Path.GetFileName(trimmedDirectory);
        return string.IsNullOrWhiteSpace(name) ? "ReqMint Workspace" : name;
    }

    private static string GetBodyType(string? contentType)
    {
        if (contentType?.Contains("json", StringComparison.OrdinalIgnoreCase) == true)
        {
            return "JSON";
        }

        if (contentType?.Contains("xml", StringComparison.OrdinalIgnoreCase) == true)
        {
            return "XML";
        }

        if (contentType?.Contains("x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase) == true)
        {
            return "Form URL Encoded";
        }

        return contentType is null ? "None" : "Text";
    }

    private ApiRequestBody? CreateBody() => SelectedBodyType switch
    {
        "JSON" => new ApiRequestBody(RequestBody, "application/json"),
        "Text" => new ApiRequestBody(RequestBody, "text/plain"),
        "XML" => new ApiRequestBody(RequestBody, "application/xml"),
        "Form URL Encoded" => new ApiRequestBody(RequestBody, "application/x-www-form-urlencoded"),
        _ => null,
    };

    private static string FormatBody(string body, string? contentType)
    {
        if (string.IsNullOrWhiteSpace(body) ||
            contentType?.Contains("json", StringComparison.OrdinalIgnoreCase) != true)
        {
            return body;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            return JsonSerializer.Serialize(document.RootElement, new JsonSerializerOptions
            {
                WriteIndented = true,
            });
        }
        catch (JsonException)
        {
            return body;
        }
    }
}

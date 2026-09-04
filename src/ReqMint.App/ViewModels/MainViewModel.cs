using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReqMint.App.Services;
using ReqMint.Core.Git;
using ReqMint.Core.History;
using ReqMint.Core.Importing;
using ReqMint.Core.Requests;
using ReqMint.Core.Runner;
using ReqMint.Core.Security;
using ReqMint.Core.Templates;
using ReqMint.Core.Workspaces;

namespace ReqMint.App.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private const string WorkspaceFileName = "reqmint.workspace.json";

    private static readonly Uri DocumentationUri = new("https://reqmintapp.github.io/docs");
    private static readonly Uri PrivacyUri = new("https://reqmintapp.github.io/privacy");
    private static readonly Uri SecurityUri = new("https://reqmintapp.github.io/security");
    private static readonly Uri SupportUri = new("https://reqmintapp.github.io/support");

    public IReadOnlyList<string> Methods { get; } =
        ["GET", "POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS"];

    public IReadOnlyList<string> BodyTypes { get; } =
        ["None", "JSON", "Text", "XML", "Form URL Encoded", "Multipart Form Data"];

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

    public ObservableCollection<RequestFieldViewModel> FormBodyFields { get; } = [];

    public ObservableCollection<RequestFileFieldViewModel> MultipartFileFields { get; } = [];

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

    public ObservableCollection<CollectionRunHistoryItemViewModel> CollectionRunHistory { get; } = [];

    public ObservableCollection<string> EnvironmentNames { get; } = ["No environment"];

    public ObservableCollection<EnvironmentVariableViewModel> EnvironmentVariables { get; } = [];

    public LocalizationService? Localization { get; }

    public ThemeService Themes { get; }

    public ApplicationInfoSnapshot ApplicationInfo { get; }

    public string SupportInformation { get; }

    public bool KeepRunningInBackground
    {
        get => _appSettings.Current.WindowCloseBehavior == WindowCloseBehavior.KeepRunning;
        set
        {
            var behavior = value
                ? WindowCloseBehavior.KeepRunning
                : WindowCloseBehavior.Exit;
            if (_appSettings.Current.WindowCloseBehavior == behavior)
            {
                return;
            }

            _appSettings.Update(_appSettings.Current with { WindowCloseBehavior = behavior });
            RefreshWindowClosePreference();
        }
    }

    public bool IsWindowClosePreferenceUndecided =>
        _appSettings.Current.WindowCloseBehavior == WindowCloseBehavior.Ask;

    public bool UseWorkspaceCookies
    {
        get => _appSettings.Current.UseWorkspaceCookies;
        set
        {
            if (_appSettings.Current.UseWorkspaceCookies == value)
            {
                return;
            }

            _appSettings.Update(_appSettings.Current with { UseWorkspaceCookies = value });
            _requestCookieManager.SetEnabled(value);
            OnPropertyChanged();
            ClearWorkspaceCookiesCommand.NotifyCanExecuteChanged();
        }
    }

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
    [NotifyPropertyChangedFor(nameof(IsGetMethod))]
    [NotifyPropertyChangedFor(nameof(IsPostMethod))]
    [NotifyPropertyChangedFor(nameof(IsPutMethod))]
    [NotifyPropertyChangedFor(nameof(IsPatchMethod))]
    [NotifyPropertyChangedFor(nameof(IsDeleteMethod))]
    public partial string SelectedMethod { get; set; } = "GET";

    public bool IsGetMethod => HttpMethodStyle.IsGet(SelectedMethod);

    public bool IsPostMethod => HttpMethodStyle.IsPost(SelectedMethod);

    public bool IsPutMethod => HttpMethodStyle.IsPut(SelectedMethod);

    public bool IsPatchMethod => HttpMethodStyle.IsPatch(SelectedMethod);

    public bool IsDeleteMethod => HttpMethodStyle.IsDelete(SelectedMethod);

    [ObservableProperty]
    public partial string Url { get; set; } = "https://api.example.com/v1/orders/42";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBodyEnabled))]
    [NotifyPropertyChangedFor(nameof(IsRawBodyVisible))]
    [NotifyPropertyChangedFor(nameof(IsFormUrlEncodedBody))]
    [NotifyPropertyChangedFor(nameof(IsMultipartFormDataBody))]
    [NotifyPropertyChangedFor(nameof(IsStructuredFormBody))]
    public partial string SelectedBodyType { get; set; } = "None";

    [ObservableProperty]
    public partial string RequestBody { get; set; } = "{\n  \"name\": \"Sample order\"\n}";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBearerAuthentication))]
    [NotifyPropertyChangedFor(nameof(IsBasicAuthentication))]
    [NotifyPropertyChangedFor(nameof(IsApiKeyAuthentication))]
    public partial int SelectedAuthenticationTypeIndex { get; set; }

    public bool IsBearerAuthentication => SelectedAuthenticationTypeIndex == 1;

    public bool IsBasicAuthentication => SelectedAuthenticationTypeIndex == 2;

    public bool IsApiKeyAuthentication => SelectedAuthenticationTypeIndex == 3;

    [ObservableProperty]
    public partial string AuthenticationBearerToken { get; set; } = "{{TOKEN}}";

    [ObservableProperty]
    public partial string AuthenticationBasicUsername { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string AuthenticationBasicPassword { get; set; } = "{{PASSWORD}}";

    [ObservableProperty]
    public partial string AuthenticationApiKeyName { get; set; } = "X-API-Key";

    [ObservableProperty]
    public partial string AuthenticationApiKeyValue { get; set; } = "{{API_KEY}}";

    [ObservableProperty]
    public partial int AuthenticationApiKeyLocationIndex { get; set; }

    [ObservableProperty]
    public partial decimal TimeoutSeconds { get; set; } = 30;

    [ObservableProperty]
    public partial bool IsStatusAssertionEnabled { get; set; }

    [ObservableProperty]
    public partial decimal AssertionExpectedStatusCode { get; set; } = 200;

    [ObservableProperty]
    public partial bool IsDurationAssertionEnabled { get; set; }

    [ObservableProperty]
    public partial decimal AssertionMaximumDurationMilliseconds { get; set; } = 1000;

    [ObservableProperty]
    public partial bool IsJsonFieldAssertionEnabled { get; set; }

    [ObservableProperty]
    public partial string AssertionJsonPointer { get; set; } = "/id";

    [ObservableProperty]
    public partial decimal ResponsePreviewLimitMegabytes { get; set; } = 2;

    [ObservableProperty]
    public partial string ResponseBody { get; set; } = "Send a request to inspect its response.";

    [ObservableProperty]
    public partial string ResponseStatus { get; set; } = "Ready";

    /// <summary>
    /// Drives the colour of the response status. The text itself always carries
    /// the same information, so colour only reinforces it.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsResponseSuccess))]
    [NotifyPropertyChangedFor(nameof(IsResponseRedirect))]
    [NotifyPropertyChangedFor(nameof(IsResponseClientError))]
    [NotifyPropertyChangedFor(nameof(IsResponseFailure))]
    public partial ResponseStatusKind ResponseStatusKind { get; set; }

    public bool IsResponseSuccess => ResponseStatusKind == ResponseStatusKind.Success;

    public bool IsResponseRedirect => ResponseStatusKind == ResponseStatusKind.Redirect;

    public bool IsResponseClientError => ResponseStatusKind == ResponseStatusKind.ClientError;

    public bool IsResponseFailure => ResponseStatusKind == ResponseStatusKind.Failure;

    [ObservableProperty]
    public partial string RequestFilterText { get; set; } = string.Empty;

    public bool IsCollectionListEmpty => Collections.Count == 0;

    public bool IsRequestFilterActive => !string.IsNullOrWhiteSpace(RequestFilterText);

    [ObservableProperty]
    public partial string ResponseTime { get; set; } = "—";

    [ObservableProperty]
    public partial bool HasResponse { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRequestsNavigationSelected))]
    [NotifyPropertyChangedFor(nameof(IsEnvironmentNavigationSelected))]
    public partial int RequestEditorTabIndex { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRequestWorkspaceVisible))]
    [NotifyPropertyChangedFor(nameof(IsRequestsNavigationSelected))]
    [NotifyPropertyChangedFor(nameof(IsEnvironmentNavigationSelected))]
    public partial bool IsApplicationSettingsVisible { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCollectionsVisible))]
    [NotifyPropertyChangedFor(nameof(IsRequestsNavigationSelected))]
    [NotifyPropertyChangedFor(nameof(IsEnvironmentNavigationSelected))]
    public partial bool IsHistoryVisible { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCollectionsVisible))]
    [NotifyPropertyChangedFor(nameof(IsRequestsNavigationSelected))]
    [NotifyPropertyChangedFor(nameof(IsEnvironmentNavigationSelected))]
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
    [NotifyPropertyChangedFor(nameof(IsRequestsNavigationSelected))]
    [NotifyPropertyChangedFor(nameof(IsEnvironmentNavigationSelected))]
    public partial bool IsCollectionRunnerVisible { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCollectionRunnerInteractionEnabled))]
    public partial bool IsCollectionRunnerBusy { get; set; }

    [ObservableProperty]
    public partial bool CollectionRunStopOnFailure { get; set; }

    [ObservableProperty]
    public partial string CollectionRunTitle { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string CollectionRunSummary { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string CollectionRunProgress { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasCollectionRunResult { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCollectionRunnerInteractionEnabled))]
    public partial bool IsCollectionRunExportBusy { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCollectionRunnerInteractionEnabled))]
    public partial bool IsCollectionRunDataBusy { get; set; }

    [ObservableProperty]
    public partial bool HasCollectionRunData { get; set; }

    [ObservableProperty]
    public partial string CollectionRunDataFileName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string CollectionRunDataSummary { get; set; } = string.Empty;

    [ObservableProperty]
    public partial CollectionRunHistoryItemViewModel? SelectedCollectionRunHistoryItem { get; set; }

    [ObservableProperty]
    public partial string CollectionRunHistoryStatus { get; set; } = string.Empty;

    [ObservableProperty]
    public partial decimal CollectionRunHistoryRetentionLimit { get; set; } = 50;

    [ObservableProperty]
    public partial CollectionRunResultFilter SelectedCollectionRunResultFilter { get; set; }

    [ObservableProperty]
    public partial string CollectionRunResultFilterStatus { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasFailedCollectionRunResults { get; set; }

    [ObservableProperty]
    public partial bool CanRerunFailedCollectionResults { get; set; }

    [ObservableProperty]
    public partial string CollectionRunRerunFailedLabel { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string CollectionRunRerunUnavailableReason { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsOnboardingVisible { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOnboardingWelcomeStep))]
    [NotifyPropertyChangedFor(nameof(IsOnboardingPrivacyStep))]
    [NotifyPropertyChangedFor(nameof(IsOnboardingReadyStep))]
    [NotifyPropertyChangedFor(nameof(CanGoToPreviousOnboardingStep))]
    [NotifyPropertyChangedFor(nameof(IsOnboardingFinalStep))]
    public partial int OnboardingStep { get; set; }

    public bool IsOnboardingWelcomeStep => OnboardingStep == 0;

    public bool IsOnboardingPrivacyStep => OnboardingStep == 1;

    public bool IsOnboardingReadyStep => OnboardingStep == 2;

    public bool CanGoToPreviousOnboardingStep => OnboardingStep > 0;

    public bool IsOnboardingFinalStep =>
        OnboardingStep == JsonAppSettingsService.MaximumOnboardingStep;

    [ObservableProperty]
    public partial bool IsTutorialGuideVisible { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTutorialSendStep))]
    [NotifyPropertyChangedFor(nameof(IsTutorialSaveStep))]
    [NotifyPropertyChangedFor(nameof(IsTutorialCompleteStep))]
    public partial TutorialGuideStage TutorialGuideStage { get; set; }

    public bool IsTutorialSendStep => TutorialGuideStage == TutorialGuideStage.Send;

    public bool IsTutorialSaveStep => TutorialGuideStage == TutorialGuideStage.Save;

    public bool IsTutorialCompleteStep => TutorialGuideStage == TutorialGuideStage.Complete;

    public bool IsCollectionRunHistoryEmpty => CollectionRunHistory.Count == 0;

    public bool IsCollectionRunnerInteractionEnabled =>
        !IsCollectionRunnerBusy
        && !IsCollectionRunExportBusy
        && !IsCollectionRunDataBusy;

    public bool IsRequestWorkspaceVisible =>
        !IsApplicationSettingsVisible
        && !IsGitDiffVisible
        && !IsGitCommitVisible
        && !IsGitRemoteVisible
        && !IsGitFastForwardVisible
        && !IsGitPushVisible
        && !IsCollectionRunnerVisible;

    public bool IsRequestsNavigationSelected =>
        !IsApplicationSettingsVisible
        && !IsHistoryVisible
        && !IsGitVisible
        && !IsCollectionRunnerVisible
        && RequestEditorTabIndex != EnvironmentEditorTabIndex;

    public bool IsEnvironmentNavigationSelected =>
        !IsApplicationSettingsVisible
        && !IsHistoryVisible
        && !IsGitVisible
        && !IsCollectionRunnerVisible
        && RequestEditorTabIndex == EnvironmentEditorTabIndex;

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
    [NotifyCanExecuteChangedFor(nameof(RemoveEnvironmentVariableCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveEnvironmentCommand))]
    [NotifyCanExecuteChangedFor(nameof(CreateCollectionCommand))]
    [NotifyCanExecuteChangedFor(nameof(RenameCollectionCommand))]
    [NotifyCanExecuteChangedFor(nameof(ImportPostmanCollectionCommand))]
    public partial bool IsSending { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    [NotifyCanExecuteChangedFor(nameof(CreateWorkspaceCommand))]
    [NotifyCanExecuteChangedFor(nameof(OpenWorkspaceCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveRequestCommand))]
    [NotifyCanExecuteChangedFor(nameof(NewRequestCommand))]
    [NotifyCanExecuteChangedFor(nameof(NewEnvironmentCommand))]
    [NotifyCanExecuteChangedFor(nameof(AddEnvironmentVariableCommand))]
    [NotifyCanExecuteChangedFor(nameof(RemoveEnvironmentVariableCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveEnvironmentCommand))]
    [NotifyCanExecuteChangedFor(nameof(CreateCollectionCommand))]
    [NotifyCanExecuteChangedFor(nameof(RenameCollectionCommand))]
    [NotifyCanExecuteChangedFor(nameof(ImportPostmanCollectionCommand))]
    public partial bool IsWorkspaceBusy { get; set; }

    private readonly IRequestExecutor _requestExecutor;
    private readonly ICollectionRunner _collectionRunner;
    private readonly IWorkspaceStore _workspaceStore;
    private readonly IWorkspaceFolderPicker _folderPicker;
    private readonly IRequestFilePicker _requestFilePicker;
    private readonly IPostmanCollectionImportService _postmanImportService;
    private readonly RequestTemplateResolver _templateResolver;
    private readonly ISecretVault _secretVault;
    private readonly IUnsavedChangesPrompt _unsavedChangesPrompt;
    private readonly IRequestHistoryStore _historyStore;
    private readonly IHistoryClearPrompt _historyClearPrompt;
    private readonly IAppSettingsService _appSettings;
    private readonly IGitService _gitService;
    private readonly IGitSecretScanner _gitSecretScanner;
    private readonly ICollectionRunExportService _collectionRunExportService;
    private readonly ICollectionRunDataFileService _collectionRunDataFileService;
    private readonly ICollectionRunHistoryStore _collectionRunHistoryStore;
    private readonly ICollectionRunHistoryClearPrompt _collectionRunHistoryClearPrompt;
    private readonly ITutorialSessionService _tutorialSessionService;
    private readonly IExternalLinkService _externalLinkService;
    private readonly IClipboardService _clipboardService;
    private readonly IRequestDeletePrompt _requestDeletePrompt;
    private readonly IRequestCookieManager _requestCookieManager;
    private TutorialSession? _activeTutorialSession;
    private WorkspaceSnapshot? _workspaceSnapshot;
    private string? _workspaceDirectory;
    private Guid? _selectedRequestId;
    private Guid? _selectedCollectionId;
    private EnvironmentDocument? _activeEnvironment;
    private Guid? _editingEnvironmentId;
    private string _cleanRequestDraft;
    private bool _hasWorkspaceError;

    public bool IsBodyEnabled => SelectedBodyType != "None";

    public bool IsFormUrlEncodedBody => SelectedBodyType == "Form URL Encoded";

    public bool IsMultipartFormDataBody => SelectedBodyType == "Multipart Form Data";

    public bool IsStructuredFormBody => IsFormUrlEncodedBody || IsMultipartFormDataBody;

    public bool IsRawBodyVisible => IsBodyEnabled && !IsStructuredFormBody;

    public MainViewModel(
        IRequestExecutor requestExecutor,
        ICollectionRunner collectionRunner,
        IWorkspaceStore workspaceStore,
        IWorkspaceFolderPicker folderPicker,
        IRequestFilePicker requestFilePicker,
        IPostmanCollectionImportService postmanImportService,
        RequestTemplateResolver templateResolver,
        ISecretVault secretVault,
        LocalizationService localization,
        ThemeService themes,
        IUnsavedChangesPrompt unsavedChangesPrompt,
        IRequestHistoryStore historyStore,
        IHistoryClearPrompt historyClearPrompt,
        IAppSettingsService appSettings,
        IGitService gitService,
        IGitSecretScanner gitSecretScanner,
        ICollectionRunExportService collectionRunExportService,
        ICollectionRunDataFileService collectionRunDataFileService,
        ICollectionRunHistoryStore collectionRunHistoryStore,
        ICollectionRunHistoryClearPrompt collectionRunHistoryClearPrompt,
        ITutorialSessionService tutorialSessionService,
        IApplicationInfoService applicationInfoService,
        IExternalLinkService externalLinkService,
        ISupportInformationService supportInformationService,
        IClipboardService clipboardService,
        IRequestDeletePrompt requestDeletePrompt,
        IRequestCookieManager requestCookieManager)
    {
        _requestExecutor = requestExecutor;
        _collectionRunner = collectionRunner;
        _workspaceStore = workspaceStore;
        _folderPicker = folderPicker;
        _requestFilePicker = requestFilePicker;
        _postmanImportService = postmanImportService;
        _templateResolver = templateResolver;
        _secretVault = secretVault;
        Localization = localization;
        Themes = themes;
        if (Localization is not null)
        {
            Localization.PropertyChanged += OnLocalizationPropertyChanged;
        }

        RefreshLocalizedShellState();
        WorkspaceStatus = Localize("StatusReady", "Ready");
        ResponseStatus = Localize("StatusReady", "Ready");
        ResponseBody = Localize(
            "ResponseInspectRequest",
            "Send a request to inspect its response.");
        GitSummary = Localize("GitNoWorkspace", "Open a workspace to inspect Git status");
        _unsavedChangesPrompt = unsavedChangesPrompt;
        _historyStore = historyStore;
        _historyClearPrompt = historyClearPrompt;
        _appSettings = appSettings;
        _gitService = gitService;
        _gitSecretScanner = gitSecretScanner;
        _collectionRunExportService = collectionRunExportService;
        _collectionRunDataFileService = collectionRunDataFileService;
        _collectionRunHistoryStore = collectionRunHistoryStore;
        _collectionRunHistoryClearPrompt = collectionRunHistoryClearPrompt;
        _tutorialSessionService = tutorialSessionService;
        _externalLinkService = externalLinkService;
        _clipboardService = clipboardService;
        _requestDeletePrompt = requestDeletePrompt;
        _requestCookieManager = requestCookieManager;
        _requestCookieManager.SetEnabled(appSettings.Current.UseWorkspaceCookies);
        ApplicationInfo = applicationInfoService.Current;
        SupportInformation = supportInformationService.Create(ApplicationInfo);
        HistoryRetentionLimit = appSettings.Current.HistoryRetentionLimit;
        CollectionRunHistoryRetentionLimit = appSettings.Current.CollectionRunHistoryRetentionLimit;
        ResponsePreviewLimitMegabytes = appSettings.Current.ResponsePreviewLimitMegabytes;
        InitializeOnboarding(appSettings.Current);
        _cleanRequestDraft = CaptureRequestDraft();
        EnsureActiveTab();
    }

    [RelayCommand]
    private Task OpenDocumentationAsync() => OpenExternalLinkAsync(DocumentationUri);

    [RelayCommand]
    private Task OpenPrivacyAsync() => OpenExternalLinkAsync(PrivacyUri);

    [RelayCommand]
    private Task OpenSecurityAsync() => OpenExternalLinkAsync(SecurityUri);

    [RelayCommand]
    private Task OpenSupportAsync() => OpenExternalLinkAsync(SupportUri);

    [RelayCommand]
    private async Task CopySupportInformationAsync()
    {
        var copied = await _clipboardService.SetTextAsync(SupportInformation);
        WorkspaceStatus = copied
            ? Localize("SupportInformationCopied", "Support information copied")
            : Localize("SupportInformationCopyFailed", "Support information could not be copied");
    }

    private async Task OpenExternalLinkAsync(Uri uri)
    {
        var opened = await _externalLinkService.OpenAsync(uri);
        WorkspaceStatus = opened
            ? Localize("ExternalLinkOpened", "Opened in your browser")
            : Localize("ExternalLinkFailed", "The link could not be opened");
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
        var selected = _workspaceSnapshot?.Environments.FirstOrDefault(
            environment => string.Equals(environment.Name, value, StringComparison.Ordinal));

        // Clearing the dropdown source (ApplyWorkspace) momentarily pushes a null
        // selection; that must not drop the environment the user picked.
        if (selected is null && _workspaceSnapshot is not null && _activeEnvironment is not null)
        {
            return;
        }

        _activeEnvironment = selected;
        LoadEnvironmentEditor(_activeEnvironment);
        if (_workspaceDirectory is not null)
        {
            RememberWorkspace(_workspaceDirectory, IsActiveTutorialWorkspace(_workspaceDirectory));
        }
    }

    private bool CanSend() => !IsSending && !IsWorkspaceBusy;

    private bool CanManageWorkspace() => !IsWorkspaceBusy && !IsSending;

    private bool CanSaveRequest() =>
        !IsWorkspaceBusy && !IsSending && _workspaceSnapshot is not null;

    private bool CanEditEnvironment() =>
        !IsWorkspaceBusy && !IsSending && _workspaceSnapshot is not null;

    private bool CanManageCollection() =>
        !IsWorkspaceBusy && !IsSending && _workspaceSnapshot is not null;

    public void RefreshWindowClosePreference()
    {
        OnPropertyChanged(nameof(KeepRunningInBackground));
        OnPropertyChanged(nameof(IsWindowClosePreferenceUndecided));
    }

    [RelayCommand]
    private void ResetWindowClosePreference()
    {
        _appSettings.Update(_appSettings.Current with
        {
            WindowCloseBehavior = WindowCloseBehavior.Ask,
        });
        RefreshWindowClosePreference();
    }

    private bool CanClearWorkspaceCookies() => _requestCookieManager.IsEnabled;

    [RelayCommand(CanExecute = nameof(CanClearWorkspaceCookies))]
    private void ClearWorkspaceCookies()
    {
        _requestCookieManager.ClearActiveWorkspace();
        WorkspaceStatus = Localize(
            "StatusWorkspaceCookiesCleared",
            "Cookies cleared for the active workspace");
    }

    public async Task<bool> ConfirmExitAsync(CancellationToken cancellationToken = default)
    {
        if (IsSending
            || IsWorkspaceBusy
            || IsCollectionRunnerBusy
            || IsGitStageBusy
            || IsGitCommitBusy
            || IsGitRemoteBusy
            || IsGitFastForwardBusy
            || IsGitPushBusy)
        {
            WorkspaceStatus = Localize(
                "ExitBusyStatus",
                "Finish or cancel the active operation before exiting");
            return false;
        }

        var hasRequestChanges = HasUnsavedRequestChanges();
        var hasCollectionChanges = HasUnsavedCollectionChanges();
        var hasEnvironmentChanges = HasUnsavedEnvironmentChanges();
        var changedDraftCount = (hasRequestChanges ? 1 : 0)
            + (hasCollectionChanges ? 1 : 0)
            + (hasEnvironmentChanges ? 1 : 0);
        if (changedDraftCount == 0)
        {
            return true;
        }

        var canSave = changedDraftCount == 1 && _workspaceSnapshot is not null;
        if (!canSave)
        {
            WorkspaceStatus = Localize(
                "ExitMultipleDraftsStatus",
                "Save each edited request, collection, or environment before exiting");
        }

        var choice = await _unsavedChangesPrompt.ShowAsync(
            WorkspaceName,
            canSave);
        if (choice == UnsavedChangesChoice.Discard)
        {
            return true;
        }

        if (choice != UnsavedChangesChoice.Save || !canSave)
        {
            return false;
        }

        if (hasRequestChanges)
        {
            await SaveRequestAsync(cancellationToken);
        }
        else if (hasCollectionChanges)
        {
            await RenameCollectionAsync(cancellationToken);
        }
        else if (hasEnvironmentChanges)
        {
            await SaveEnvironmentAsync(cancellationToken);
        }

        return !HasUnsavedWorkspaceChanges();
    }

    private string Localize(string key, string fallback) =>
        Localization?.GetString(key) ?? fallback;

    private string Localize(string key, string fallback, object value) =>
        string.Format(Localize(key, fallback), value);

    private string Localize(string key, string fallback, object first, object second) =>
        string.Format(Localize(key, fallback), first, second);

    private void OnLocalizationPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName != nameof(LocalizationService.SelectedLanguage))
        {
            return;
        }

        RefreshLocalizedShellState();
        if (!IsSending && !IsWorkspaceBusy)
        {
            WorkspaceStatus = Localize("StatusReady", "Ready");
        }

        if (!HasResponse && !IsSending)
        {
            ResponseStatus = Localize("StatusReady", "Ready");
            ResponseBody = Localize(
                "ResponseInspectRequest",
                "Send a request to inspect its response.");
        }

        if (_workspaceDirectory is null)
        {
            GitSummary = Localize("GitNoWorkspace", "Open a workspace to inspect Git status");
        }
    }

    private void RefreshLocalizedShellState()
    {
        if (_workspaceSnapshot is null)
        {
            WorkspaceName = Localize("TextNoWorkspace", "No workspace");
            WorkspaceLocation = Localize(
                "TextChooseWorkspace",
                "Choose a local workspace to begin");
        }

        if (_workspaceSnapshot is null || _workspaceSnapshot.Environments.Count == 0)
        {
            var noEnvironment = Localize("TextNoEnvironment", "No environment");
            EnvironmentNames.Clear();
            EnvironmentNames.Add(noEnvironment);
            EnvironmentName = noEnvironment;
        }
    }

    [RelayCommand]
    private void AddQueryParameter() => QueryParameters.Add(new RequestFieldViewModel());

    [RelayCommand]
    private void AddHeader() => Headers.Add(new RequestFieldViewModel());

    [RelayCommand]
    private void AddFormBodyField() => FormBodyFields.Add(new RequestFieldViewModel());

    [RelayCommand]
    private void AddMultipartFile() => MultipartFileFields.Add(new RequestFileFieldViewModel());

    [RelayCommand]
    private void RemoveQueryParameter(RequestFieldViewModel? field)
    {
        if (field is not null)
        {
            QueryParameters.Remove(field);
        }
    }

    [RelayCommand]
    private void RemoveHeader(RequestFieldViewModel? field)
    {
        if (field is not null)
        {
            Headers.Remove(field);
        }
    }

    [RelayCommand]
    private void RemoveFormBodyField(RequestFieldViewModel? field)
    {
        if (field is not null)
        {
            FormBodyFields.Remove(field);
        }
    }

    [RelayCommand]
    private void RemoveMultipartFile(RequestFileFieldViewModel? field)
    {
        if (field is not null)
        {
            MultipartFileFields.Remove(field);
        }
    }

    [RelayCommand]
    private async Task ChooseMultipartFileAsync(RequestFileFieldViewModel? field)
    {
        if (field is null)
        {
            return;
        }

        var selected = await _requestFilePicker.PickAsync();
        if (selected is null)
        {
            return;
        }

        field.FileName = selected.Name;
        field.LocalPath = selected.LocalPath;
    }

    /// <summary>
    /// Closes the request that is currently open in the editor, honouring the
    /// unsaved-changes prompt first.
    /// </summary>
    [RelayCommand]
    private async Task CloseRequestAsync()
    {
        if (ActiveTab is not { } tab)
        {
            return;
        }

        await CloseTabAsync(tab);
        WorkspaceStatus = Localize("StatusRequestClosed", "Request closed");
    }

    [RelayCommand]
    private async Task CopyResponseAsync()
    {
        var copied = await _clipboardService.SetTextAsync(ResponseBody);
        WorkspaceStatus = copied
            ? Localize("ResponseCopied", "Response copied to the clipboard")
            : Localize("ResponseCopyFailed", "The response could not be copied");
    }

    [RelayCommand(CanExecute = nameof(CanSaveRequest))]
    private Task NewRequestAsync(CancellationToken cancellationToken) => NewTabAsync();

    private void ResetRequestDraft()
    {
        _selectedRequestId = null;
        RequestName = "New request";
        SelectedMethod = "GET";
        Url = string.Empty;
        TimeoutSeconds = 30;
        IsStatusAssertionEnabled = false;
        AssertionExpectedStatusCode = 200;
        IsDurationAssertionEnabled = false;
        AssertionMaximumDurationMilliseconds = 1000;
        IsJsonFieldAssertionEnabled = false;
        AssertionJsonPointer = "/id";

        QueryParameters.Clear();
        QueryParameters.Add(new RequestFieldViewModel());
        Headers.Clear();
        Headers.Add(new RequestFieldViewModel("Accept", "application/json"));

        SelectedBodyType = "None";
        RequestBody = string.Empty;
        FormBodyFields.Clear();
        FormBodyFields.Add(new RequestFieldViewModel());
        MultipartFileFields.Clear();
        ResetAuthenticationDraft();
        ResponseStatus = Localize("StatusReady", "Ready");
        ResponseStatusKind = ResponseStatusKind.Neutral;
        ResponseTime = "—";
        ResponseBody = Localize(
            "ResponseComposeNewRequest",
            "Compose and send a new request.");
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
        ClearWorkspaceError();
        WorkspaceStatus = Localize("StatusChooseWorkspaceFolder", "Choose a workspace folder");

        try
        {
            var directory = await _folderPicker.PickFolderAsync(
                Localize(
                    "DialogChooseWorkspaceFolder",
                    "Choose a folder for the ReqMint workspace"));
            if (directory is null)
            {
                WorkspaceStatus = Localize("StatusReady", "Ready");
                return;
            }

            WorkspaceStatus = Localize("StatusCreatingWorkspace", "Creating workspace...");
            var existingWorkspace = Path.Combine(directory, WorkspaceFileName);
            if (File.Exists(existingWorkspace))
            {
                var existingSnapshot = await _workspaceStore.LoadAsync(directory, cancellationToken);
                ApplyWorkspace(existingSnapshot, directory);
                await LoadHistoryAsync(existingSnapshot.Workspace.Id, cancellationToken);
                await RefreshGitStatusAsync(directory, cancellationToken);
                ResetRequestDraft();
                WorkspaceStatus = Localize(
                    "StatusExistingWorkspaceOpened",
                    "Existing workspace opened");
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
            WorkspaceStatus = Localize(
                "StatusWorkspaceCreationCancelled",
                "Workspace creation cancelled");
        }
        catch (Exception exception)
        {
            ShowWorkspaceError(
                Localize("ErrorCreateWorkspace", "Could not create workspace"),
                exception);
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
        ClearWorkspaceError();
        WorkspaceStatus = Localize("StatusChooseWorkspaceFolder", "Choose a workspace folder");

        try
        {
            var directory = await _folderPicker.PickFolderAsync(
                Localize("DialogOpenWorkspace", "Open a ReqMint workspace"));
            if (directory is null)
            {
                WorkspaceStatus = Localize("StatusReady", "Ready");
                return;
            }

            WorkspaceStatus = Localize("StatusOpeningWorkspace", "Opening workspace...");
            var snapshot = await _workspaceStore.LoadAsync(directory, cancellationToken);
            ApplyWorkspace(snapshot, directory);
            await LoadHistoryAsync(snapshot.Workspace.Id, cancellationToken);
            await RefreshGitStatusAsync(directory, cancellationToken);
            ResetRequestDraft();
            WorkspaceStatus = Localize("StatusWorkspaceOpened", "Workspace opened");
        }
        catch (OperationCanceledException)
        {
            WorkspaceStatus = Localize(
                "StatusWorkspaceOpenCancelled",
                "Opening workspace cancelled");
        }
        catch (Exception exception)
        {
            ShowWorkspaceError(
                Localize("ErrorOpenWorkspace", "Could not open workspace"),
                exception);
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
        ClearWorkspaceError();
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
            AdvanceTutorialAfterSave(document);
        }
        catch (OperationCanceledException)
        {
            WorkspaceStatus = Localize("StatusSaveCancelled", "Save cancelled");
        }
        catch (Exception exception)
        {
            ShowWorkspaceError(
                Localize("ErrorSaveRequest", "Could not save request"),
                exception);
        }
        finally
        {
            IsWorkspaceBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanSend), IncludeCancelCommand = true)]
    private async Task SendAsync(CancellationToken cancellationToken)
    {
        ClearWorkspaceError();
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
            EnsureMultipartFilesSelected(request);
        }
        catch (ArgumentException exception)
        {
            ResponseStatus = Localize("StatusInvalidRequest", "Invalid request");
            ResponseStatusKind = ResponseStatusKind.Failure;
            ResponseBody = DescribeException(exception);
            HasResponse = true;
            return;
        }
        catch (RequestTemplateResolutionException exception)
        {
            ResponseStatus = Localize("StatusMissingVariables", "Missing variables");
            ResponseStatusKind = ResponseStatusKind.Failure;
            ResponseBody = DescribeException(exception);
            HasResponse = true;
            return;
        }
        catch (SecretVaultUnavailableException exception)
        {
            ResponseStatus = Localize("StatusSecretVaultUnavailable", "Secret vault unavailable");
            ResponseStatusKind = ResponseStatusKind.Failure;
            ResponseBody = DescribeException(exception);
            HasResponse = true;
            return;
        }

        IsSending = true;
        ResponseStatus = Localize("StatusSending", "Sending...");
        ResponseStatusKind = ResponseStatusKind.Neutral;
        ResponseTime = "—";

        try
        {
            var response = await _requestExecutor.ExecuteAsync(request, cancellationToken);

            ResponseStatus = $"{response.StatusCode} {response.ReasonPhrase}".TrimEnd();
            ResponseStatusKind = ResponseStatusKinds.FromStatusCode(response.StatusCode);
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
            AdvanceTutorialAfterResponse(request, response);
            await RecordHistoryUnlessTutorialAsync(
                requestDocument,
                request,
                response,
                "completed");
        }
        catch (OperationCanceledException)
        {
            ResponseStatus = Localize("StatusCancelled", "Cancelled");
            ResponseBody = "The request was cancelled.";
            HasResponse = true;
            await RecordHistoryUnlessTutorialAsync(
                requestDocument,
                request,
                response: null,
                "cancelled");
        }
        catch (TimeoutException)
        {
            ResponseStatus = Localize("StatusTimedOut", "Timed out");
            ResponseStatusKind = ResponseStatusKind.Failure;
            ResponseBody = Localize(
                "ErrorRequestTimedOut",
                "The request exceeded the {0} second timeout.",
                request.Timeout.TotalSeconds.ToString("N0"));
            HasResponse = true;
            await RecordHistoryUnlessTutorialAsync(
                requestDocument,
                request,
                response: null,
                "timed-out");
        }
        catch (HttpRequestException exception)
        {
            ResponseStatus = Localize("StatusConnectionFailed", "Connection failed");
            ResponseStatusKind = ResponseStatusKind.Failure;
            // Keep the transport detail: it is diagnostic text from the network
            // stack, not something ReqMint can translate faithfully.
            ResponseBody = Localize(
                "ErrorConnectionFailed",
                "The request could not be sent. Check the address and your connection.")
                + Environment.NewLine
                + Environment.NewLine
                + exception.Message;
            HasResponse = true;
            await RecordHistoryUnlessTutorialAsync(
                requestDocument,
                request,
                response: null,
                "failed");
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
            throw new ArgumentException(Localize(
                "ValidationTimeoutRange",
                "Request timeout must be between 1 and 600 seconds."));
        }

        if (string.IsNullOrWhiteSpace(SelectedMethod))
        {
            throw new ArgumentException(Localize(
                "ValidationMethodRequired",
                "An HTTP method is required."));
        }

        if (string.IsNullOrWhiteSpace(Url))
        {
            throw new ArgumentException(Localize(
                "ValidationUrlRequired",
                "A request URL is required."));
        }

        var isHttpUrl = Uri.TryCreate(Url, UriKind.Absolute, out var parsedUrl) &&
            (parsedUrl.Scheme == Uri.UriSchemeHttp || parsedUrl.Scheme == Uri.UriSchemeHttps);
        if (!isHttpUrl && !RequestTemplate.ContainsVariables(Url))
        {
            throw new ArgumentException(Localize(
                "ValidationUrlInvalid",
                "A valid HTTP URL or URL template is required."));
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
                .Where(field => !string.IsNullOrWhiteSpace(field.Name))
                .Select(field => new RequestField(
                    field.Name.Trim(),
                    field.Value,
                    field.IsEnabled))
                .ToArray(),
            Headers = Headers
                .Where(field => !string.IsNullOrWhiteSpace(field.Name))
                .Select(field => new RequestField(
                    field.Name.Trim(),
                    field.Value,
                    field.IsEnabled))
                .ToArray(),
            Authentication = CreateAuthentication(),
            Body = CreateBody(),
            TimeoutSeconds = (int)TimeoutSeconds,
            Assertions = CreateAssertions(),
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
        _requestCookieManager.SelectWorkspace(directory);
        CloseCollectionRunner();
        IsApplicationSettingsVisible = false;
        var workspaceChanged = _workspaceSnapshot?.Workspace.Id != snapshot.Workspace.Id;
        var isActiveTutorialWorkspace = IsActiveTutorialWorkspace(directory);
        if (_activeTutorialSession is not null && !isActiveTutorialWorkspace)
        {
            _activeTutorialSession = null;
            IsTutorialGuideVisible = false;
        }

        _workspaceSnapshot = snapshot;
        _workspaceDirectory = directory;
        _selectedRequestId = selectedRequestId;
        _selectedCollectionId = selectedCollectionId ?? snapshot.Collections.FirstOrDefault()?.Id;
        CollectionDraftName = snapshot.Collections.FirstOrDefault(
            collection => collection.Id == _selectedCollectionId)?.Name ?? "Requests";

        WorkspaceName = snapshot.Workspace.Name;
        WorkspaceLocation = isActiveTutorialWorkspace
            ? Localize("TutorialWorkspaceLocation", "Temporary local workspace")
            : directory;
        EnvironmentNames.Clear();
        if (snapshot.Environments.Count == 0)
        {
            EnvironmentNames.Add(Localize("TextNoEnvironment", "No environment"));
        }
        else
        {
            foreach (var environment in snapshot.Environments)
            {
                EnvironmentNames.Add(environment.Name);
            }
        }

        // The active environment is tracked by ID, not by name: rebuilding the
        // workspace (saving a request, renaming a collection, reopening the folder)
        // must not silently switch the user onto the first environment in the file.
        var desiredEnvironmentId = selectedEnvironmentId ?? _activeEnvironment?.Id;
        _activeEnvironment = desiredEnvironmentId is null
            ? snapshot.Environments.FirstOrDefault()
            : snapshot.Environments.FirstOrDefault(
                environment => environment.Id == desiredEnvironmentId)
                ?? snapshot.Environments.FirstOrDefault();
        EnvironmentName = _activeEnvironment?.Name ?? EnvironmentNames[0];
        LoadEnvironmentEditor(_activeEnvironment);

        RefreshCollections();

        SyncTabsWithWorkspace(snapshot, workspaceChanged);
        EnsureActiveTab();
        if (ActiveTab is { } activeTab)
        {
            activeTab.RequestId = _selectedRequestId;
            activeTab.CollectionId = _selectedCollectionId;
        }

        RefreshActiveTabHeader();
        RememberWorkspace(directory, isActiveTutorialWorkspace);

        SaveRequestCommand.NotifyCanExecuteChanged();
        NewRequestCommand.NotifyCanExecuteChanged();
        NewEnvironmentCommand.NotifyCanExecuteChanged();
        AddEnvironmentVariableCommand.NotifyCanExecuteChanged();
        RemoveEnvironmentVariableCommand.NotifyCanExecuteChanged();
        SaveEnvironmentCommand.NotifyCanExecuteChanged();
        CreateCollectionCommand.NotifyCanExecuteChanged();
        RenameCollectionCommand.NotifyCanExecuteChanged();
        ImportPostmanCollectionCommand.NotifyCanExecuteChanged();
        UpdateCollectionRunAvailability();
    }

    private void RememberWorkspace(string directory, bool isTutorialWorkspace)
    {
        // Tutorial workspaces live in a temporary folder and must never be restored.
        if (isTutorialWorkspace)
        {
            return;
        }

        var current = _appSettings.Current;
        if (string.Equals(current.LastWorkspaceDirectory, directory, StringComparison.Ordinal)
            && current.LastEnvironmentId == _activeEnvironment?.Id)
        {
            return;
        }

        _appSettings.Update(current with
        {
            LastWorkspaceDirectory = directory,
            LastEnvironmentId = _activeEnvironment?.Id,
        });
    }

    /// <summary>
    /// Reopens the workspace that was active when the application last closed.
    /// Failures are silent by design: a missing or moved folder must never block startup.
    /// </summary>
    public async Task RestoreLastWorkspaceAsync(CancellationToken cancellationToken = default)
    {
        var directory = _appSettings.Current.LastWorkspaceDirectory;
        if (string.IsNullOrWhiteSpace(directory) ||
            !File.Exists(Path.Combine(directory, WorkspaceFileName)))
        {
            return;
        }

        IsWorkspaceBusy = true;
        ClearWorkspaceError();
        try
        {
            var snapshot = await _workspaceStore.LoadAsync(directory, cancellationToken);
            ApplyWorkspace(
                snapshot,
                directory,
                selectedEnvironmentId: _appSettings.Current.LastEnvironmentId);
            await LoadHistoryAsync(snapshot.Workspace.Id, cancellationToken);
            await RefreshGitStatusAsync(directory, cancellationToken);
            ResetRequestDraft();
            WorkspaceStatus = Localize("StatusWorkspaceOpened", "Workspace opened");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            WorkspaceStatus = Localize(
                "StatusLastWorkspaceUnavailable",
                "The last workspace could not be reopened");
            System.Diagnostics.Debug.WriteLine(exception);
        }
        finally
        {
            IsWorkspaceBusy = false;
        }
    }

    partial void OnRequestFilterTextChanged(string value)
    {
        OnPropertyChanged(nameof(IsRequestFilterActive));
        RefreshCollections();
    }

    [RelayCommand]
    private void ClearRequestFilter() => RequestFilterText = string.Empty;

    /// <summary>
    /// Rebuilds the collection tree, honouring the request filter. A collection
    /// whose own name matches keeps all of its requests; otherwise only the
    /// matching requests are listed and empty collections are hidden.
    /// </summary>
    private void RefreshCollections()
    {
        Collections.Clear();
        if (_workspaceSnapshot is null)
        {
            OnPropertyChanged(nameof(IsCollectionListEmpty));
            return;
        }

        var parts = CommandPaletteSearch.Fold(RequestFilterText.Trim())
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);

        foreach (var collection in _workspaceSnapshot.Collections)
        {
            var collectionMatches = parts.Length == 0
                || CommandPaletteSearch.Matches(CommandPaletteSearch.Fold(collection.Name), parts);
            var requests = collection.Requests
                .Where(request => collectionMatches
                    || CommandPaletteSearch.Matches(
                        CommandPaletteSearch.Fold($"{request.Name} {request.Method} {request.Url}"),
                        parts))
                .ToArray();

            if (parts.Length > 0 && requests.Length == 0 && !collectionMatches)
            {
                continue;
            }

            Collections.Add(new CollectionItemViewModel(
                collection.Id,
                collection.Name,
                requests.Select(request => new SavedRequestItemViewModel(
                    request,
                    selected => OpenRequest(selected, collection.Id),
                    selected => DuplicateRequestAsync(selected, collection.Id),
                    selected => DeleteRequestAsync(selected, collection.Id))),
                SelectCollection));
        }

        OnPropertyChanged(nameof(IsCollectionListEmpty));
    }

    private bool IsActiveTutorialWorkspace(string directory) =>
        _activeTutorialSession is { } tutorialSession
        && string.Equals(
            directory,
            tutorialSession.WorkspaceDirectory,
            OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal);

    private async Task SelectCollection(Guid collectionId)
    {
        var collection = _workspaceSnapshot?.Collections.FirstOrDefault(item => item.Id == collectionId);
        if (collection is null)
        {
            return;
        }

        _selectedCollectionId = collection.Id;
        CollectionDraftName = collection.Name;
        UpdateCollectionRunAvailability();
        WorkspaceStatus = collection.Name;
        await Task.CompletedTask;
    }

    // Opening a request no longer threatens unsaved work: it lands in its own
    // tab, so the unsaved prompt belongs to closing a tab instead.
    private Task OpenRequest(RequestDocument request, Guid collectionId) =>
        OpenRequestInTabAsync(request, collectionId);

    private void LoadRequestDraft(RequestDocument request)
    {
        RequestName = request.Name;
        SelectedMethod = request.Method;
        Url = request.Url;
        TimeoutSeconds = request.TimeoutSeconds;

        QueryParameters.Clear();
        foreach (var field in request.QueryParameters)
        {
            QueryParameters.Add(new RequestFieldViewModel(field.Name, field.Value)
            {
                IsEnabled = field.IsEnabled,
            });
        }

        Headers.Clear();
        foreach (var field in request.Headers)
        {
            Headers.Add(new RequestFieldViewModel(field.Name, field.Value)
            {
                IsEnabled = field.IsEnabled,
            });
        }

        LoadAuthenticationDraft(request.Authentication);

        SelectedBodyType = GetBodyType(request.Body?.ContentType);
        RequestBody = request.Body?.Content ?? string.Empty;
        FormBodyFields.Clear();
        foreach (var field in request.Body?.FormFields ?? [])
        {
            FormBodyFields.Add(new RequestFieldViewModel(field.Name, field.Value)
            {
                IsEnabled = field.IsEnabled,
            });
        }

        MultipartFileFields.Clear();
        foreach (var file in request.Body?.FileFields ?? [])
        {
            MultipartFileFields.Add(new RequestFileFieldViewModel(
                file.Name,
                file.FileName,
                file.LocalPath)
            {
                IsEnabled = file.IsEnabled,
            });
        }

        if (IsFormUrlEncodedBody && FormBodyFields.Count == 0)
        {
            FormBodyFields.Add(new RequestFieldViewModel());
        }
        IsStatusAssertionEnabled = request.Assertions.Any(
            assertion => assertion.Kind == RequestAssertionKind.StatusCodeEquals);
        AssertionExpectedStatusCode = request.Assertions.FirstOrDefault(
            assertion => assertion.Kind == RequestAssertionKind.StatusCodeEquals)
            ?.ExpectedStatusCode ?? 200;
        IsDurationAssertionEnabled = request.Assertions.Any(
            assertion => assertion.Kind == RequestAssertionKind.MaximumDuration);
        AssertionMaximumDurationMilliseconds = request.Assertions.FirstOrDefault(
            assertion => assertion.Kind == RequestAssertionKind.MaximumDuration)
            ?.MaximumDurationMilliseconds ?? 1000;
        IsJsonFieldAssertionEnabled = request.Assertions.Any(
            assertion => assertion.Kind == RequestAssertionKind.JsonPointerExists);
        AssertionJsonPointer = request.Assertions.FirstOrDefault(
            assertion => assertion.Kind == RequestAssertionKind.JsonPointerExists)
            ?.JsonPointer ?? "/id";
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

    private void MarkRequestClean()
    {
        _cleanRequestDraft = CaptureRequestDraft();

        // The dirty marker is derived from this baseline, so the tab strip has to
        // be told: otherwise a freshly reset tab keeps looking unsaved and is no
        // longer reused as a scratch pad.
        RefreshActiveTabHeader();
    }

    private bool HasUnsavedRequestChanges() => !string.Equals(
        _cleanRequestDraft,
        CaptureRequestDraft(),
        StringComparison.Ordinal);

    private bool HasUnsavedWorkspaceChanges() =>
        HasUnsavedRequestChanges() || HasUnsavedNonRequestChanges();

    private bool HasUnsavedNonRequestChanges() =>
        HasUnsavedCollectionChanges() || HasUnsavedEnvironmentChanges();

    private bool HasUnsavedCollectionChanges()
    {
        var selectedCollection = _workspaceSnapshot?.Collections.FirstOrDefault(
            collection => collection.Id == _selectedCollectionId);
        return selectedCollection is not null
            && !string.Equals(
                CollectionDraftName,
                selectedCollection.Name,
                StringComparison.Ordinal);
    }

    private bool HasUnsavedEnvironmentChanges()
    {
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
        SelectedAuthenticationTypeIndex,
        AuthenticationBearerToken,
        AuthenticationBasicUsername,
        AuthenticationBasicPassword,
        AuthenticationApiKeyName,
        AuthenticationApiKeyValue,
        AuthenticationApiKeyLocationIndex,
        TimeoutSeconds,
        IsStatusAssertionEnabled,
        AssertionExpectedStatusCode,
        IsDurationAssertionEnabled,
        AssertionMaximumDurationMilliseconds,
        IsJsonFieldAssertionEnabled,
        AssertionJsonPointer,
        Query = QueryParameters.Select(field => new { field.IsEnabled, field.Name, field.Value }),
        Headers = Headers.Select(field => new { field.IsEnabled, field.Name, field.Value }),
        FormBodyFields = FormBodyFields.Select(
            field => new { field.IsEnabled, field.Name, field.Value }),
        MultipartFileFields = MultipartFileFields.Select(
            field => new { field.IsEnabled, field.Name, field.FileName, HasLocalFile = field.LocalPath is not null }),
    });

    private void ShowWorkspaceError(string title, Exception exception)
    {
        _hasWorkspaceError = true;
        WorkspaceStatus = title;
        ResponseStatus = title;
        ResponseStatusKind = ResponseStatusKind.Failure;
        ResponseBody = DescribeException(exception);
        HasResponse = true;
    }

    /// <summary>
    /// Turns an exception raised by a lower layer into text the user can read in
    /// their own language. Anything ReqMint cannot describe keeps its original
    /// technical message so the detail is never lost.
    /// </summary>
    private string DescribeException(Exception exception) => exception switch
    {
        RequestTemplateResolutionException missing => Localize(
            "ErrorMissingEnvironmentValues",
            "Missing environment values: {0}.",
            string.Join(", ", missing.MissingVariables)),
        AuthenticationSecretNotProtectedException unprotected => Localize(
            "ErrorAuthenticationSecretNotProtected",
            "Authentication variable '{0}' must exist and be marked Secret in the active environment.",
            unprotected.VariableName),
        SecretVaultUnavailableException => Localize(
            "ErrorSecretVaultUnavailable",
            "Secure secret storage is not available on this platform yet. "
                + "ReqMint will not use a plaintext fallback."),
        PostmanImportException => Localize(
            "ErrorInvalidPostmanCollection",
            "The selected file is not a valid Postman Collection v2.1 document."),
        _ => exception.Message,
    };

    /// <summary>
    /// Drops a previously shown workspace failure so a later success does not leave
    /// the old error text sitting in the response panel and the status bar.
    /// </summary>
    private void ClearWorkspaceError()
    {
        if (!_hasWorkspaceError)
        {
            return;
        }

        _hasWorkspaceError = false;
        HasResponse = false;
        ResponseStatus = Localize("StatusReady", "Ready");
        ResponseStatusKind = ResponseStatusKind.Neutral;
        ResponseTime = "—";
        ResponseBody = Localize(
            "ResponseInspectRequest",
            "Send a request to inspect its response.");
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

        if (contentType?.Contains("multipart/form-data", StringComparison.OrdinalIgnoreCase) == true)
        {
            return "Multipart Form Data";
        }

        return contentType is null ? "None" : "Text";
    }

    private ApiRequestBody? CreateBody() => SelectedBodyType switch
    {
        "JSON" => new ApiRequestBody(RequestBody, "application/json"),
        "Text" => new ApiRequestBody(RequestBody, "text/plain"),
        "XML" => new ApiRequestBody(RequestBody, "application/xml"),
        "Form URL Encoded" => new ApiRequestBody(string.Empty, "application/x-www-form-urlencoded")
        {
            FormFields = FormBodyFields
                .Where(field => !string.IsNullOrWhiteSpace(field.Name))
                .Select(field => new RequestField(
                    field.Name.Trim(),
                    field.Value,
                    field.IsEnabled))
                .ToArray(),
        },
        "Multipart Form Data" => new ApiRequestBody(string.Empty, "multipart/form-data")
        {
            FormFields = FormBodyFields
                .Where(field => !string.IsNullOrWhiteSpace(field.Name))
                .Select(field => new RequestField(
                    field.Name.Trim(),
                    field.Value,
                    field.IsEnabled))
                .ToArray(),
            FileFields = MultipartFileFields
                .Where(field =>
                    !string.IsNullOrWhiteSpace(field.Name) &&
                    !string.IsNullOrWhiteSpace(field.FileName))
                .Select(field => new RequestFileField(
                    field.Name.Trim(),
                    field.FileName,
                    field.IsEnabled)
                {
                    LocalPath = field.LocalPath,
                })
                .ToArray(),
        },
        _ => null,
    };

    private void EnsureMultipartFilesSelected(ApiRequest request)
    {
        if (request.Body?.ContentType != "multipart/form-data")
        {
            return;
        }

        var missing = request.Body.FileFields.FirstOrDefault(file =>
            file.IsEnabled && string.IsNullOrWhiteSpace(file.LocalPath));
        if (missing is not null)
        {
            throw new ArgumentException(Localize(
                "ErrorMultipartFileRequired",
                "Select the local file '{0}' before sending.",
                missing.FileName));
        }
    }

    private RequestAuthentication? CreateAuthentication()
    {
        return SelectedAuthenticationTypeIndex switch
        {
            0 => null,
            1 => new RequestAuthentication
            {
                Type = RequestAuthenticationType.Bearer,
                BearerToken = ValidateAuthenticationSecretReference(
                    AuthenticationBearerToken,
                    "ValidationAuthBearerSecret",
                    "Bearer token must be a single secret environment variable such as {{TOKEN}}."),
            },
            2 => new RequestAuthentication
            {
                Type = RequestAuthenticationType.Basic,
                BasicUsername = string.IsNullOrWhiteSpace(AuthenticationBasicUsername)
                    ? throw new ArgumentException(Localize(
                        "ValidationAuthUsernameRequired",
                        "Basic Auth username is required."))
                    : AuthenticationBasicUsername.Trim(),
                BasicPassword = ValidateAuthenticationSecretReference(
                    AuthenticationBasicPassword,
                    "ValidationAuthBasicSecret",
                    "Basic Auth password must be a single secret environment variable such as {{PASSWORD}}."),
            },
            3 => new RequestAuthentication
            {
                Type = RequestAuthenticationType.ApiKey,
                ApiKeyName = string.IsNullOrWhiteSpace(AuthenticationApiKeyName)
                    ? throw new ArgumentException(Localize(
                        "ValidationAuthApiKeyNameRequired",
                        "API key name is required."))
                    : AuthenticationApiKeyName.Trim(),
                ApiKeyValue = ValidateAuthenticationSecretReference(
                    AuthenticationApiKeyValue,
                    "ValidationAuthApiKeySecret",
                    "API key value must be a single secret environment variable such as {{API_KEY}}."),
                ApiKeyLocation = AuthenticationApiKeyLocationIndex == 1
                    ? ApiKeyLocation.Query
                    : ApiKeyLocation.Header,
            },
            _ => throw new ArgumentException(Localize(
                "ValidationAuthTypeInvalid",
                "Select a supported authentication type.")),
        };
    }

    private string ValidateAuthenticationSecretReference(
        string value,
        string localizationKey,
        string fallback)
    {
        if (!RequestTemplate.IsVariableReference(value))
        {
            throw new ArgumentException(Localize(localizationKey, fallback));
        }

        return value.Trim();
    }

    private void LoadAuthenticationDraft(RequestAuthentication? authentication)
    {
        ResetAuthenticationDraft();
        if (authentication is null)
        {
            return;
        }

        SelectedAuthenticationTypeIndex = authentication.Type switch
        {
            RequestAuthenticationType.Bearer => 1,
            RequestAuthenticationType.Basic => 2,
            RequestAuthenticationType.ApiKey => 3,
            _ => 0,
        };
        AuthenticationBearerToken = authentication.BearerToken ?? "{{TOKEN}}";
        AuthenticationBasicUsername = authentication.BasicUsername ?? string.Empty;
        AuthenticationBasicPassword = authentication.BasicPassword ?? "{{PASSWORD}}";
        AuthenticationApiKeyName = authentication.ApiKeyName ?? "X-API-Key";
        AuthenticationApiKeyValue = authentication.ApiKeyValue ?? "{{API_KEY}}";
        AuthenticationApiKeyLocationIndex = authentication.ApiKeyLocation == ApiKeyLocation.Query
            ? 1
            : 0;
    }

    private void ResetAuthenticationDraft()
    {
        SelectedAuthenticationTypeIndex = 0;
        AuthenticationBearerToken = "{{TOKEN}}";
        AuthenticationBasicUsername = string.Empty;
        AuthenticationBasicPassword = "{{PASSWORD}}";
        AuthenticationApiKeyName = "X-API-Key";
        AuthenticationApiKeyValue = "{{API_KEY}}";
        AuthenticationApiKeyLocationIndex = 0;
    }

    private IReadOnlyList<RequestAssertion> CreateAssertions()
    {
        var assertions = new List<RequestAssertion>(3);
        if (IsStatusAssertionEnabled)
        {
            assertions.Add(new RequestAssertion
            {
                Kind = RequestAssertionKind.StatusCodeEquals,
                ExpectedStatusCode = (int)AssertionExpectedStatusCode,
            });
        }

        if (IsDurationAssertionEnabled)
        {
            assertions.Add(new RequestAssertion
            {
                Kind = RequestAssertionKind.MaximumDuration,
                MaximumDurationMilliseconds = (int)AssertionMaximumDurationMilliseconds,
            });
        }

        if (IsJsonFieldAssertionEnabled)
        {
            assertions.Add(new RequestAssertion
            {
                Kind = RequestAssertionKind.JsonPointerExists,
                JsonPointer = AssertionJsonPointer.Trim(),
            });
        }

        var validationError = RequestAssertionValidator.GetValidationError(assertions);
        if (validationError is not null)
        {
            throw new ArgumentException(validationError);
        }

        return assertions;
    }

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
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            });
        }
        catch (JsonException)
        {
            return body;
        }
    }
}

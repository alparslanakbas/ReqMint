using ReqMint.App.Services;
using ReqMint.App.ViewModels;
using ReqMint.Core.Git;
using ReqMint.Core.History;
using ReqMint.Core.Requests;
using ReqMint.Core.Runner;
using ReqMint.Core.Security;
using ReqMint.Core.Templates;
using ReqMint.Core.Workspaces;

namespace ReqMint.App.Tests;

public sealed class MainViewModelWorkspaceTests
{
    [Fact]
    public void RailNavigation_SelectsEnvironmentAndSettingsEditorTabs()
    {
        var viewModel = CreateViewModel(new RecordingWorkspaceStore(), CreateWorkspacePath());

        viewModel.ShowEnvironmentEditorCommand.Execute(null);

        Assert.Equal(4, viewModel.RequestEditorTabIndex);
        Assert.True(viewModel.IsRequestWorkspaceVisible);
        Assert.False(viewModel.IsApplicationSettingsVisible);
        Assert.False(viewModel.IsRequestsNavigationSelected);
        Assert.True(viewModel.IsEnvironmentNavigationSelected);

        viewModel.ShowSettingsEditorCommand.Execute(null);

        Assert.Equal(4, viewModel.RequestEditorTabIndex);
        Assert.False(viewModel.IsRequestWorkspaceVisible);
        Assert.True(viewModel.IsApplicationSettingsVisible);
        Assert.False(viewModel.IsRequestsNavigationSelected);
        Assert.False(viewModel.IsEnvironmentNavigationSelected);
    }

    [Fact]
    public async Task StartingTutorial_ReturnsComposerToParametersTab()
    {
        var viewModel = CreateViewModel(new RecordingWorkspaceStore(), CreateWorkspacePath());
        viewModel.ShowSettingsEditorCommand.Execute(null);
        viewModel.OnboardingStep = JsonAppSettingsService.MaximumOnboardingStep;
        viewModel.IsOnboardingVisible = true;

        await viewModel.StartTutorialSampleCommand.ExecuteAsync(null);

        Assert.Equal(0, viewModel.RequestEditorTabIndex);
        Assert.True(viewModel.IsTutorialGuideVisible);
    }

    [Fact]
    public async Task CreateWorkspaceCommand_CreatesInitialCollectionAndEnablesSaving()
    {
        var directory = Path.Combine(Path.GetTempPath(), "ReqMint.Tests", Guid.NewGuid().ToString("N"));
        var store = new RecordingWorkspaceStore();
        var viewModel = CreateViewModel(store, directory);

        await viewModel.CreateWorkspaceCommand.ExecuteAsync(null);

        Assert.Equal(Path.GetFileName(directory), viewModel.WorkspaceName);
        Assert.Equal(directory, store.SavedDirectory);
        Assert.NotNull(store.SavedSnapshot);
        Assert.Single(store.SavedSnapshot.Workspace.Collections);
        Assert.Equal("collections/requests.json", store.SavedSnapshot.Workspace.Collections[0].File);
        Assert.Single(viewModel.Collections);
        Assert.True(viewModel.SaveRequestCommand.CanExecute(null));
    }

    [Fact]
    public async Task OpeningSavedRequest_PopulatesTheComposer()
    {
        var snapshot = CreateSnapshot();
        var store = new RecordingWorkspaceStore { SnapshotToLoad = snapshot };
        var viewModel = CreateViewModel(store, CreateWorkspacePath());

        await viewModel.OpenWorkspaceCommand.ExecuteAsync(null);
        await viewModel.Collections[0].Requests[0].OpenCommand.ExecuteAsync(null);

        Assert.Equal("Create order", viewModel.RequestName);
        Assert.Equal("POST", viewModel.SelectedMethod);
        Assert.Equal("https://api.example.com/orders", viewModel.Url);
        Assert.Equal("JSON", viewModel.SelectedBodyType);
        Assert.Equal("{\"sku\":\"MINT-1\"}", viewModel.RequestBody);
        Assert.Equal(45, viewModel.TimeoutSeconds);
        Assert.Equal("Opened Create order", viewModel.WorkspaceStatus);
    }

    [Fact]
    public async Task SaveRequestCommand_UpdatesSelectedRequestWithoutCreatingADuplicate()
    {
        var snapshot = CreateSnapshot();
        var store = new RecordingWorkspaceStore { SnapshotToLoad = snapshot };
        var viewModel = CreateViewModel(store, CreateWorkspacePath());
        await viewModel.OpenWorkspaceCommand.ExecuteAsync(null);
        await viewModel.Collections[0].Requests[0].OpenCommand.ExecuteAsync(null);
        viewModel.RequestName = "Create mint order";
        viewModel.RequestBody = "{\"sku\":\"MINT-2\"}";

        await viewModel.SaveRequestCommand.ExecuteAsync(null);

        var savedRequest = Assert.Single(store.SavedSnapshot!.Collections[0].Requests);
        Assert.Equal(snapshot.Collections[0].Requests[0].Id, savedRequest.Id);
        Assert.Equal("Create mint order", savedRequest.Name);
        Assert.Equal("{\"sku\":\"MINT-2\"}", savedRequest.Body?.Content);
        Assert.Equal("Saved Create mint order", viewModel.WorkspaceStatus);
    }

    [Fact]
    public async Task SaveRequestCommand_PersistsFormUrlEncodedFields()
    {
        var store = new RecordingWorkspaceStore { SnapshotToLoad = CreateSnapshot() };
        var viewModel = CreateViewModel(store, CreateWorkspacePath());
        await viewModel.OpenWorkspaceCommand.ExecuteAsync(null);
        await viewModel.Collections[0].Requests[0].OpenCommand.ExecuteAsync(null);
        viewModel.SelectedBodyType = "Form URL Encoded";
        viewModel.FormBodyFields.Clear();
        viewModel.FormBodyFields.Add(new RequestFieldViewModel("name", "Mint & Co"));
        viewModel.FormBodyFields.Add(new RequestFieldViewModel("ignored", "value")
        {
            IsEnabled = false,
        });

        await viewModel.SaveRequestCommand.ExecuteAsync(null);

        var body = store.SavedSnapshot!.Collections[0].Requests[0].Body;
        Assert.NotNull(body);
        Assert.Equal("application/x-www-form-urlencoded", body.ContentType);
        Assert.Equal(string.Empty, body.Content);
        Assert.Collection(
            body.FormFields,
            field => Assert.Equal(new RequestField("name", "Mint & Co"), field),
            field => Assert.Equal(new RequestField("ignored", "value", IsEnabled: false), field));
    }

    [Fact]
    public async Task SaveRequestCommand_PersistsRunnerAssertions()
    {
        var snapshot = CreateSnapshot();
        var store = new RecordingWorkspaceStore { SnapshotToLoad = snapshot };
        var viewModel = CreateViewModel(store, CreateWorkspacePath());
        await viewModel.OpenWorkspaceCommand.ExecuteAsync(null);
        await viewModel.Collections[0].Requests[0].OpenCommand.ExecuteAsync(null);
        viewModel.IsStatusAssertionEnabled = true;
        viewModel.AssertionExpectedStatusCode = 201;
        viewModel.IsDurationAssertionEnabled = true;
        viewModel.AssertionMaximumDurationMilliseconds = 750;
        viewModel.IsJsonFieldAssertionEnabled = true;
        viewModel.AssertionJsonPointer = "/data/id";

        await viewModel.SaveRequestCommand.ExecuteAsync(null);

        var assertions = store.SavedSnapshot!.Collections[0].Requests[0].Assertions;
        Assert.Collection(
            assertions,
            assertion =>
            {
                Assert.Equal(RequestAssertionKind.StatusCodeEquals, assertion.Kind);
                Assert.Equal(201, assertion.ExpectedStatusCode);
            },
            assertion =>
            {
                Assert.Equal(RequestAssertionKind.MaximumDuration, assertion.Kind);
                Assert.Equal(750, assertion.MaximumDurationMilliseconds);
            },
            assertion =>
            {
                Assert.Equal(RequestAssertionKind.JsonPointerExists, assertion.Kind);
                Assert.Equal("/data/id", assertion.JsonPointer);
            });
    }

    [Fact]
    public async Task NewRequestCommand_SavesANewDocumentInsteadOfOverwritingTheSelection()
    {
        var snapshot = CreateSnapshot();
        var store = new RecordingWorkspaceStore { SnapshotToLoad = snapshot };
        var viewModel = CreateViewModel(store, CreateWorkspacePath());
        await viewModel.OpenWorkspaceCommand.ExecuteAsync(null);
        await viewModel.Collections[0].Requests[0].OpenCommand.ExecuteAsync(null);

        await viewModel.NewRequestCommand.ExecuteAsync(null);
        viewModel.RequestName = "List orders";
        viewModel.Url = "https://api.example.com/orders";
        await viewModel.SaveRequestCommand.ExecuteAsync(null);

        Assert.Equal(2, store.SavedSnapshot!.Collections[0].Requests.Count);
        Assert.Contains(
            store.SavedSnapshot.Collections[0].Requests,
            request => request.Name == "List orders" && request.Method == "GET");
        Assert.Equal("Saved List orders", viewModel.WorkspaceStatus);
    }

    [Fact]
    public async Task SaveEnvironmentCommand_SeparatesPublicAndSecretValues()
    {
        var snapshot = CreateSnapshot();
        var store = new RecordingWorkspaceStore { SnapshotToLoad = snapshot };
        var vault = new RecordingSecretVault();
        var viewModel = CreateViewModel(store, CreateWorkspacePath(), vault: vault);
        await viewModel.OpenWorkspaceCommand.ExecuteAsync(null);
        viewModel.NewEnvironmentCommand.Execute(null);
        viewModel.EnvironmentDraftName = "Development";
        viewModel.EnvironmentVariables.Clear();
        viewModel.EnvironmentVariables.Add(
            new EnvironmentVariableViewModel("BASE_URL", "https://api.example.com"));
        viewModel.EnvironmentVariables.Add(
            new EnvironmentVariableViewModel("TOKEN", "secret-token", isSecret: true));

        await viewModel.SaveEnvironmentCommand.ExecuteAsync(null);

        var environment = Assert.Single(store.SavedSnapshot!.Environments);
        Assert.Equal("https://api.example.com", environment.Variables[0].Value);
        Assert.Null(environment.Variables[1].Value);
        Assert.True(environment.Variables[1].IsSecret);
        var storedSecret = Assert.Single(vault.StoredValues);
        Assert.Equal("TOKEN", storedSecret.Reference.VariableName);
        Assert.Equal("secret-token", storedSecret.Value);
        Assert.Equal(string.Empty, viewModel.EnvironmentVariables[1].Value);
    }

    [Fact]
    public async Task SendCommand_ResolvesTheActiveEnvironmentBeforeExecution()
    {
        var snapshot = CreateSnapshot();
        var request = snapshot.Collections[0].Requests[0] with
        {
            Url = "{{BASE_URL}}/orders",
            Authentication = new RequestAuthentication
            {
                Type = RequestAuthenticationType.Bearer,
                BearerToken = "{{TOKEN}}",
            },
        };
        var collection = snapshot.Collections[0] with { Requests = [request] };
        var environment = new EnvironmentDocument
        {
            Id = Guid.Parse("44444444-4444-4444-4444-444444444444"),
            Name = "Development",
            Variables =
            [
                new EnvironmentVariable("BASE_URL", "https://api.example.com"),
                new EnvironmentVariable("TOKEN", null, IsSecret: true),
            ],
        };
        snapshot = snapshot with
        {
            Workspace = snapshot.Workspace with
            {
                Environments =
                [
                    new WorkspaceFileReference(
                        environment.Id,
                        environment.Name,
                        "environments/development.json"),
                ],
            },
            Collections = [collection],
            Environments = [environment],
        };
        var store = new RecordingWorkspaceStore { SnapshotToLoad = snapshot };
        var vault = new RecordingSecretVault { ValueToRead = "secret-token" };
        var executor = new RecordingRequestExecutor();
        var viewModel = CreateViewModel(store, CreateWorkspacePath(), executor, vault);
        await viewModel.OpenWorkspaceCommand.ExecuteAsync(null);
        await viewModel.Collections[0].Requests[0].OpenCommand.ExecuteAsync(null);

        await viewModel.SendCommand.ExecuteAsync(null);

        Assert.Equal("https://api.example.com/orders", executor.Request?.Url.AbsoluteUri);
        Assert.Equal(
            "Bearer secret-token",
            Assert.Single(
                executor.Request!.Headers,
                header => header.Name == "Authorization").Value);
        Assert.Equal("200 OK", viewModel.ResponseStatus);
    }

    [Fact]
    public async Task SendCommand_StoresAPrivateBoundedHistorySnapshot()
    {
        var historyStore = new RecordingHistoryStore();
        var executor = new RecordingRequestExecutor();
        var viewModel = CreateViewModel(
            new RecordingWorkspaceStore { SnapshotToLoad = CreateSnapshot() },
            CreateWorkspacePath(),
            executor,
            historyStore: historyStore);
        await viewModel.OpenWorkspaceCommand.ExecuteAsync(null);
        await viewModel.Collections[0].Requests[0].OpenCommand.ExecuteAsync(null);
        viewModel.Headers.Add(new RequestFieldViewModel("Authorization", "Bearer secret-token"));

        await viewModel.SendCommand.ExecuteAsync(null);

        var entry = Assert.Single(historyStore.Entries);
        Assert.Equal("completed", entry.Outcome);
        Assert.Equal(200, entry.StatusCode);
        Assert.Null(entry.Request.Body);
        Assert.Contains(
            entry.Request.Headers,
            header => header.Name == "Authorization" &&
                header.Value == RequestHistoryPrivacy.RedactedValue);
    }

    [Fact]
    public async Task OpeningHistoryEntry_LoadsANewRequestDraft()
    {
        var snapshot = CreateSnapshot();
        var entry = new RequestHistoryEntry
        {
            Id = Guid.NewGuid(),
            WorkspaceId = snapshot.Workspace.Id,
            SentAtUtc = DateTimeOffset.UtcNow,
            Request = snapshot.Collections[0].Requests[0],
            Outcome = "completed",
            StatusCode = 201,
            ReasonPhrase = "Created",
            DurationMilliseconds = 24,
        };
        var historyStore = new RecordingHistoryStore([entry]);
        var viewModel = CreateViewModel(
            new RecordingWorkspaceStore { SnapshotToLoad = snapshot },
            CreateWorkspacePath(),
            historyStore: historyStore);
        await viewModel.OpenWorkspaceCommand.ExecuteAsync(null);

        await viewModel.ShowHistoryCommand.ExecuteAsync(null);
        await viewModel.History[0].OpenCommand.ExecuteAsync(null);

        Assert.Equal("Create order", viewModel.RequestName);
        Assert.Equal("201 Created", viewModel.ResponseStatus);
        Assert.Equal("24 ms", viewModel.ResponseTime);
    }

    [Fact]
    public async Task HistorySearch_FiltersAcrossRequestAndResponseMetadata()
    {
        var snapshot = CreateSnapshot();
        var entries = new[]
        {
            CreateHistoryEntry(snapshot.Workspace.Id, "List orders", "GET", 200),
            CreateHistoryEntry(snapshot.Workspace.Id, "Create customer", "POST", 201),
        };
        var viewModel = CreateViewModel(
            new RecordingWorkspaceStore { SnapshotToLoad = snapshot },
            CreateWorkspacePath(),
            historyStore: new RecordingHistoryStore(entries));
        await viewModel.OpenWorkspaceCommand.ExecuteAsync(null);

        viewModel.HistorySearchText = "post 201";

        var result = Assert.Single(viewModel.History);
        Assert.Equal("Create customer", result.Name);
    }

    [Fact]
    public async Task ClearHistoryCommand_RequiresConfirmationBeforeDeletingEntries()
    {
        var snapshot = CreateSnapshot();
        var historyStore = new RecordingHistoryStore(
            [CreateHistoryEntry(snapshot.Workspace.Id, "List orders", "GET", 200)]);
        var prompt = new StubHistoryClearPrompt { Confirmed = false };
        var viewModel = CreateViewModel(
            new RecordingWorkspaceStore { SnapshotToLoad = snapshot },
            CreateWorkspacePath(),
            historyStore: historyStore,
            historyClearPrompt: prompt);
        await viewModel.OpenWorkspaceCommand.ExecuteAsync(null);

        await viewModel.ClearHistoryCommand.ExecuteAsync(null);

        Assert.Single(historyStore.Entries);
        Assert.Equal(1, prompt.CallCount);

        prompt.Confirmed = true;
        await viewModel.ClearHistoryCommand.ExecuteAsync(null);

        Assert.Empty(historyStore.Entries);
        Assert.Empty(viewModel.History);
        Assert.Equal("Request history cleared", viewModel.WorkspaceStatus);
    }

    [Fact]
    public async Task HistoryRetentionLimit_PersistsAndControlsTrimming()
    {
        var settings = new StubAppSettingsService();
        var historyStore = new RecordingHistoryStore();
        var viewModel = CreateViewModel(
            new RecordingWorkspaceStore { SnapshotToLoad = CreateSnapshot() },
            CreateWorkspacePath(),
            executor: new RecordingRequestExecutor(),
            historyStore: historyStore,
            appSettings: settings);
        await viewModel.OpenWorkspaceCommand.ExecuteAsync(null);
        await viewModel.Collections[0].Requests[0].OpenCommand.ExecuteAsync(null);

        viewModel.HistoryRetentionLimit = 5000;
        Assert.Equal(JsonAppSettingsService.MaximumHistoryRetentionLimit, viewModel.HistoryRetentionLimit);

        viewModel.HistoryRetentionLimit = 350;
        await viewModel.SendCommand.ExecuteAsync(null);

        Assert.Equal(350, settings.Current.HistoryRetentionLimit);
        Assert.Equal(350, historyStore.LastRetentionLimit);
    }

    [Fact]
    public async Task ResponsePreviewLimit_PersistsAndFlowsToHttpExecution()
    {
        var settings = new StubAppSettingsService();
        var executor = new RecordingRequestExecutor { IsBodyTruncated = true };
        var viewModel = CreateViewModel(
            new RecordingWorkspaceStore { SnapshotToLoad = CreateSnapshot() },
            CreateWorkspacePath(),
            executor: executor,
            appSettings: settings);
        await viewModel.OpenWorkspaceCommand.ExecuteAsync(null);
        await viewModel.Collections[0].Requests[0].OpenCommand.ExecuteAsync(null);

        viewModel.ResponsePreviewLimitMegabytes = 50;
        Assert.Equal(JsonAppSettingsService.MaximumResponsePreviewLimitMegabytes, viewModel.ResponsePreviewLimitMegabytes);

        viewModel.ResponsePreviewLimitMegabytes = 5;
        await viewModel.SendCommand.ExecuteAsync(null);

        Assert.Equal(5, settings.Current.ResponsePreviewLimitMegabytes);
        Assert.Equal(5 * 1024 * 1024, executor.Request?.ResponsePreviewLimitBytes);
        Assert.Contains("Preview limited to 5 MB", viewModel.ResponseBody, StringComparison.Ordinal);
    }

    [Fact]
    public void WindowClosePreference_PersistsAndCanBeReset()
    {
        var settings = new StubAppSettingsService();
        var viewModel = CreateViewModel(
            new RecordingWorkspaceStore { SnapshotToLoad = CreateSnapshot() },
            CreateWorkspacePath(),
            appSettings: settings);

        Assert.True(viewModel.IsWindowClosePreferenceUndecided);
        Assert.False(viewModel.KeepRunningInBackground);

        viewModel.KeepRunningInBackground = true;

        Assert.Equal(WindowCloseBehavior.KeepRunning, settings.Current.WindowCloseBehavior);
        Assert.True(viewModel.KeepRunningInBackground);
        Assert.False(viewModel.IsWindowClosePreferenceUndecided);

        viewModel.ResetWindowClosePreferenceCommand.Execute(null);

        Assert.Equal(WindowCloseBehavior.Ask, settings.Current.WindowCloseBehavior);
        Assert.True(viewModel.IsWindowClosePreferenceUndecided);
    }

    [Fact]
    public async Task SaveRequestCommand_PersistsAuthenticationWithoutTheSecretValue()
    {
        var snapshot = CreateSnapshot();
        var store = new RecordingWorkspaceStore { SnapshotToLoad = snapshot };
        var viewModel = CreateViewModel(store, CreateWorkspacePath());
        await viewModel.OpenWorkspaceCommand.ExecuteAsync(null);
        await viewModel.Collections[0].Requests[0].OpenCommand.ExecuteAsync(null);
        viewModel.SelectedAuthenticationTypeIndex = 1;
        viewModel.AuthenticationBearerToken = "{{TOKEN}}";

        await viewModel.SaveRequestCommand.ExecuteAsync(null);

        var authentication = store.SavedSnapshot!.Collections[0].Requests[0].Authentication;
        Assert.NotNull(authentication);
        Assert.Equal(RequestAuthenticationType.Bearer, authentication.Type);
        Assert.Equal("{{TOKEN}}", authentication.BearerToken);
    }

    [Fact]
    public async Task SaveRequestCommand_RejectsLiteralAuthenticationSecrets()
    {
        var store = new RecordingWorkspaceStore { SnapshotToLoad = CreateSnapshot() };
        var viewModel = CreateViewModel(store, CreateWorkspacePath());
        await viewModel.OpenWorkspaceCommand.ExecuteAsync(null);
        await viewModel.Collections[0].Requests[0].OpenCommand.ExecuteAsync(null);
        viewModel.SelectedAuthenticationTypeIndex = 1;
        viewModel.AuthenticationBearerToken = "literal-secret";

        await viewModel.SaveRequestCommand.ExecuteAsync(null);

        Assert.Equal("Could not save request", viewModel.WorkspaceStatus);
        Assert.Contains("{{TOKEN}}", viewModel.ResponseBody, StringComparison.Ordinal);
        Assert.Null(store.SavedSnapshot);
    }

    [Fact]
    public async Task WorkspaceCookieSettings_PersistSelectTheWorkspaceAndClearOnDemand()
    {
        var directory = CreateWorkspacePath();
        var settings = new StubAppSettingsService();
        var cookies = new StubRequestCookieManager();
        var viewModel = CreateViewModel(
            new RecordingWorkspaceStore { SnapshotToLoad = CreateSnapshot() },
            directory,
            appSettings: settings,
            requestCookieManager: cookies);

        Assert.False(viewModel.UseWorkspaceCookies);
        Assert.False(cookies.IsEnabled);

        await viewModel.OpenWorkspaceCommand.ExecuteAsync(null);
        viewModel.UseWorkspaceCookies = true;
        viewModel.ClearWorkspaceCookiesCommand.Execute(null);

        Assert.True(settings.Current.UseWorkspaceCookies);
        Assert.True(cookies.IsEnabled);
        Assert.Equal(Path.GetFullPath(directory), cookies.SelectedWorkspace);
        Assert.Equal(1, cookies.ClearCount);
        Assert.Equal("Cookies cleared for the active workspace", viewModel.WorkspaceStatus);

        viewModel.UseWorkspaceCookies = false;

        Assert.False(settings.Current.UseWorkspaceCookies);
        Assert.False(cookies.IsEnabled);
    }

    [Fact]
    public async Task ConfirmExitAsync_BlocksWhileAnOperationIsActive()
    {
        var prompt = new StubUnsavedChangesPrompt();
        var viewModel = CreateViewModel(
            new RecordingWorkspaceStore { SnapshotToLoad = CreateSnapshot() },
            CreateWorkspacePath(),
            prompt: prompt);
        viewModel.IsSending = true;

        var canExit = await viewModel.ConfirmExitAsync();

        Assert.False(canExit);
        Assert.Equal(0, prompt.CallCount);
        Assert.Equal(
            "Finish or cancel the active operation before exiting",
            viewModel.WorkspaceStatus);
    }

    [Fact]
    public async Task ConfirmExitAsync_SavesASingleEditedDraft()
    {
        var store = new RecordingWorkspaceStore { SnapshotToLoad = CreateSnapshot() };
        var prompt = new StubUnsavedChangesPrompt { Choice = UnsavedChangesChoice.Save };
        var viewModel = CreateViewModel(store, CreateWorkspacePath(), prompt: prompt);
        await viewModel.OpenWorkspaceCommand.ExecuteAsync(null);
        await viewModel.Collections[0].Requests[0].OpenCommand.ExecuteAsync(null);
        viewModel.Url = "https://api.example.com/changed-before-exit";

        var canExit = await viewModel.ConfirmExitAsync();

        Assert.True(canExit);
        Assert.True(prompt.LastCanSave);
        Assert.Equal(
            "https://api.example.com/changed-before-exit",
            store.SavedSnapshot!.Collections[0].Requests[0].Url);
    }

    [Fact]
    public async Task ConfirmExitAsync_RequiresSeparateSavesForSeveralDraftTypes()
    {
        var store = new RecordingWorkspaceStore { SnapshotToLoad = CreateSnapshot() };
        var prompt = new StubUnsavedChangesPrompt { Choice = UnsavedChangesChoice.Save };
        var viewModel = CreateViewModel(store, CreateWorkspacePath(), prompt: prompt);
        await viewModel.OpenWorkspaceCommand.ExecuteAsync(null);
        await viewModel.Collections[0].Requests[0].OpenCommand.ExecuteAsync(null);
        viewModel.Url = "https://api.example.com/changed-before-exit";
        viewModel.CollectionDraftName = "Renamed during exit";

        var canExit = await viewModel.ConfirmExitAsync();

        Assert.False(canExit);
        Assert.False(prompt.LastCanSave);
        Assert.Null(store.SavedSnapshot);
        Assert.Equal(
            "Save each edited request, collection, or environment before exiting",
            viewModel.WorkspaceStatus);
    }

    [Fact]
    public void Onboarding_ResumesAndPersistsCompletionLocally()
    {
        var settings = new StubAppSettingsService(new AppSettings
        {
            OnboardingStatus = OnboardingStatus.InProgress,
            OnboardingStep = 1,
        });
        var viewModel = CreateViewModel(
            new RecordingWorkspaceStore { SnapshotToLoad = CreateSnapshot() },
            CreateWorkspacePath(),
            appSettings: settings);

        Assert.True(viewModel.IsOnboardingVisible);
        Assert.True(viewModel.IsOnboardingPrivacyStep);

        viewModel.PreviousOnboardingStepCommand.Execute(null);

        Assert.True(viewModel.IsOnboardingWelcomeStep);
        Assert.Equal(0, settings.Current.OnboardingStep);

        viewModel.ContinueOnboardingCommand.Execute(null);
        Assert.True(viewModel.IsOnboardingPrivacyStep);

        viewModel.ContinueOnboardingCommand.Execute(null);

        Assert.True(viewModel.IsOnboardingReadyStep);
        Assert.Equal(OnboardingStatus.InProgress, settings.Current.OnboardingStatus);
        Assert.Equal(2, settings.Current.OnboardingStep);

        viewModel.ContinueOnboardingCommand.Execute(null);

        Assert.False(viewModel.IsOnboardingVisible);
        Assert.Equal(OnboardingStatus.Completed, settings.Current.OnboardingStatus);
        Assert.Equal("Welcome to ReqMint", viewModel.WorkspaceStatus);
    }

    [Fact]
    public void Onboarding_CanBeSkippedAndRestartedFromSettings()
    {
        var settings = new StubAppSettingsService();
        var viewModel = CreateViewModel(
            new RecordingWorkspaceStore { SnapshotToLoad = CreateSnapshot() },
            CreateWorkspacePath(),
            appSettings: settings);

        Assert.True(viewModel.IsOnboardingVisible);

        viewModel.SkipOnboardingCommand.Execute(null);

        Assert.False(viewModel.IsOnboardingVisible);
        Assert.Equal(OnboardingStatus.Skipped, settings.Current.OnboardingStatus);

        viewModel.RestartOnboardingCommand.Execute(null);

        Assert.True(viewModel.IsOnboardingVisible);
        Assert.True(viewModel.IsOnboardingWelcomeStep);
        Assert.Equal(OnboardingStatus.InProgress, settings.Current.OnboardingStatus);
        Assert.Equal(0, settings.Current.OnboardingStep);
    }

    [Fact]
    public async Task Onboarding_LocalTutorialGuidesSendAndSaveWithoutChangingAnotherWorkspace()
    {
        var existingSnapshot = CreateSnapshot();
        var existingDirectory = CreateWorkspacePath();
        var store = new RecordingWorkspaceStore { SnapshotToLoad = existingSnapshot };
        var executor = new RecordingRequestExecutor();
        var historyStore = new RecordingHistoryStore();
        var settings = new StubAppSettingsService(new AppSettings
        {
            OnboardingStatus = OnboardingStatus.InProgress,
            OnboardingStep = JsonAppSettingsService.MaximumOnboardingStep,
        });
        var tutorialSession = CreateTutorialSession();
        var tutorial = new StubTutorialSessionService(tutorialSession);
        var git = new StubGitService
        {
            Status = new GitRepositoryStatus
            {
                RepositoryRoot = tutorialSession.WorkspaceDirectory,
                Branch = "main",
            },
        };
        var viewModel = CreateViewModel(
            store,
            existingDirectory,
            executor: executor,
            historyStore: historyStore,
            appSettings: settings,
            tutorialSessionService: tutorial,
            gitService: git);
        await viewModel.OpenWorkspaceCommand.ExecuteAsync(null);

        await viewModel.StartTutorialSampleCommand.ExecuteAsync(null);

        Assert.Equal(1, tutorial.CallCount);
        Assert.False(viewModel.IsOnboardingVisible);
        Assert.True(viewModel.IsTutorialGuideVisible);
        Assert.True(viewModel.IsTutorialSendStep);
        Assert.Equal("ReqMint Tutorial", viewModel.WorkspaceName);
        Assert.Equal("Temporary local workspace", viewModel.WorkspaceLocation);
        Assert.Equal("Temporary local workspace", viewModel.GitRepositoryRoot);
        Assert.Equal("{{TUTORIAL_BASE_URL}}/api/hello", viewModel.Url);
        Assert.Equal("Tutorial", viewModel.EnvironmentName);
        Assert.Null(store.SavedSnapshot);
        Assert.Equal("Commerce", existingSnapshot.Workspace.Name);

        await viewModel.SendCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsTutorialSaveStep);
        Assert.Equal(
            "http://127.0.0.1:43210/api/hello",
            executor.Request!.Url.AbsoluteUri);
        Assert.Empty(historyStore.Entries);

        await viewModel.SaveRequestCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsTutorialCompleteStep);
        Assert.Equal("Temporary local workspace", viewModel.WorkspaceLocation);
        Assert.Equal("First local request completed", viewModel.WorkspaceStatus);
        Assert.Equal(
            "Say hello to ReqMint",
            Assert.Single(Assert.Single(store.SavedSnapshot!.Collections).Requests).Name);
        Assert.Equal(tutorialSession.WorkspaceDirectory, store.SavedDirectory);
        Assert.NotEqual(existingDirectory, store.SavedDirectory);
    }

    [Fact]
    public async Task Onboarding_LocalTutorialDoesNotReplaceUnsavedWorkspaceEdits()
    {
        var store = new RecordingWorkspaceStore { SnapshotToLoad = CreateSnapshot() };
        var settings = new StubAppSettingsService(new AppSettings
        {
            OnboardingStatus = OnboardingStatus.InProgress,
            OnboardingStep = JsonAppSettingsService.MaximumOnboardingStep,
        });
        var tutorial = new StubTutorialSessionService(CreateTutorialSession());
        var viewModel = CreateViewModel(
            store,
            CreateWorkspacePath(),
            appSettings: settings,
            tutorialSessionService: tutorial);
        await viewModel.OpenWorkspaceCommand.ExecuteAsync(null);
        viewModel.CollectionDraftName = "Unsaved collection name";

        await viewModel.StartTutorialSampleCommand.ExecuteAsync(null);

        Assert.Equal(0, tutorial.CallCount);
        Assert.Equal("Commerce", viewModel.WorkspaceName);
        Assert.Equal(
            "Save or discard workspace edits before opening the tutorial",
            viewModel.WorkspaceStatus);
        Assert.True(viewModel.IsOnboardingVisible);
    }

    [Fact]
    public async Task OpeningWorkspace_LoadsReadOnlyGitStatus()
    {
        var git = new StubGitService
        {
            Status = new GitRepositoryStatus
            {
                RepositoryRoot = "C:/repos/commerce",
                Branch = "feature/mint-history",
                AheadBy = 1,
                Changes =
                [
                    new GitFileChange("collections/commerce.json", " M"),
                    new GitFileChange("environments/local.json", "??"),
                    new GitFileChange("src/ReqMint.App/App.axaml.cs", " M"),
                ],
            },
        };
        var viewModel = CreateViewModel(
            new RecordingWorkspaceStore { SnapshotToLoad = CreateSnapshot() },
            CreateWorkspacePath(),
            gitService: git);

        await viewModel.OpenWorkspaceCommand.ExecuteAsync(null);
        await viewModel.ShowGitCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsGitVisible);
        Assert.False(viewModel.IsHistoryVisible);
        Assert.Equal("feature/mint-history", viewModel.GitBranch);
        Assert.Equal("2 ReqMint file changes · 1 other repository changes · ahead 1", viewModel.GitSummary);
        Assert.Equal(2, viewModel.GitChanges.Count);
        Assert.Equal(1, viewModel.GitOtherChangeCount);
        Assert.Equal("Security check passed", viewModel.GitSecuritySummary);
        Assert.False(viewModel.HasGitSecurityWarning);
        Assert.Equal("C:/repos/commerce", viewModel.GitRepositoryRoot);
        Assert.True(git.CallCount >= 2);
    }

    [Fact]
    public async Task OpeningWorkspace_SurfacesSecretPreflightWarningsWithoutValues()
    {
        var git = new StubGitService
        {
            Status = new GitRepositoryStatus
            {
                RepositoryRoot = "C:/repos/commerce",
                Branch = "main",
                Changes =
                [
                    new GitFileChange("environments/local.json", " M"),
                    new GitFileChange("src/Program.cs", " M"),
                ],
            },
        };
        var scanner = new StubGitSecretScanner
        {
            Result = new GitSecretScanResult
            {
                Findings =
                [
                    new GitSecretFinding(
                        "environments/local.json",
                        "$.variables[0].value",
                        GitSecretFindingKind.PersistedSecretValue),
                ],
            },
        };
        var viewModel = CreateViewModel(
            new RecordingWorkspaceStore { SnapshotToLoad = CreateSnapshot() },
            CreateWorkspacePath(),
            gitService: git,
            gitSecretScanner: scanner);

        await viewModel.OpenWorkspaceCommand.ExecuteAsync(null);

        Assert.True(viewModel.HasGitSecurityWarning);
        Assert.Equal(1, viewModel.GitSecretWarningCount);
        Assert.Equal(
            "Security check found 1 possible secret findings across 1 files",
            viewModel.GitSecuritySummary);
        Assert.Equal(["environments/local.json"], scanner.LastPaths);
    }

    [Fact]
    public async Task OpeningGitChange_ShowsBoundedReadOnlyDiffPreview()
    {
        var git = new StubGitService
        {
            Status = new GitRepositoryStatus
            {
                RepositoryRoot = "C:/repos/commerce",
                Branch = "main",
                Changes = [new GitFileChange("collections/orders.json", " M")],
            },
            DiffContent =
                "diff --git a/collections/orders.json b/collections/orders.json\n" +
                "@@ -1 +1 @@\n" +
                "-old\n" +
                "+new",
        };
        var viewModel = CreateViewModel(
            new RecordingWorkspaceStore { SnapshotToLoad = CreateSnapshot() },
            CreateWorkspacePath(),
            gitService: git);

        await viewModel.OpenWorkspaceCommand.ExecuteAsync(null);
        await Assert.Single(viewModel.GitChanges).OpenCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsGitDiffVisible);
        Assert.Equal("collections/orders.json", viewModel.GitDiffPath);
        Assert.Equal(GitDiffScope.WorkingTree, git.LastDiffScope);
        Assert.Equal(4, viewModel.GitDiffLines.Count);
        Assert.True(viewModel.GitDiffLines[2].IsRemoved);
        Assert.True(viewModel.GitDiffLines[3].IsAdded);
        Assert.False(viewModel.IsGitDiffSecurityBlocked);
    }

    [Fact]
    public async Task OpeningStagedGitChange_BlocksUnsafePreviewContent()
    {
        var git = new StubGitService
        {
            Status = new GitRepositoryStatus
            {
                RepositoryRoot = "C:/repos/commerce",
                Branch = "main",
                Changes = [new GitFileChange("environments/local.json", "M ")],
            },
            DiffState = GitDiffPreviewState.BlockedBySecurity,
            DiffSecurityWarningCount = 1,
            DiffContent = "+never-display-this-secret",
        };
        var viewModel = CreateViewModel(
            new RecordingWorkspaceStore { SnapshotToLoad = CreateSnapshot() },
            CreateWorkspacePath(),
            gitService: git);

        await viewModel.OpenWorkspaceCommand.ExecuteAsync(null);
        await Assert.Single(viewModel.GitChanges).OpenCommand.ExecuteAsync(null);

        Assert.Equal(GitDiffScope.Staged, git.LastDiffScope);
        Assert.True(viewModel.IsGitDiffSecurityBlocked);
        Assert.Empty(viewModel.GitDiffLines);
        Assert.DoesNotContain(
            "never-display",
            viewModel.GitDiffMessage,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task OpeningConflictedReqMintFile_ShowsGuidanceWithoutRunningDiff()
    {
        var git = new StubGitService
        {
            Status = new GitRepositoryStatus
            {
                RepositoryRoot = "C:/repos/commerce",
                Branch = "main",
                Changes = [new GitFileChange("collections/orders.json", "UU")],
            },
        };
        var viewModel = CreateViewModel(
            new RecordingWorkspaceStore { SnapshotToLoad = CreateSnapshot() },
            CreateWorkspacePath(),
            gitService: git);

        await viewModel.OpenWorkspaceCommand.ExecuteAsync(null);
        var change = Assert.Single(viewModel.GitChanges);
        await change.OpenCommand.ExecuteAsync(null);

        Assert.True(change.IsConflict);
        Assert.Equal("!", change.Status);
        Assert.Equal(1, viewModel.GitConflictCount);
        Assert.Equal("1 ReqMint file changes · Conflicts: 1", viewModel.GitSummary);
        Assert.True(viewModel.IsGitConflictGuidanceVisible);
        Assert.False(viewModel.HasGitWorkingTreeDiff);
        Assert.False(viewModel.HasGitStagedDiff);
        Assert.Equal(0, git.DiffCallCount);
    }

    [Fact]
    public async Task StagingAFile_RequiresReviewAndExplicitConfirmation()
    {
        var git = new StubGitService
        {
            Status = new GitRepositoryStatus
            {
                RepositoryRoot = "C:/repos/commerce",
                Branch = "main",
                Changes = [new GitFileChange("collections/orders.json", " M")],
            },
            DiffContent = "+{\"name\":\"Orders\"}",
        };
        var viewModel = CreateViewModel(
            new RecordingWorkspaceStore { SnapshotToLoad = CreateSnapshot() },
            CreateWorkspacePath(),
            gitService: git);

        await viewModel.OpenWorkspaceCommand.ExecuteAsync(null);
        await Assert.Single(viewModel.GitChanges).OpenCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsGitStageAvailable);
        viewModel.ReviewGitStageCommand.Execute(null);
        Assert.True(viewModel.IsGitStageReviewVisible);
        Assert.Equal(0, git.StageCallCount);

        await viewModel.ConfirmGitStageCommand.ExecuteAsync(null);

        Assert.Equal(1, git.StageCallCount);
        Assert.Equal("collections/orders.json", git.LastStagedPath);
        Assert.Equal("File staged safely", viewModel.WorkspaceStatus);
        Assert.False(viewModel.IsGitStageReviewVisible);
    }

    [Fact]
    public async Task CommittingStagedFiles_RequiresPreflightAndExplicitConfirmation()
    {
        var git = new StubGitService
        {
            Status = new GitRepositoryStatus
            {
                RepositoryRoot = "C:/repos/commerce",
                Branch = "main",
                Changes = [new GitFileChange("collections/orders.json", "M ")],
            },
            CommitPreflight = new GitCommitPreflight
            {
                State = GitCommitPreflightState.Ready,
                StagedPaths = ["collections/orders.json"],
            },
            CommitResult = new GitCommitResult
            {
                State = GitCommitResultState.Committed,
                CommitId = "abc123def456",
            },
        };
        var viewModel = CreateViewModel(
            new RecordingWorkspaceStore { SnapshotToLoad = CreateSnapshot() },
            CreateWorkspacePath(),
            gitService: git);

        await viewModel.OpenWorkspaceCommand.ExecuteAsync(null);
        Assert.True(viewModel.IsGitCommitReviewAvailable);

        await viewModel.ReviewGitCommitCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsGitCommitVisible);
        Assert.Equal(["collections/orders.json"], viewModel.GitCommitFiles);
        Assert.Equal(1, git.CommitPreflightCallCount);
        Assert.Equal(0, git.CommitCallCount);

        viewModel.GitCommitMessage = "chore: update orders request";
        await viewModel.ConfirmGitCommitCommand.ExecuteAsync(null);

        Assert.Equal(1, git.CommitCallCount);
        Assert.Equal("chore: update orders request", git.LastCommitMessage);
        Assert.Equal("Commit abc123def456 created safely", viewModel.WorkspaceStatus);
        Assert.False(viewModel.IsGitCommitVisible);
    }

    [Fact]
    public async Task CommitReview_ExplainsWhyMixedStagedScopeIsBlocked()
    {
        var git = new StubGitService
        {
            Status = new GitRepositoryStatus
            {
                RepositoryRoot = "C:/repos/commerce",
                Branch = "main",
                Changes =
                [
                    new GitFileChange("collections/orders.json", "M "),
                    new GitFileChange("src/Program.cs", "M "),
                ],
            },
            CommitPreflight = new GitCommitPreflight
            {
                State = GitCommitPreflightState.ContainsOtherStagedFiles,
                StagedPaths = ["collections/orders.json"],
                OtherStagedFileCount = 1,
            },
        };
        var viewModel = CreateViewModel(
            new RecordingWorkspaceStore { SnapshotToLoad = CreateSnapshot() },
            CreateWorkspacePath(),
            gitService: git);

        await viewModel.OpenWorkspaceCommand.ExecuteAsync(null);
        Assert.True(viewModel.IsGitCommitReviewAvailable);

        await viewModel.ReviewGitCommitCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsGitCommitVisible);
        Assert.Equal(
            "Commit blocked because non-ReqMint files are staged",
            viewModel.WorkspaceStatus);
        Assert.Equal(1, git.CommitPreflightCallCount);
        Assert.Equal(0, git.CommitCallCount);
    }

    [Fact]
    public async Task FetchingRemoteUpdates_RequiresReviewAndExplicitConfirmation()
    {
        var git = new StubGitService
        {
            Status = new GitRepositoryStatus
            {
                RepositoryRoot = "C:/repos/commerce",
                Branch = "main",
            },
            RemotePreflight = new GitRemotePreflight
            {
                State = GitRemotePreflightState.Ready,
                RemoteName = "origin",
                Branch = "main",
            },
            FetchResult = new GitFetchResult
            {
                State = GitFetchResultState.Fetched,
                AheadBy = 1,
                BehindBy = 2,
            },
        };
        var viewModel = CreateViewModel(
            new RecordingWorkspaceStore { SnapshotToLoad = CreateSnapshot() },
            CreateWorkspacePath(),
            gitService: git);

        await viewModel.OpenWorkspaceCommand.ExecuteAsync(null);
        Assert.True(viewModel.IsGitRemoteReviewAvailable);

        await viewModel.ReviewGitRemoteCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsGitRemoteVisible);
        Assert.Equal("origin", viewModel.GitRemoteName);
        Assert.Equal("main", viewModel.GitRemoteBranch);
        Assert.Equal(1, git.RemotePreflightCallCount);
        Assert.Equal(0, git.FetchCallCount);

        await viewModel.ConfirmGitFetchCommand.ExecuteAsync(null);

        Assert.Equal(1, git.FetchCallCount);
        Assert.Equal("Remote check complete · behind 2 · ahead 1", viewModel.WorkspaceStatus);
        Assert.False(viewModel.IsGitRemoteVisible);
    }

    [Fact]
    public async Task RemoteReview_ExplainsMissingUpstreamWithoutNetworkAccess()
    {
        var git = new StubGitService
        {
            Status = new GitRepositoryStatus
            {
                RepositoryRoot = "C:/repos/local-only",
                Branch = "main",
            },
            RemotePreflight = new GitRemotePreflight
            {
                State = GitRemotePreflightState.NoUpstream,
                Branch = "main",
            },
        };
        var viewModel = CreateViewModel(
            new RecordingWorkspaceStore { SnapshotToLoad = CreateSnapshot() },
            CreateWorkspacePath(),
            gitService: git);

        await viewModel.OpenWorkspaceCommand.ExecuteAsync(null);
        await viewModel.ReviewGitRemoteCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsGitRemoteVisible);
        Assert.Equal("The current branch has no upstream remote", viewModel.WorkspaceStatus);
        Assert.Equal(1, git.RemotePreflightCallCount);
        Assert.Equal(0, git.FetchCallCount);
    }

    [Fact]
    public async Task FastForwardUpdate_RequiresPreviewAndExplicitConfirmation()
    {
        var git = new StubGitService
        {
            Status = new GitRepositoryStatus
            {
                RepositoryRoot = "C:/repos/commerce",
                Branch = "main",
                BehindBy = 1,
            },
            FastForwardPreflight = new GitFastForwardPreflight
            {
                State = GitFastForwardPreflightState.Ready,
                Remote = new GitRemotePreflight
                {
                    State = GitRemotePreflightState.Ready,
                    RemoteName = "origin",
                    Branch = "main",
                    BehindBy = 1,
                },
                CommitSummaries = ["abc123 · update orders"],
                ChangedPaths = ["collections/orders.json"],
            },
            FastForwardResult = new GitFastForwardResult
            {
                State = GitFastForwardResultState.Updated,
                PreviousCommitId = "111111111111",
                CurrentCommitId = "222222222222",
            },
        };
        var viewModel = CreateViewModel(
            new RecordingWorkspaceStore { SnapshotToLoad = CreateSnapshot() },
            CreateWorkspacePath(),
            gitService: git);

        await viewModel.OpenWorkspaceCommand.ExecuteAsync(null);
        Assert.True(viewModel.IsGitFastForwardReviewAvailable);

        await viewModel.ReviewGitFastForwardCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsGitFastForwardVisible);
        Assert.Equal(["abc123 · update orders"], viewModel.GitFastForwardCommits);
        Assert.Equal(["collections/orders.json"], viewModel.GitFastForwardPaths);
        Assert.Equal(1, git.FastForwardPreflightCallCount);
        Assert.Equal(0, git.FastForwardCallCount);

        await viewModel.ConfirmGitFastForwardCommand.ExecuteAsync(null);

        Assert.Equal(1, git.FastForwardCallCount);
        Assert.Equal(
            "Workspace updated 111111111111 → 222222222222",
            viewModel.WorkspaceStatus);
        Assert.False(viewModel.IsGitFastForwardVisible);
    }

    [Fact]
    public async Task FastForwardReview_BlocksUnsavedRequestDraftBeforeGitAccess()
    {
        var git = new StubGitService
        {
            Status = new GitRepositoryStatus
            {
                RepositoryRoot = "C:/repos/commerce",
                Branch = "main",
                BehindBy = 1,
            },
        };
        var viewModel = CreateViewModel(
            new RecordingWorkspaceStore { SnapshotToLoad = CreateSnapshot() },
            CreateWorkspacePath(),
            gitService: git);
        await viewModel.OpenWorkspaceCommand.ExecuteAsync(null);
        viewModel.Url = "https://api.example.com/unsaved-change";

        await viewModel.ReviewGitFastForwardCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsGitFastForwardVisible);
        Assert.Equal(
            "Save or discard current workspace edits before updating",
            viewModel.WorkspaceStatus);
        Assert.Equal(0, git.FastForwardPreflightCallCount);
    }

    [Fact]
    public async Task FastForwardReview_BlocksUnsavedEnvironmentEditorBeforeGitAccess()
    {
        var git = new StubGitService
        {
            Status = new GitRepositoryStatus
            {
                RepositoryRoot = "C:/repos/commerce",
                Branch = "main",
                BehindBy = 1,
            },
        };
        var viewModel = CreateViewModel(
            new RecordingWorkspaceStore { SnapshotToLoad = CreateSnapshot() },
            CreateWorkspacePath(),
            gitService: git);
        await viewModel.OpenWorkspaceCommand.ExecuteAsync(null);
        viewModel.NewEnvironmentCommand.Execute(null);

        await viewModel.ReviewGitFastForwardCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsGitFastForwardVisible);
        Assert.Equal(
            "Save or discard current workspace edits before updating",
            viewModel.WorkspaceStatus);
        Assert.Equal(0, git.FastForwardPreflightCallCount);
    }

    [Fact]
    public async Task Push_RequiresPreviewAndExplicitConfirmation()
    {
        var pushPreflight = new GitPushPreflight
        {
            State = GitPushPreflightState.Ready,
            Remote = new GitRemotePreflight
            {
                State = GitRemotePreflightState.Ready,
                RemoteName = "origin",
                Branch = "main",
                AheadBy = 1,
            },
            CommitSummaries = ["abc123 · update orders"],
            ChangedPaths = ["collections/orders.json"],
        };
        var git = new StubGitService
        {
            Status = new GitRepositoryStatus
            {
                RepositoryRoot = "C:/repos/commerce",
                Branch = "main",
                AheadBy = 1,
            },
            PushPreflight = pushPreflight,
            PushResult = new GitPushResult
            {
                State = GitPushResultState.Pushed,
                Preflight = pushPreflight,
                CurrentCommitId = "abc123",
            },
        };
        var viewModel = CreateViewModel(
            new RecordingWorkspaceStore { SnapshotToLoad = CreateSnapshot() },
            CreateWorkspacePath(),
            gitService: git);

        await viewModel.OpenWorkspaceCommand.ExecuteAsync(null);
        Assert.True(viewModel.IsGitPushReviewAvailable);

        await viewModel.ReviewGitPushCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsGitPushVisible);
        Assert.Equal(["abc123 · update orders"], viewModel.GitPushCommits);
        Assert.Equal(["collections/orders.json"], viewModel.GitPushPaths);
        Assert.Equal(1, git.PushPreflightCallCount);
        Assert.Equal(0, git.PushCallCount);

        await viewModel.ConfirmGitPushCommand.ExecuteAsync(null);

        Assert.Equal(1, git.PushCallCount);
        Assert.Equal("Pushed 1 commits to origin/main", viewModel.WorkspaceStatus);
        Assert.False(viewModel.IsGitPushVisible);
    }

    [Fact]
    public async Task OpeningWorkspace_DoesNotListChangesOutsideReqMintScope()
    {
        var git = new StubGitService
        {
            Status = new GitRepositoryStatus
            {
                RepositoryRoot = "C:/repos/commerce",
                Branch = "main",
                Changes = [new GitFileChange("src/Program.cs", " M")],
            },
        };
        var viewModel = CreateViewModel(
            new RecordingWorkspaceStore { SnapshotToLoad = CreateSnapshot() },
            CreateWorkspacePath(),
            gitService: git);

        await viewModel.OpenWorkspaceCommand.ExecuteAsync(null);

        Assert.Empty(viewModel.GitChanges);
        Assert.Equal(1, viewModel.GitOtherChangeCount);
        Assert.Equal("No ReqMint file changes · 1 other repository changes", viewModel.GitSummary);
    }

    [Fact]
    public async Task OpeningWorkspace_HandlesFoldersOutsideGitRepositories()
    {
        var viewModel = CreateViewModel(
            new RecordingWorkspaceStore { SnapshotToLoad = CreateSnapshot() },
            CreateWorkspacePath(),
            gitService: new StubGitService());

        await viewModel.OpenWorkspaceCommand.ExecuteAsync(null);

        Assert.Equal("—", viewModel.GitBranch);
        Assert.Equal("Workspace is not inside a Git repository", viewModel.GitSummary);
        Assert.Empty(viewModel.GitChanges);
        Assert.Equal(0, viewModel.GitOtherChangeCount);
    }

    [Fact]
    public async Task CollectionCommands_CreateSelectAndRenameACollection()
    {
        var store = new RecordingWorkspaceStore { SnapshotToLoad = CreateSnapshot() };
        var viewModel = CreateViewModel(store, CreateWorkspacePath());
        await viewModel.OpenWorkspaceCommand.ExecuteAsync(null);

        await viewModel.CreateCollectionCommand.ExecuteAsync(null);

        Assert.Equal(2, store.SavedSnapshot!.Collections.Count);
        Assert.Equal("New collection", viewModel.CollectionDraftName);

        viewModel.CollectionDraftName = "Partner API";
        await viewModel.RenameCollectionCommand.ExecuteAsync(null);

        Assert.Contains(store.SavedSnapshot.Collections, collection => collection.Name == "Partner API");
        Assert.Contains(
            store.SavedSnapshot.Workspace.Collections,
            reference => reference.Name == "Partner API");
    }

    [Fact]
    public async Task CollectionRunner_RequiresReviewAndShowsSafeResultSummary()
    {
        var snapshot = CreateSnapshot();
        var request = snapshot.Collections[0].Requests[0];
        var runner = new StubCollectionRunner
        {
            Result = new CollectionRunResult
            {
                CollectionId = snapshot.Collections[0].Id,
                CollectionName = snapshot.Collections[0].Name,
                Results =
                [
                    new CollectionRequestRunResult
                    {
                        RequestId = request.Id,
                        RequestName = request.Name,
                        State = CollectionRequestRunState.Passed,
                        StatusCode = 201,
                        Duration = TimeSpan.FromMilliseconds(12),
                        Assertions =
                        [
                            new CollectionAssertionResult(
                                RequestAssertionKind.StatusCodeEquals,
                                CollectionAssertionOutcome.Passed),
                        ],
                    },
                ],
            },
        };
        var viewModel = CreateViewModel(
            new RecordingWorkspaceStore { SnapshotToLoad = snapshot },
            CreateWorkspacePath(),
            collectionRunner: runner);
        await viewModel.OpenWorkspaceCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsCollectionRunAvailable);
        await viewModel.OpenCollectionRunnerCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsCollectionRunnerVisible);
        Assert.Equal(0, runner.CallCount);

        await viewModel.StartCollectionRunCommand.ExecuteAsync(null);

        Assert.Equal(1, runner.CallCount);
        Assert.Equal(snapshot.Workspace.Id, runner.Definition!.WorkspaceId);
        var item = Assert.Single(viewModel.CollectionRunResults);
        Assert.Equal("Create order", item.Name);
        Assert.Equal("Passed", item.Status);
        Assert.Equal("HTTP 201", item.Detail);
        Assert.Equal("Status: passed", item.Assertions);
        Assert.Equal("Completed · 1 passed · 0 failed", viewModel.CollectionRunSummary);
    }

    [Fact]
    public async Task CollectionRunner_ExportsTheLatestSanitizedResult()
    {
        var snapshot = CreateSnapshot();
        var runResult = new CollectionRunResult
        {
            CollectionId = snapshot.Collections[0].Id,
            CollectionName = "Commerce/API",
            Results =
            [
                new CollectionRequestRunResult
                {
                    RequestId = snapshot.Collections[0].Requests[0].Id,
                    RequestName = "Create order",
                    State = CollectionRequestRunState.Passed,
                    StatusCode = 201,
                    Duration = TimeSpan.FromMilliseconds(12),
                },
            ],
        };
        var exportService = new RecordingCollectionRunExportService();
        var viewModel = CreateViewModel(
            new RecordingWorkspaceStore { SnapshotToLoad = snapshot },
            CreateWorkspacePath(),
            collectionRunner: new StubCollectionRunner { Result = runResult },
            collectionRunExportService: exportService);
        await viewModel.OpenWorkspaceCommand.ExecuteAsync(null);
        await viewModel.OpenCollectionRunnerCommand.ExecuteAsync(null);
        await viewModel.StartCollectionRunCommand.ExecuteAsync(null);

        await viewModel.ExportCollectionRunJsonCommand.ExecuteAsync(null);

        Assert.True(viewModel.HasCollectionRunResult);
        Assert.Same(runResult, exportService.Result);
        Assert.Equal(CollectionRunExportFormat.Json, exportService.Format);
        Assert.Equal("Commerce-API-run.json", exportService.SuggestedFileName);
        Assert.Equal("JSON run report exported", viewModel.WorkspaceStatus);
    }

    [Fact]
    public async Task CollectionRunner_LoadsBoundedIterationDataForTheNextRun()
    {
        const string sensitiveValue = "iteration-value-must-not-be-shown";
        var snapshot = CreateSnapshot();
        var runner = new StubCollectionRunner
        {
            Result = new CollectionRunResult
            {
                CollectionId = snapshot.Collections[0].Id,
                CollectionName = snapshot.Collections[0].Name,
                IterationCount = 2,
                Results =
                [
                    new CollectionRequestRunResult
                    {
                        RequestId = snapshot.Collections[0].Requests[0].Id,
                        RequestName = "Create order",
                        IterationNumber = 1,
                        State = CollectionRequestRunState.Passed,
                    },
                    new CollectionRequestRunResult
                    {
                        RequestId = snapshot.Collections[0].Requests[0].Id,
                        RequestName = "Create order",
                        IterationNumber = 2,
                        State = CollectionRequestRunState.Passed,
                    },
                ],
            },
        };
        var dataService = new StubCollectionRunDataFileService
        {
            Selection = new CollectionRunDataFile(
                "orders.json",
                new CollectionRunDataSet
                {
                    Rows =
                    [
                        DataRow(("orderId", "A-1")),
                        DataRow(("orderId", sensitiveValue)),
                    ],
                }),
        };
        var viewModel = CreateViewModel(
            new RecordingWorkspaceStore { SnapshotToLoad = snapshot },
            CreateWorkspacePath(),
            collectionRunner: runner,
            collectionRunDataFileService: dataService);
        await viewModel.OpenWorkspaceCommand.ExecuteAsync(null);
        await viewModel.OpenCollectionRunnerCommand.ExecuteAsync(null);

        await viewModel.SelectCollectionRunDataFileCommand.ExecuteAsync(null);
        await viewModel.StartCollectionRunCommand.ExecuteAsync(null);

        Assert.True(viewModel.HasCollectionRunData);
        Assert.Equal("orders.json", viewModel.CollectionRunDataFileName);
        Assert.Equal(2, runner.Definition!.DataRows.Count);
        Assert.Equal("Iteration 1", viewModel.CollectionRunResults[0].Assertions);
        Assert.Equal("Iteration 2", viewModel.CollectionRunResults[1].Assertions);
        Assert.DoesNotContain(
            sensitiveValue,
            string.Join('|', viewModel.CollectionRunResults),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task CollectionRunner_SavesAndReopensSanitizedRunHistory()
    {
        var snapshot = CreateSnapshot();
        var request = snapshot.Collections[0].Requests[0];
        var result = new CollectionRunResult
        {
            CollectionId = snapshot.Collections[0].Id,
            CollectionName = snapshot.Collections[0].Name,
            Results =
            [
                new CollectionRequestRunResult
                {
                    RequestId = request.Id,
                    RequestName = request.Name,
                    State = CollectionRequestRunState.Passed,
                    StatusCode = 201,
                    Duration = TimeSpan.FromMilliseconds(15),
                },
            ],
        };
        var historyStore = new RecordingCollectionRunHistoryStore();
        var settings = new StubAppSettingsService();
        settings.Update(settings.Current with { CollectionRunHistoryRetentionLimit = 75 });
        var viewModel = CreateViewModel(
            new RecordingWorkspaceStore { SnapshotToLoad = snapshot },
            CreateWorkspacePath(),
            collectionRunner: new StubCollectionRunner { Result = result },
            appSettings: settings,
            collectionRunHistoryStore: historyStore);
        await viewModel.OpenWorkspaceCommand.ExecuteAsync(null);
        await viewModel.OpenCollectionRunnerCommand.ExecuteAsync(null);

        await viewModel.StartCollectionRunCommand.ExecuteAsync(null);

        var saved = Assert.Single(historyStore.Entries);
        Assert.Equal(75, historyStore.LastRetentionLimit);
        Assert.Equal(snapshot.Workspace.Id, saved.WorkspaceId);
        Assert.Equal(201, Assert.Single(saved.Requests).StatusCode);
        Assert.Single(viewModel.CollectionRunHistory);

        viewModel.SelectedCollectionRunHistoryItem = viewModel.CollectionRunHistory[0];

        Assert.Equal("Previous run · 1 passed · 0 failed", viewModel.CollectionRunSummary);
        Assert.Equal("HTTP 201", Assert.Single(viewModel.CollectionRunResults).Detail);
    }

    [Fact]
    public async Task CollectionRunner_FiltersResultsAndRerunsOnlyFailedExecutions()
    {
        var snapshot = CreateSnapshot();
        var request = snapshot.Collections[0].Requests[0];
        var runner = new StubCollectionRunner
        {
            Result = new CollectionRunResult
            {
                CollectionId = snapshot.Collections[0].Id,
                CollectionName = snapshot.Collections[0].Name,
                IterationCount = 3,
                Results =
                [
                    new CollectionRequestRunResult
                    {
                        RequestId = request.Id,
                        RequestName = request.Name,
                        IterationNumber = 1,
                        State = CollectionRequestRunState.Passed,
                    },
                    new CollectionRequestRunResult
                    {
                        RequestId = request.Id,
                        RequestName = request.Name,
                        IterationNumber = 2,
                        State = CollectionRequestRunState.Failed,
                    },
                    new CollectionRequestRunResult
                    {
                        RequestId = request.Id,
                        RequestName = request.Name,
                        IterationNumber = 3,
                        State = CollectionRequestRunState.Error,
                        ErrorKind = CollectionRunErrorKind.Timeout,
                    },
                ],
            },
        };
        var viewModel = CreateViewModel(
            new RecordingWorkspaceStore { SnapshotToLoad = snapshot },
            CreateWorkspacePath(),
            collectionRunner: runner);
        await viewModel.OpenWorkspaceCommand.ExecuteAsync(null);
        await viewModel.OpenCollectionRunnerCommand.ExecuteAsync(null);
        await viewModel.StartCollectionRunCommand.ExecuteAsync(null);

        viewModel.ShowFailedCollectionRunResultsCommand.Execute(null);

        Assert.Equal(2, viewModel.CollectionRunResults.Count);
        Assert.All(viewModel.CollectionRunResults, item => Assert.True(item.State is
            CollectionRequestRunState.Failed or CollectionRequestRunState.Error));
        Assert.Equal("Showing 2 of 3", viewModel.CollectionRunResultFilterStatus);
        Assert.True(viewModel.CanRerunFailedCollectionResults);

        await viewModel.RerunFailedCollectionRequestsCommand.ExecuteAsync(null);

        Assert.Equal(2, runner.CallCount);
        Assert.Equal(
            [
                new CollectionRunExecutionKey(request.Id, 2),
                new CollectionRunExecutionKey(request.Id, 3),
            ],
            runner.Definition!.ExecutionSelection);
    }

    [Fact]
    public async Task CollectionRunner_DoesNotRerunHistoricalDataDrivenResultWithoutInputs()
    {
        var snapshot = CreateSnapshot();
        var request = snapshot.Collections[0].Requests[0];
        var entry = CollectionRunHistoryEntry.Create(
            snapshot.Workspace.Id,
            new CollectionRunResult
            {
                CollectionId = snapshot.Collections[0].Id,
                CollectionName = snapshot.Collections[0].Name,
                IterationCount = 2,
                UsedDataFile = true,
                Results =
                [
                    new CollectionRequestRunResult
                    {
                        RequestId = request.Id,
                        RequestName = request.Name,
                        IterationNumber = 2,
                        State = CollectionRequestRunState.Failed,
                    },
                ],
            },
            DateTimeOffset.UtcNow);
        var historyStore = new RecordingCollectionRunHistoryStore([entry]);
        var viewModel = CreateViewModel(
            new RecordingWorkspaceStore { SnapshotToLoad = snapshot },
            CreateWorkspacePath(),
            collectionRunHistoryStore: historyStore);
        await viewModel.OpenWorkspaceCommand.ExecuteAsync(null);
        await viewModel.OpenCollectionRunnerCommand.ExecuteAsync(null);

        viewModel.SelectedCollectionRunHistoryItem = Assert.Single(
            viewModel.CollectionRunHistory);

        Assert.True(viewModel.HasFailedCollectionRunResults);
        Assert.False(viewModel.CanRerunFailedCollectionResults);
        Assert.NotEmpty(viewModel.CollectionRunRerunUnavailableReason);
    }

    [Fact]
    public async Task CollectionRunner_ClearHistoryRequiresConfirmation()
    {
        var snapshot = CreateSnapshot();
        var entry = CollectionRunHistoryEntry.Create(
            snapshot.Workspace.Id,
            new CollectionRunResult
            {
                CollectionId = snapshot.Collections[0].Id,
                CollectionName = snapshot.Collections[0].Name,
                Results =
                [
                    new CollectionRequestRunResult
                    {
                        RequestId = snapshot.Collections[0].Requests[0].Id,
                        RequestName = "Create order",
                        State = CollectionRequestRunState.Passed,
                    },
                ],
            },
            DateTimeOffset.UtcNow);
        var historyStore = new RecordingCollectionRunHistoryStore([entry]);
        var prompt = new StubCollectionRunHistoryClearPrompt { Confirmed = true };
        var viewModel = CreateViewModel(
            new RecordingWorkspaceStore { SnapshotToLoad = snapshot },
            CreateWorkspacePath(),
            collectionRunHistoryStore: historyStore,
            collectionRunHistoryClearPrompt: prompt);
        await viewModel.OpenWorkspaceCommand.ExecuteAsync(null);
        await viewModel.OpenCollectionRunnerCommand.ExecuteAsync(null);

        await viewModel.ClearCollectionRunHistoryCommand.ExecuteAsync(null);

        Assert.Equal(1, prompt.CallCount);
        Assert.Empty(historyStore.Entries);
        Assert.Empty(viewModel.CollectionRunHistory);
        Assert.Equal("Run history cleared", viewModel.CollectionRunHistoryStatus);
    }

    [Fact]
    public async Task NewRequestCommand_KeepsUnsavedWorkInItsOwnTabInsteadOfPrompting()
    {
        var store = new RecordingWorkspaceStore { SnapshotToLoad = CreateSnapshot() };
        var prompt = new StubUnsavedChangesPrompt { Choice = UnsavedChangesChoice.Cancel };
        var viewModel = CreateViewModel(store, CreateWorkspacePath(), prompt: prompt);
        await viewModel.OpenWorkspaceCommand.ExecuteAsync(null);
        await viewModel.Collections[0].Requests[0].OpenCommand.ExecuteAsync(null);
        viewModel.Url = "https://api.example.com/changed";
        var edited = viewModel.ActiveTab!;

        await viewModel.NewRequestCommand.ExecuteAsync(null);

        // The new tab is empty, nothing was discarded and nothing was asked.
        Assert.Equal(string.Empty, viewModel.Url);
        Assert.Equal(0, prompt.CallCount);
        Assert.Equal(2, viewModel.Tabs.Count);
        Assert.NotSame(edited, viewModel.ActiveTab);

        await edited.SelectCommand.ExecuteAsync(null);

        Assert.Equal("https://api.example.com/changed", viewModel.Url);
    }

    [Theory]
    [InlineData(UnsavedChangesChoice.Cancel, 2)]
    [InlineData(UnsavedChangesChoice.Discard, 1)]
    public async Task ClosingADirtyTab_AsksBeforeLosingTheChanges(
        UnsavedChangesChoice choice,
        int expectedTabCount)
    {
        var store = new RecordingWorkspaceStore { SnapshotToLoad = CreateSnapshot() };
        var prompt = new StubUnsavedChangesPrompt { Choice = choice };
        var viewModel = CreateViewModel(store, CreateWorkspacePath(), prompt: prompt);
        await viewModel.OpenWorkspaceCommand.ExecuteAsync(null);
        await viewModel.Collections[0].Requests[0].OpenCommand.ExecuteAsync(null);
        await viewModel.NewTabCommand.ExecuteAsync(null);
        viewModel.Url = "https://api.example.com/scratch";
        var scratch = viewModel.ActiveTab!;

        await scratch.CloseCommand.ExecuteAsync(null);

        Assert.Equal(1, prompt.CallCount);
        Assert.Equal(expectedTabCount, viewModel.Tabs.Count);
    }

    [Fact]
    public async Task ClosingADirtyTab_CanSaveItFirst()
    {
        var store = new RecordingWorkspaceStore { SnapshotToLoad = CreateSnapshot() };
        var prompt = new StubUnsavedChangesPrompt { Choice = UnsavedChangesChoice.Save };
        var viewModel = CreateViewModel(store, CreateWorkspacePath(), prompt: prompt);
        await viewModel.OpenWorkspaceCommand.ExecuteAsync(null);
        await viewModel.Collections[0].Requests[0].OpenCommand.ExecuteAsync(null);
        viewModel.Url = "https://api.example.com/changed";

        await viewModel.ActiveTab!.CloseCommand.ExecuteAsync(null);

        Assert.Equal(
            "https://api.example.com/changed",
            store.SavedSnapshot!.Collections[0].Requests[0].Url);
    }

    [Fact]
    public async Task AboutLinks_OpenExpectedTrustedPages()
    {
        var links = new RecordingExternalLinkService();
        var viewModel = CreateViewModel(
            new RecordingWorkspaceStore(),
            CreateWorkspacePath(),
            externalLinkService: links);

        await viewModel.OpenDocumentationCommand.ExecuteAsync(null);
        await viewModel.OpenPrivacyCommand.ExecuteAsync(null);
        await viewModel.OpenSecurityCommand.ExecuteAsync(null);
        await viewModel.OpenSupportCommand.ExecuteAsync(null);

        Assert.Equal(
            ["/docs", "/privacy", "/security", "/support"],
            links.OpenedUris.Select(uri => uri.AbsolutePath));
        Assert.All(links.OpenedUris, uri =>
        {
            Assert.Equal(Uri.UriSchemeHttps, uri.Scheme);
            Assert.Equal("reqmintapp.github.io", uri.Host);
        });
        Assert.Equal("Opened in your browser", viewModel.WorkspaceStatus);
    }

    [Fact]
    public async Task CopySupportInformation_CopiesOnlyReleaseAndPlatformDetails()
    {
        var clipboard = new RecordingClipboardService();
        var viewModel = CreateViewModel(
            new RecordingWorkspaceStore(),
            CreateWorkspacePath(),
            clipboardService: clipboard);

        await viewModel.CopySupportInformationCommand.ExecuteAsync(null);

        Assert.Equal(viewModel.SupportInformation, clipboard.Text);
        Assert.DoesNotContain("api.example.com", clipboard.Text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("workspace.json", clipboard.Text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("authorization", clipboard.Text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", clipboard.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Support information copied", viewModel.WorkspaceStatus);
    }

    [Fact]
    public async Task SaveRequestCommand_KeepsTheEnvironmentTheUserSelected()
    {
        var snapshot = CreateSnapshotWithEnvironments();
        var store = new RecordingWorkspaceStore { SnapshotToLoad = snapshot };
        var viewModel = CreateViewModel(store, CreateWorkspacePath());
        await viewModel.OpenWorkspaceCommand.ExecuteAsync(null);
        await viewModel.Collections[0].Requests[0].OpenCommand.ExecuteAsync(null);

        viewModel.EnvironmentName = "Staging";
        await viewModel.SaveRequestCommand.ExecuteAsync(null);

        Assert.Equal("Staging", viewModel.EnvironmentName);
    }

    [Fact]
    public async Task CreateCollectionCommand_KeepsTheEnvironmentTheUserSelected()
    {
        var snapshot = CreateSnapshotWithEnvironments();
        var store = new RecordingWorkspaceStore { SnapshotToLoad = snapshot };
        var viewModel = CreateViewModel(store, CreateWorkspacePath());
        await viewModel.OpenWorkspaceCommand.ExecuteAsync(null);

        viewModel.EnvironmentName = "Staging";
        await viewModel.CreateCollectionCommand.ExecuteAsync(null);

        Assert.Equal("Staging", viewModel.EnvironmentName);
    }

    [Fact]
    public async Task SaveRequestCommand_KeepsDisabledFieldsInsteadOfDroppingThem()
    {
        var snapshot = CreateSnapshot();
        var store = new RecordingWorkspaceStore { SnapshotToLoad = snapshot };
        var viewModel = CreateViewModel(store, CreateWorkspacePath());
        await viewModel.OpenWorkspaceCommand.ExecuteAsync(null);
        await viewModel.Collections[0].Requests[0].OpenCommand.ExecuteAsync(null);
        viewModel.QueryParameters.Add(new RequestFieldViewModel("locale", "en-US")
        {
            IsEnabled = false,
        });

        await viewModel.SaveRequestCommand.ExecuteAsync(null);

        var saved = Assert.Single(store.SavedSnapshot!.Collections[0].Requests);
        var parameter = Assert.Single(saved.QueryParameters);
        Assert.Equal("locale", parameter.Name);
        Assert.False(parameter.IsEnabled);
    }

    [Fact]
    public async Task OpeningSavedRequest_RestoresTheDisabledState()
    {
        var snapshot = CreateSnapshot();
        var request = snapshot.Collections[0].Requests[0] with
        {
            QueryParameters = [new RequestField("locale", "en-US", IsEnabled: false)],
        };
        var collection = snapshot.Collections[0] with { Requests = [request] };
        var store = new RecordingWorkspaceStore
        {
            SnapshotToLoad = snapshot with { Collections = [collection] },
        };
        var viewModel = CreateViewModel(store, CreateWorkspacePath());

        await viewModel.OpenWorkspaceCommand.ExecuteAsync(null);
        await viewModel.Collections[0].Requests[0].OpenCommand.ExecuteAsync(null);

        var parameter = Assert.Single(viewModel.QueryParameters);
        Assert.Equal("locale", parameter.Name);
        Assert.False(parameter.IsEnabled);
    }

    [Fact]
    public async Task OpenWorkspaceCommand_RemembersTheWorkspaceForTheNextLaunch()
    {
        var snapshot = CreateSnapshotWithEnvironments();
        var store = new RecordingWorkspaceStore { SnapshotToLoad = snapshot };
        var settings = new StubAppSettingsService();
        var directory = CreateWorkspacePath();
        var viewModel = CreateViewModel(store, directory, appSettings: settings);

        await viewModel.OpenWorkspaceCommand.ExecuteAsync(null);
        viewModel.EnvironmentName = "Staging";

        Assert.Equal(directory, settings.Current.LastWorkspaceDirectory);
        Assert.Equal(snapshot.Environments[1].Id, settings.Current.LastEnvironmentId);
    }

    [Fact]
    public async Task RestoreLastWorkspaceAsync_ReopensTheWorkspaceAndItsActiveEnvironment()
    {
        var snapshot = CreateSnapshotWithEnvironments();
        var store = new RecordingWorkspaceStore { SnapshotToLoad = snapshot };
        var directory = CreateWorkspacePath();
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(
            Path.Combine(directory, "reqmint.workspace.json"),
            "{}");
        var settings = new StubAppSettingsService(new AppSettings
        {
            LastWorkspaceDirectory = directory,
            LastEnvironmentId = snapshot.Environments[1].Id,
        });
        var viewModel = CreateViewModel(store, CreateWorkspacePath(), appSettings: settings);

        try
        {
            await viewModel.RestoreLastWorkspaceAsync();

            Assert.Equal(snapshot.Workspace.Name, viewModel.WorkspaceName);
            Assert.Equal("Staging", viewModel.EnvironmentName);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task RestoreLastWorkspaceAsync_IgnoresAWorkspaceThatIsNoLongerOnDisk()
    {
        var store = new RecordingWorkspaceStore();
        var settings = new StubAppSettingsService(new AppSettings
        {
            LastWorkspaceDirectory = CreateWorkspacePath(),
        });
        var viewModel = CreateViewModel(store, CreateWorkspacePath(), appSettings: settings);

        await viewModel.RestoreLastWorkspaceAsync();

        Assert.False(viewModel.SaveRequestCommand.CanExecute(null));
    }

    [Fact]
    public async Task ASuccessfulOperation_ClearsThePreviousWorkspaceError()
    {
        var snapshot = CreateSnapshot();
        var store = new RecordingWorkspaceStore { SnapshotToLoad = snapshot };
        var viewModel = CreateViewModel(store, CreateWorkspacePath());
        await viewModel.OpenWorkspaceCommand.ExecuteAsync(null);

        viewModel.CollectionDraftName = "   ";
        await viewModel.RenameCollectionCommand.ExecuteAsync(null);
        Assert.True(viewModel.HasResponse);
        Assert.Equal("Could not rename collection", viewModel.ResponseStatus);

        await viewModel.CreateCollectionCommand.ExecuteAsync(null);

        Assert.False(viewModel.HasResponse);
        Assert.Equal("Ready", viewModel.ResponseStatus);
        Assert.Equal("Collection created", viewModel.WorkspaceStatus);
    }

    [Fact]
    public void RemoveFieldCommands_RemoveOnlyTheSelectedRow()
    {
        var viewModel = CreateViewModel(new RecordingWorkspaceStore(), CreateWorkspacePath());
        var header = viewModel.Headers[0];
        var parameter = viewModel.QueryParameters[0];
        var headerCount = viewModel.Headers.Count;
        var parameterCount = viewModel.QueryParameters.Count;

        viewModel.RemoveHeaderCommand.Execute(header);
        viewModel.RemoveQueryParameterCommand.Execute(parameter);

        Assert.Equal(headerCount - 1, viewModel.Headers.Count);
        Assert.Equal(parameterCount - 1, viewModel.QueryParameters.Count);
        Assert.DoesNotContain(header, viewModel.Headers);
        Assert.DoesNotContain(parameter, viewModel.QueryParameters);
    }

    [Fact]
    public async Task RemoveEnvironmentVariableCommand_RemovesOnlyTheSelectedVariable()
    {
        var snapshot = CreateSnapshotWithEnvironments();
        var store = new RecordingWorkspaceStore { SnapshotToLoad = snapshot };
        var viewModel = CreateViewModel(store, CreateWorkspacePath());
        await viewModel.OpenWorkspaceCommand.ExecuteAsync(null);
        viewModel.AddEnvironmentVariableCommand.Execute(null);
        var variable = viewModel.EnvironmentVariables[^1];
        var count = viewModel.EnvironmentVariables.Count;

        viewModel.RemoveEnvironmentVariableCommand.Execute(variable);

        Assert.Equal(count - 1, viewModel.EnvironmentVariables.Count);
        Assert.DoesNotContain(variable, viewModel.EnvironmentVariables);
    }

    [Fact]
    public async Task CloseRequestCommand_ResetsTheComposer()
    {
        var snapshot = CreateSnapshot();
        var store = new RecordingWorkspaceStore { SnapshotToLoad = snapshot };
        var viewModel = CreateViewModel(store, CreateWorkspacePath());
        await viewModel.OpenWorkspaceCommand.ExecuteAsync(null);
        await viewModel.Collections[0].Requests[0].OpenCommand.ExecuteAsync(null);

        await viewModel.CloseRequestCommand.ExecuteAsync(null);

        Assert.Equal("New request", viewModel.RequestName);
        Assert.Equal("GET", viewModel.SelectedMethod);
        Assert.Equal(string.Empty, viewModel.Url);
        Assert.Equal("Request closed", viewModel.WorkspaceStatus);
    }

    [Fact]
    public async Task CopyResponseCommand_PutsTheResponseBodyOnTheClipboard()
    {
        var clipboard = new RecordingClipboardService();
        var viewModel = CreateViewModel(
            new RecordingWorkspaceStore(),
            CreateWorkspacePath(),
            clipboardService: clipboard);
        viewModel.ResponseBody = "{\"status\":\"ok\"}";

        await viewModel.CopyResponseCommand.ExecuteAsync(null);

        Assert.Equal("{\"status\":\"ok\"}", clipboard.Text);
        Assert.Equal("Response copied to the clipboard", viewModel.WorkspaceStatus);
    }

    private static WorkspaceSnapshot CreateSnapshotWithEnvironments()
    {
        var snapshot = CreateSnapshot();
        var production = new EnvironmentDocument
        {
            Id = Guid.Parse("44444444-4444-4444-4444-444444444444"),
            Name = "Production",
            Variables = [new EnvironmentVariable("BASE_URL", "https://api.example.com")],
        };
        var staging = new EnvironmentDocument
        {
            Id = Guid.Parse("55555555-5555-5555-5555-555555555555"),
            Name = "Staging",
            Variables = [new EnvironmentVariable("BASE_URL", "https://staging.example.com")],
        };

        return snapshot with
        {
            Workspace = snapshot.Workspace with
            {
                Environments =
                [
                    new WorkspaceFileReference(
                        production.Id,
                        production.Name,
                        "environments/production.json"),
                    new WorkspaceFileReference(
                        staging.Id,
                        staging.Name,
                        "environments/staging.json"),
                ],
            },
            Environments = [production, staging],
        };
    }

    [Fact]
    public async Task SendCommand_DescribesMissingEnvironmentValuesInsteadOfLeakingTheCoreMessage()
    {
        var viewModel = CreateViewModel(new RecordingWorkspaceStore(), CreateWorkspacePath());
        viewModel.Url = "{{BASE_URL}}/orders/{{ORDER_ID}}";

        await viewModel.SendCommand.ExecuteAsync(null);

        Assert.Equal("Missing variables", viewModel.ResponseStatus);
        Assert.Equal(
            "Missing environment values: BASE_URL, ORDER_ID.",
            viewModel.ResponseBody);
    }

    [Fact]
    public async Task SendCommand_DescribesATimeoutWithTheConfiguredNumberOfSeconds()
    {
        var viewModel = CreateViewModel(
            new RecordingWorkspaceStore(),
            CreateWorkspacePath(),
            new TimingOutRequestExecutor());
        viewModel.Url = "https://api.example.com/orders";
        viewModel.TimeoutSeconds = 5;

        await viewModel.SendCommand.ExecuteAsync(null);

        Assert.Equal("Timed out", viewModel.ResponseStatus);
        Assert.Equal("The request exceeded the 5 second timeout.", viewModel.ResponseBody);
    }

    [Fact]
    public async Task SendCommand_KeepsTheTransportDetailUnderALocalizableConnectionMessage()
    {
        var viewModel = CreateViewModel(
            new RecordingWorkspaceStore(),
            CreateWorkspacePath(),
            new FailingRequestExecutor());
        viewModel.Url = "https://api.example.com/orders";

        await viewModel.SendCommand.ExecuteAsync(null);

        Assert.Equal("Connection failed", viewModel.ResponseStatus);
        Assert.StartsWith(
            "The request could not be sent. Check the address and your connection.",
            viewModel.ResponseBody,
            StringComparison.Ordinal);
        Assert.Contains("No such host is known.", viewModel.ResponseBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SaveEnvironmentCommand_ReportsADuplicateNameWithALocalizableMessage()
    {
        var snapshot = CreateSnapshotWithEnvironments();
        var store = new RecordingWorkspaceStore { SnapshotToLoad = snapshot };
        var viewModel = CreateViewModel(store, CreateWorkspacePath());
        await viewModel.OpenWorkspaceCommand.ExecuteAsync(null);

        viewModel.EnvironmentDraftName = "Staging";
        await viewModel.SaveEnvironmentCommand.ExecuteAsync(null);

        Assert.Equal("Could not save environment", viewModel.ResponseStatus);
        Assert.Equal("Environment 'Staging' already exists.", viewModel.ResponseBody);
    }

    private sealed class TimingOutRequestExecutor : IRequestExecutor
    {
        public Task<ApiResponse> ExecuteAsync(
            ApiRequest request,
            CancellationToken cancellationToken = default) =>
            throw new TimeoutException("The request exceeded the 5 second timeout.");
    }

    private sealed class FailingRequestExecutor : IRequestExecutor
    {
        public Task<ApiResponse> ExecuteAsync(
            ApiRequest request,
            CancellationToken cancellationToken = default) =>
            throw new HttpRequestException("No such host is known.");
    }

    [Fact]
    public void CommandPalette_StaysClosedUntilThereIsAQuery()
    {
        var viewModel = CreateViewModel(new RecordingWorkspaceStore(), CreateWorkspacePath());

        Assert.False(viewModel.IsCommandPaletteOpen);
        Assert.Empty(viewModel.CommandPaletteResults);

        viewModel.CommandPaletteQuery = "the";

        Assert.True(viewModel.IsCommandPaletteOpen);
        Assert.NotEmpty(viewModel.CommandPaletteResults);
    }

    [Fact]
    public void CommandPalette_FindsThemesWithoutTurkishDiacritics()
    {
        var viewModel = CreateViewModel(new RecordingWorkspaceStore(), CreateWorkspacePath());

        viewModel.CommandPaletteQuery = "theme";

        Assert.NotEmpty(viewModel.CommandPaletteResults);
        Assert.All(
            viewModel.CommandPaletteResults,
            item => Assert.StartsWith("Theme:", item.Title, StringComparison.Ordinal));
    }

    [Fact]
    public void CommandPalette_MatchesEveryQueryPartInAnyOrder()
    {
        var viewModel = CreateViewModel(new RecordingWorkspaceStore(), CreateWorkspacePath());

        viewModel.CommandPaletteQuery = "workspace open";

        var titles = viewModel.CommandPaletteResults.Select(item => item.Title).ToArray();
        Assert.Contains("Open a local ReqMint workspace", titles);
        Assert.DoesNotContain("Send", titles);
    }

    [Fact]
    public void CommandPalette_ReportsWhenNothingMatches()
    {
        var viewModel = CreateViewModel(new RecordingWorkspaceStore(), CreateWorkspacePath());

        viewModel.CommandPaletteQuery = "zzzzzz";

        Assert.Empty(viewModel.CommandPaletteResults);
        Assert.False(viewModel.HasCommandPaletteResults);
        Assert.Equal("No matching command", viewModel.CommandPaletteEmptyMessage);
    }

    [Fact]
    public void CommandPalette_KeepsTheSelectionInsideTheResultsAndWrapsAround()
    {
        var viewModel = CreateViewModel(new RecordingWorkspaceStore(), CreateWorkspacePath());
        viewModel.CommandPaletteQuery = "theme";
        var count = viewModel.CommandPaletteResults.Count;
        Assert.True(count > 1);

        Assert.True(viewModel.CommandPaletteResults[0].IsSelected);

        viewModel.MoveCommandPaletteSelectionDownCommand.Execute(null);
        Assert.True(viewModel.CommandPaletteResults[1].IsSelected);
        Assert.False(viewModel.CommandPaletteResults[0].IsSelected);

        viewModel.MoveCommandPaletteSelectionUpCommand.Execute(null);
        viewModel.MoveCommandPaletteSelectionUpCommand.Execute(null);
        Assert.True(viewModel.CommandPaletteResults[count - 1].IsSelected);
    }

    [Fact]
    public async Task CommandPalette_RunsTheSelectedEntryAndCloses()
    {
        var appSettings = new StubAppSettingsService();
        var viewModel = CreateViewModel(
            new RecordingWorkspaceStore(),
            CreateWorkspacePath(),
            appSettings: appSettings);
        var target = viewModel.Themes.Themes.Last();
        viewModel.CommandPaletteQuery = target.DisplayName;
        var entry = viewModel.CommandPaletteResults.First(
            item => item.Title == $"Theme: {target.DisplayName}");
        while (!entry.IsSelected)
        {
            viewModel.MoveCommandPaletteSelectionDownCommand.Execute(null);
        }

        await viewModel.RunSelectedCommandPaletteItemCommand.ExecuteAsync(null);

        Assert.Equal(target, viewModel.Themes.SelectedTheme);
        Assert.False(viewModel.IsCommandPaletteOpen);
        Assert.Equal(string.Empty, viewModel.CommandPaletteQuery);
    }

    [Fact]
    public async Task CommandPalette_OpensSavedRequestsByName()
    {
        var snapshot = CreateSnapshot();
        var store = new RecordingWorkspaceStore { SnapshotToLoad = snapshot };
        var viewModel = CreateViewModel(store, CreateWorkspacePath());
        await viewModel.OpenWorkspaceCommand.ExecuteAsync(null);

        viewModel.CommandPaletteQuery = "create order";
        var entry = Assert.Single(
            viewModel.CommandPaletteResults,
            item => item.Title == "Create order");
        await viewModel.RunCommandPaletteItemCommand.ExecuteAsync(entry);

        Assert.Equal("Create order", viewModel.RequestName);
        Assert.Equal("POST", viewModel.SelectedMethod);
        Assert.False(viewModel.IsCommandPaletteOpen);
    }

    [Fact]
    public void CommandPalette_EscapeClearsTheQueryAndCloses()
    {
        var viewModel = CreateViewModel(new RecordingWorkspaceStore(), CreateWorkspacePath());
        viewModel.CommandPaletteQuery = "theme";

        viewModel.CloseCommandPaletteCommand.Execute(null);

        Assert.False(viewModel.IsCommandPaletteOpen);
        Assert.Equal(string.Empty, viewModel.CommandPaletteQuery);
        Assert.Empty(viewModel.CommandPaletteResults);
    }

    [Fact]
    public void CommandPalette_OpenCommandListsEntriesWithoutTyping()
    {
        var viewModel = CreateViewModel(new RecordingWorkspaceStore(), CreateWorkspacePath());

        viewModel.OpenCommandPaletteCommand.Execute(null);

        Assert.True(viewModel.IsCommandPaletteOpen);
        Assert.NotEmpty(viewModel.CommandPaletteResults);
    }

    [Theory]
    [InlineData("Sıfırla", "sifirla")]
    [InlineData("Çevir", "cevir")]
    [InlineData("İstek", "istek")]
    [InlineData("API", "api")]
    [InlineData("Gövde", "govde")]
    public void CommandPaletteSearch_FoldsTurkishCharactersWithoutBreakingAscii(
        string value,
        string expected)
    {
        Assert.Equal(expected, CommandPaletteSearch.Fold(value));
    }

    [Fact]
    public async Task RequestFilter_NarrowsTheTreeAndRestoresItWhenCleared()
    {
        var store = new RecordingWorkspaceStore { SnapshotToLoad = CreateSnapshotWithTwoRequests() };
        var viewModel = CreateViewModel(store, CreateWorkspacePath());
        await viewModel.OpenWorkspaceCommand.ExecuteAsync(null);
        Assert.Equal(2, viewModel.Collections[0].Requests.Count);

        viewModel.RequestFilterText = "create";

        var request = Assert.Single(Assert.Single(viewModel.Collections).Requests);
        Assert.Equal("Create order", request.Name);
        Assert.True(viewModel.IsRequestFilterActive);

        viewModel.ClearRequestFilterCommand.Execute(null);

        Assert.Equal(2, viewModel.Collections[0].Requests.Count);
        Assert.False(viewModel.IsRequestFilterActive);
    }

    [Fact]
    public async Task RequestFilter_MatchesTheMethodAndTheUrlToo()
    {
        var store = new RecordingWorkspaceStore { SnapshotToLoad = CreateSnapshotWithTwoRequests() };
        var viewModel = CreateViewModel(store, CreateWorkspacePath());
        await viewModel.OpenWorkspaceCommand.ExecuteAsync(null);

        viewModel.RequestFilterText = "delete";
        Assert.Equal("Remove order", Assert.Single(viewModel.Collections[0].Requests).Name);

        viewModel.RequestFilterText = "orders/42";
        Assert.Equal("Remove order", Assert.Single(viewModel.Collections[0].Requests).Name);
    }

    [Fact]
    public async Task RequestFilter_HidesEverythingWhenNothingMatches()
    {
        var store = new RecordingWorkspaceStore { SnapshotToLoad = CreateSnapshotWithTwoRequests() };
        var viewModel = CreateViewModel(store, CreateWorkspacePath());
        await viewModel.OpenWorkspaceCommand.ExecuteAsync(null);

        viewModel.RequestFilterText = "zzzzzz";

        Assert.Empty(viewModel.Collections);
        Assert.True(viewModel.IsCollectionListEmpty);
    }

    [Fact]
    public async Task DuplicateRequest_AddsACopyUnderAFreeNameWithoutTouchingTheOriginal()
    {
        var store = new RecordingWorkspaceStore { SnapshotToLoad = CreateSnapshot() };
        var viewModel = CreateViewModel(store, CreateWorkspacePath());
        await viewModel.OpenWorkspaceCommand.ExecuteAsync(null);

        await viewModel.Collections[0].Requests[0].DuplicateCommand.ExecuteAsync(null);

        var saved = store.SavedSnapshot!.Collections[0].Requests;
        Assert.Equal(2, saved.Count);
        Assert.Equal("Create order", saved[0].Name);
        Assert.Equal("Create order (2)", saved[1].Name);
        Assert.NotEqual(saved[0].Id, saved[1].Id);
        Assert.Equal(saved[0].Body?.Content, saved[1].Body?.Content);
        Assert.Equal("Duplicated Create order (2)", viewModel.WorkspaceStatus);
    }

    [Fact]
    public async Task DeleteRequest_AsksFirstAndKeepsTheRequestWhenTheUserCancels()
    {
        var store = new RecordingWorkspaceStore { SnapshotToLoad = CreateSnapshot() };
        var prompt = new StubRequestDeletePrompt(confirm: false);
        var viewModel = CreateViewModel(
            store,
            CreateWorkspacePath(),
            requestDeletePrompt: prompt);
        await viewModel.OpenWorkspaceCommand.ExecuteAsync(null);
        store.ForgetLastSave();

        await viewModel.Collections[0].Requests[0].DeleteCommand.ExecuteAsync(null);

        Assert.Equal(1, prompt.CallCount);
        Assert.Equal("Create order", prompt.LastRequestName);
        Assert.Null(store.SavedSnapshot);
        Assert.Single(viewModel.Collections[0].Requests);
    }

    [Fact]
    public async Task DeleteRequest_RemovesItAndResetsTheComposerWhenItWasOpen()
    {
        var store = new RecordingWorkspaceStore { SnapshotToLoad = CreateSnapshot() };
        var viewModel = CreateViewModel(store, CreateWorkspacePath());
        await viewModel.OpenWorkspaceCommand.ExecuteAsync(null);
        await viewModel.Collections[0].Requests[0].OpenCommand.ExecuteAsync(null);
        Assert.Equal("Create order", viewModel.RequestName);

        await viewModel.Collections[0].Requests[0].DeleteCommand.ExecuteAsync(null);

        Assert.Empty(store.SavedSnapshot!.Collections[0].Requests);
        Assert.Empty(viewModel.Collections[0].Requests);
        Assert.Equal("New request", viewModel.RequestName);
        Assert.Equal("Deleted Create order", viewModel.WorkspaceStatus);
    }

    [Theory]
    [InlineData(204, ResponseStatusKind.Success)]
    [InlineData(302, ResponseStatusKind.Redirect)]
    [InlineData(404, ResponseStatusKind.ClientError)]
    [InlineData(503, ResponseStatusKind.Failure)]
    public void ResponseStatusKind_FollowsTheStatusCodeFamily(int statusCode, ResponseStatusKind expected)
    {
        Assert.Equal(expected, ResponseStatusKinds.FromStatusCode(statusCode));
    }

    [Fact]
    public async Task ResponseStatus_IsMarkedAsAFailureWhenTheRequestTimesOut()
    {
        var viewModel = CreateViewModel(
            new RecordingWorkspaceStore(),
            CreateWorkspacePath(),
            new TimingOutRequestExecutor());
        viewModel.Url = "https://api.example.com/orders";

        await viewModel.SendCommand.ExecuteAsync(null);

        Assert.Equal(ResponseStatusKind.Failure, viewModel.ResponseStatusKind);
        Assert.True(viewModel.IsResponseFailure);
        Assert.False(viewModel.IsResponseSuccess);
    }

    [Fact]
    public void MethodStyle_FlagsOnlyTheMatchingMethod()
    {
        var viewModel = CreateViewModel(new RecordingWorkspaceStore(), CreateWorkspacePath());

        viewModel.SelectedMethod = "DELETE";

        Assert.True(viewModel.IsDeleteMethod);
        Assert.False(viewModel.IsGetMethod);
        Assert.False(viewModel.IsPostMethod);

        viewModel.SelectedMethod = "post";

        Assert.True(viewModel.IsPostMethod);
        Assert.False(viewModel.IsDeleteMethod);
    }

    private static WorkspaceSnapshot CreateSnapshotWithTwoRequests()
    {
        var snapshot = CreateSnapshot();
        var collection = snapshot.Collections[0];
        var remove = new RequestDocument
        {
            Id = Guid.Parse("66666666-6666-6666-6666-666666666666"),
            Name = "Remove order",
            Method = "DELETE",
            Url = "https://api.example.com/orders/42",
        };

        return snapshot with
        {
            Collections = [collection with { Requests = [.. collection.Requests, remove] }],
        };
    }

    [Fact]
    public void Tabs_StartWithASingleEmptyTab()
    {
        var viewModel = CreateViewModel(new RecordingWorkspaceStore(), CreateWorkspacePath());

        var tab = Assert.Single(viewModel.Tabs);
        Assert.Same(tab, viewModel.ActiveTab);
        Assert.True(tab.IsSelected);
        Assert.False(viewModel.HasMultipleTabs);
    }

    [Fact]
    public async Task Tabs_KeepEachRequestsEditorStateSeparate()
    {
        var store = new RecordingWorkspaceStore { SnapshotToLoad = CreateSnapshotWithTwoRequests() };
        var viewModel = CreateViewModel(store, CreateWorkspacePath());
        await viewModel.OpenWorkspaceCommand.ExecuteAsync(null);

        await viewModel.Collections[0].Requests[0].OpenCommand.ExecuteAsync(null);
        var first = viewModel.ActiveTab!;
        viewModel.Url = "https://api.example.com/first";
        viewModel.Headers.Add(new RequestFieldViewModel("X-First", "1"));
        viewModel.SelectedAuthenticationTypeIndex = 1;
        viewModel.AuthenticationBearerToken = "{{FIRST_TOKEN}}";

        await viewModel.Collections[0].Requests[1].OpenCommand.ExecuteAsync(null);
        var second = viewModel.ActiveTab!;

        Assert.NotSame(first, second);
        Assert.Equal(2, viewModel.Tabs.Count);
        Assert.Equal("https://api.example.com/orders/42", viewModel.Url);
        Assert.DoesNotContain(viewModel.Headers, header => header.Name == "X-First");
        Assert.Equal(0, viewModel.SelectedAuthenticationTypeIndex);

        await first.SelectCommand.ExecuteAsync(null);

        Assert.Equal("https://api.example.com/first", viewModel.Url);
        Assert.Contains(viewModel.Headers, header => header.Name == "X-First");
        Assert.Equal(1, viewModel.SelectedAuthenticationTypeIndex);
        Assert.Equal("{{FIRST_TOKEN}}", viewModel.AuthenticationBearerToken);
    }

    [Fact]
    public async Task Tabs_ReuseTheTabThatAlreadyHoldsARequest()
    {
        var store = new RecordingWorkspaceStore { SnapshotToLoad = CreateSnapshotWithTwoRequests() };
        var viewModel = CreateViewModel(store, CreateWorkspacePath());
        await viewModel.OpenWorkspaceCommand.ExecuteAsync(null);

        await viewModel.Collections[0].Requests[0].OpenCommand.ExecuteAsync(null);
        await viewModel.Collections[0].Requests[1].OpenCommand.ExecuteAsync(null);
        await viewModel.Collections[0].Requests[0].OpenCommand.ExecuteAsync(null);

        Assert.Equal(2, viewModel.Tabs.Count);
        Assert.Equal("Create order", viewModel.ActiveTab!.Title);
    }

    [Fact]
    public async Task Tabs_ReuseAnUntouchedScratchTabInsteadOfPilingUp()
    {
        var store = new RecordingWorkspaceStore { SnapshotToLoad = CreateSnapshot() };
        var viewModel = CreateViewModel(store, CreateWorkspacePath());
        await viewModel.OpenWorkspaceCommand.ExecuteAsync(null);

        await viewModel.Collections[0].Requests[0].OpenCommand.ExecuteAsync(null);

        Assert.Single(viewModel.Tabs);
        Assert.Equal("Create order", viewModel.ActiveTab!.Title);
    }

    [Fact]
    public async Task Tabs_ShowTheTitleMethodAndUnsavedMarkerOfTheirRequest()
    {
        var store = new RecordingWorkspaceStore { SnapshotToLoad = CreateSnapshot() };
        var viewModel = CreateViewModel(store, CreateWorkspacePath());
        await viewModel.OpenWorkspaceCommand.ExecuteAsync(null);
        await viewModel.Collections[0].Requests[0].OpenCommand.ExecuteAsync(null);
        var tab = viewModel.ActiveTab!;

        Assert.Equal("Create order", tab.Title);
        Assert.Equal("POST", tab.Method);
        Assert.True(tab.IsPostMethod);
        Assert.False(tab.HasUnsavedChanges);

        viewModel.Url = "https://api.example.com/changed";
        Assert.True(tab.HasUnsavedChanges);

        await viewModel.SaveRequestCommand.ExecuteAsync(null);
        Assert.False(tab.HasUnsavedChanges);

        viewModel.RequestName = "Renamed";
        Assert.Equal("Renamed", tab.Title);
    }

    [Fact]
    public async Task Tabs_TellApartTwoRequestsThatShareAName()
    {
        var snapshot = CreateSnapshot();
        var twin = snapshot.Collections[0].Requests[0] with
        {
            Id = Guid.Parse("77777777-7777-7777-7777-777777777777"),
        };
        var collection = snapshot.Collections[0] with
        {
            Requests = [.. snapshot.Collections[0].Requests, twin],
        };
        var store = new RecordingWorkspaceStore
        {
            SnapshotToLoad = snapshot with { Collections = [collection] },
        };
        var viewModel = CreateViewModel(store, CreateWorkspacePath());
        await viewModel.OpenWorkspaceCommand.ExecuteAsync(null);

        await viewModel.Collections[0].Requests[0].OpenCommand.ExecuteAsync(null);
        await viewModel.Collections[0].Requests[1].OpenCommand.ExecuteAsync(null);

        Assert.Equal(2, viewModel.Tabs.Count);
        Assert.All(viewModel.Tabs, tab => Assert.True(tab.HasSubtitle));
        Assert.NotEqual(viewModel.Tabs[0].Subtitle, viewModel.Tabs[1].Subtitle);
    }

    [Fact]
    public async Task Tabs_ClosingTheLastOneLeavesAFreshEmptyTab()
    {
        var store = new RecordingWorkspaceStore { SnapshotToLoad = CreateSnapshot() };
        var viewModel = CreateViewModel(store, CreateWorkspacePath());
        await viewModel.OpenWorkspaceCommand.ExecuteAsync(null);
        await viewModel.Collections[0].Requests[0].OpenCommand.ExecuteAsync(null);

        await viewModel.ActiveTab!.CloseCommand.ExecuteAsync(null);

        var tab = Assert.Single(viewModel.Tabs);
        Assert.Null(tab.RequestId);
        Assert.Equal("New request", viewModel.RequestName);
        Assert.Equal(string.Empty, viewModel.Url);
    }

    [Fact]
    public async Task Tabs_ActivateANeighbourWhenTheSelectedTabIsClosed()
    {
        var store = new RecordingWorkspaceStore { SnapshotToLoad = CreateSnapshotWithTwoRequests() };
        var viewModel = CreateViewModel(store, CreateWorkspacePath());
        await viewModel.OpenWorkspaceCommand.ExecuteAsync(null);
        await viewModel.Collections[0].Requests[0].OpenCommand.ExecuteAsync(null);
        await viewModel.Collections[0].Requests[1].OpenCommand.ExecuteAsync(null);
        var closing = viewModel.ActiveTab!;

        await closing.CloseCommand.ExecuteAsync(null);

        var remaining = Assert.Single(viewModel.Tabs);
        Assert.Same(remaining, viewModel.ActiveTab);
        Assert.True(remaining.IsSelected);
        Assert.Equal("Create order", viewModel.RequestName);
    }

    [Fact]
    public async Task Tabs_CanBeReordered()
    {
        var store = new RecordingWorkspaceStore { SnapshotToLoad = CreateSnapshotWithTwoRequests() };
        var viewModel = CreateViewModel(store, CreateWorkspacePath());
        await viewModel.OpenWorkspaceCommand.ExecuteAsync(null);
        await viewModel.Collections[0].Requests[0].OpenCommand.ExecuteAsync(null);
        await viewModel.Collections[0].Requests[1].OpenCommand.ExecuteAsync(null);
        var second = viewModel.Tabs[1];

        second.MoveLeftCommand.Execute(null);
        Assert.Same(second, viewModel.Tabs[0]);

        second.MoveLeftCommand.Execute(null);
        Assert.Same(second, viewModel.Tabs[0]);

        viewModel.MoveTabTo(second, 1);
        Assert.Same(second, viewModel.Tabs[1]);
    }

    [Fact]
    public async Task Tabs_DropTheTabOfADeletedRequest()
    {
        var store = new RecordingWorkspaceStore { SnapshotToLoad = CreateSnapshotWithTwoRequests() };
        var viewModel = CreateViewModel(store, CreateWorkspacePath());
        await viewModel.OpenWorkspaceCommand.ExecuteAsync(null);
        await viewModel.Collections[0].Requests[0].OpenCommand.ExecuteAsync(null);
        await viewModel.Collections[0].Requests[1].OpenCommand.ExecuteAsync(null);
        Assert.Equal(2, viewModel.Tabs.Count);

        await viewModel.Collections[0].Requests[0].DeleteCommand.ExecuteAsync(null);

        Assert.Single(viewModel.Tabs);
        Assert.Equal("Remove order", viewModel.ActiveTab!.Title);
    }

    [Fact]
    public async Task Tabs_AreClearedWhenAnotherWorkspaceIsOpened()
    {
        var store = new RecordingWorkspaceStore { SnapshotToLoad = CreateSnapshotWithTwoRequests() };
        var viewModel = CreateViewModel(store, CreateWorkspacePath());
        await viewModel.OpenWorkspaceCommand.ExecuteAsync(null);
        await viewModel.Collections[0].Requests[0].OpenCommand.ExecuteAsync(null);
        await viewModel.Collections[0].Requests[1].OpenCommand.ExecuteAsync(null);
        Assert.Equal(2, viewModel.Tabs.Count);

        var other = CreateSnapshot() with
        {
            Workspace = CreateSnapshot().Workspace with
            {
                Id = Guid.Parse("88888888-8888-8888-8888-888888888888"),
            },
        };
        var otherStore = new RecordingWorkspaceStore { SnapshotToLoad = other };
        typeof(MainViewModel)
            .GetField("_workspaceStore", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(viewModel, otherStore);

        await viewModel.OpenWorkspaceCommand.ExecuteAsync(null);

        Assert.Single(viewModel.Tabs);
        Assert.Null(viewModel.Tabs[0].RequestId);
    }

    private static MainViewModel CreateViewModel(
        IWorkspaceStore store,
        string directory,
        IRequestExecutor? executor = null,
        RecordingSecretVault? vault = null,
        StubUnsavedChangesPrompt? prompt = null,
        RecordingHistoryStore? historyStore = null,
        StubHistoryClearPrompt? historyClearPrompt = null,
        StubAppSettingsService? appSettings = null,
        StubGitService? gitService = null,
        StubGitSecretScanner? gitSecretScanner = null,
        ICollectionRunner? collectionRunner = null,
        ICollectionRunExportService? collectionRunExportService = null,
        ICollectionRunDataFileService? collectionRunDataFileService = null,
        RecordingCollectionRunHistoryStore? collectionRunHistoryStore = null,
        StubCollectionRunHistoryClearPrompt? collectionRunHistoryClearPrompt = null,
        ITutorialSessionService? tutorialSessionService = null,
        IApplicationInfoService? applicationInfoService = null,
        IExternalLinkService? externalLinkService = null,
        ISupportInformationService? supportInformationService = null,
        IClipboardService? clipboardService = null,
        StubRequestDeletePrompt? requestDeletePrompt = null,
        StubRequestCookieManager? requestCookieManager = null)
    {
        vault ??= new RecordingSecretVault();
        executor ??= new NoOpRequestExecutor();
        appSettings ??= new StubAppSettingsService();
        var templateResolver = new RequestTemplateResolver(vault);
        return new MainViewModel(
            executor,
            collectionRunner ?? new CollectionRunner(executor, templateResolver),
            store,
            new StubFolderPicker(directory),
            templateResolver,
            vault,
            localization: null!,
            new ThemeService(appSettings),
            prompt ?? new StubUnsavedChangesPrompt(),
            historyStore ?? new RecordingHistoryStore(),
            historyClearPrompt ?? new StubHistoryClearPrompt(),
            appSettings,
            gitService ?? new StubGitService(),
            gitSecretScanner ?? new StubGitSecretScanner(),
            collectionRunExportService ?? new RecordingCollectionRunExportService(),
            collectionRunDataFileService ?? new StubCollectionRunDataFileService(),
            collectionRunHistoryStore ?? new RecordingCollectionRunHistoryStore(),
            collectionRunHistoryClearPrompt ?? new StubCollectionRunHistoryClearPrompt(),
            tutorialSessionService ?? new StubTutorialSessionService(CreateTutorialSession()),
            applicationInfoService ?? new RuntimeApplicationInfoService(),
            externalLinkService ?? new RecordingExternalLinkService(),
            supportInformationService ?? new SupportInformationService(),
            clipboardService ?? new RecordingClipboardService(),
            requestDeletePrompt ?? new StubRequestDeletePrompt(),
            requestCookieManager ?? new StubRequestCookieManager());
    }

    private static string CreateWorkspacePath() => Path.Combine(
        Path.GetTempPath(),
        "ReqMint.Tests",
        Guid.NewGuid().ToString("N"));

    private static WorkspaceSnapshot CreateSnapshot()
    {
        var collectionId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var request = new RequestDocument
        {
            Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Name = "Create order",
            Method = "POST",
            Url = "https://api.example.com/orders",
            Headers = [new RequestField("Accept", "application/json")],
            Body = new ApiRequestBody("{\"sku\":\"MINT-1\"}", "application/json"),
            TimeoutSeconds = 45,
        };
        var collection = new CollectionDocument
        {
            Id = collectionId,
            Name = "Commerce API",
            Requests = [request],
        };
        var workspace = new WorkspaceDocument
        {
            Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
            Name = "Commerce",
            Collections =
            [
                new WorkspaceFileReference(collectionId, collection.Name, "collections/commerce.json"),
            ],
        };

        return new WorkspaceSnapshot(workspace, [collection], []);
    }

    private static TutorialSession CreateTutorialSession()
    {
        var collectionId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var environmentId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var collection = new CollectionDocument
        {
            Id = collectionId,
            Name = "Getting Started",
        };
        var environment = new EnvironmentDocument
        {
            Id = environmentId,
            Name = "Tutorial",
            Variables =
            [
                new EnvironmentVariable("TUTORIAL_BASE_URL", "http://127.0.0.1:43210"),
            ],
        };
        var workspace = new WorkspaceDocument
        {
            Id = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
            Name = "ReqMint Tutorial",
            Collections =
            [
                new WorkspaceFileReference(
                    collectionId,
                    collection.Name,
                    "collections/getting-started.json"),
            ],
            Environments =
            [
                new WorkspaceFileReference(
                    environmentId,
                    environment.Name,
                    "environments/tutorial.json"),
            ],
        };
        var request = new RequestDocument
        {
            Id = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
            Name = "Say hello to ReqMint",
            Method = "GET",
            Url = "{{TUTORIAL_BASE_URL}}/api/hello",
            Headers = [new RequestField("Accept", "application/json")],
            Assertions =
            [
                new RequestAssertion
                {
                    Kind = RequestAssertionKind.StatusCodeEquals,
                    ExpectedStatusCode = 200,
                },
            ],
        };
        return new TutorialSession(
            CreateWorkspacePath(),
            new Uri("http://127.0.0.1:43210/"),
            new WorkspaceSnapshot(workspace, [collection], [environment]),
            request,
            collectionId,
            environmentId);
    }

    private static RequestHistoryEntry CreateHistoryEntry(
        Guid workspaceId,
        string name,
        string method,
        int statusCode) => new()
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            SentAtUtc = DateTimeOffset.UtcNow,
            Outcome = "completed",
            StatusCode = statusCode,
            ReasonPhrase = statusCode == 201 ? "Created" : "OK",
            Request = new RequestDocument
            {
                Id = Guid.NewGuid(),
                Name = name,
                Method = method,
                Url = $"https://api.example.com/{name.Replace(' ', '-').ToLowerInvariant()}",
            },
        };

    private static CollectionRunDataRow DataRow(
        params (string Name, string Value)[] variables) => new()
        {
            Variables = variables.ToDictionary(
                variable => variable.Name,
                variable => variable.Value,
                StringComparer.OrdinalIgnoreCase),
        };

    private sealed class StubFolderPicker(string directory) : IWorkspaceFolderPicker
    {
        public Task<string?> PickFolderAsync(string title) => Task.FromResult<string?>(directory);
    }

    private sealed class StubUnsavedChangesPrompt : IUnsavedChangesPrompt
    {
        public UnsavedChangesChoice Choice { get; init; } = UnsavedChangesChoice.Discard;

        public int CallCount { get; private set; }

        public bool LastCanSave { get; private set; }

        public Task<UnsavedChangesChoice> ShowAsync(string requestName, bool canSave)
        {
            CallCount++;
            LastCanSave = canSave;
            return Task.FromResult(Choice);
        }
    }

    private sealed class StubHistoryClearPrompt : IHistoryClearPrompt
    {
        public bool Confirmed { get; set; }

        public int CallCount { get; private set; }

        public Task<bool> ShowAsync(string workspaceName, int entryCount)
        {
            CallCount++;
            return Task.FromResult(Confirmed);
        }
    }

    private sealed class StubAppSettingsService : IAppSettingsService
    {
        public StubAppSettingsService(AppSettings? settings = null)
        {
            Current = settings ?? new AppSettings();
        }

        public AppSettings Current { get; private set; }

        public void Update(AppSettings settings) => Current = settings;
    }

    private sealed class StubRequestCookieManager : IRequestCookieManager
    {
        public bool IsEnabled { get; private set; }

        public string? SelectedWorkspace { get; private set; }

        public int ClearCount { get; private set; }

        public void SetEnabled(bool enabled) => IsEnabled = enabled;

        public void SelectWorkspace(string? workspaceDirectory) =>
            SelectedWorkspace = workspaceDirectory is null
                ? null
                : Path.GetFullPath(workspaceDirectory);

        public void ClearActiveWorkspace() => ClearCount++;
    }

    private sealed class StubTutorialSessionService(TutorialSession session)
        : ITutorialSessionService
    {
        public int CallCount { get; private set; }

        public Task<TutorialSession> StartAsync(
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(session);
        }

        public void Dispose()
        {
        }
    }

    private sealed class RecordingExternalLinkService : IExternalLinkService
    {
        public List<Uri> OpenedUris { get; } = [];

        public Task<bool> OpenAsync(Uri uri)
        {
            OpenedUris.Add(uri);
            return Task.FromResult(true);
        }
    }

    private sealed class StubRequestDeletePrompt(bool confirm = true) : IRequestDeletePrompt
    {
        public int CallCount { get; private set; }

        public string? LastRequestName { get; private set; }

        public Task<bool> ShowAsync(string requestName)
        {
            CallCount++;
            LastRequestName = requestName;
            return Task.FromResult(confirm);
        }
    }

    private sealed class RecordingClipboardService : IClipboardService
    {
        public string? Text { get; private set; }

        public Task<bool> SetTextAsync(string text)
        {
            Text = text;
            return Task.FromResult(true);
        }
    }

    private sealed class StubGitService : IGitService
    {
        public GitRepositoryStatus? Status { get; init; }

        public string DiffContent { get; init; } = string.Empty;

        public GitDiffPreviewState DiffState { get; init; } = GitDiffPreviewState.Available;

        public int DiffSecurityWarningCount { get; init; }

        public int CallCount { get; private set; }

        public GitDiffScope? LastDiffScope { get; private set; }

        public int DiffCallCount { get; private set; }

        public GitStageResultState StageState { get; init; } = GitStageResultState.Staged;

        public int StageCallCount { get; private set; }

        public string? LastStagedPath { get; private set; }

        public GitCommitPreflight CommitPreflight { get; init; } = new()
        {
            State = GitCommitPreflightState.NoStagedReqMintFiles,
        };

        public GitCommitResult CommitResult { get; init; } = new()
        {
            State = GitCommitResultState.PreflightBlocked,
        };

        public int CommitPreflightCallCount { get; private set; }

        public int CommitCallCount { get; private set; }

        public string? LastCommitMessage { get; private set; }

        public GitRemotePreflight RemotePreflight { get; init; } = new();

        public GitFetchResult FetchResult { get; init; } = new();

        public int RemotePreflightCallCount { get; private set; }

        public int FetchCallCount { get; private set; }

        public GitFastForwardPreflight FastForwardPreflight { get; init; } = new();

        public GitFastForwardResult FastForwardResult { get; init; } = new();

        public int FastForwardPreflightCallCount { get; private set; }

        public int FastForwardCallCount { get; private set; }

        public GitPushPreflight PushPreflight { get; init; } = new();

        public GitPushResult PushResult { get; init; } = new();

        public int PushPreflightCallCount { get; private set; }

        public int PushCallCount { get; private set; }

        public Task<GitRepositoryStatus?> GetStatusAsync(
            string workspaceDirectory,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(Status);
        }

        public Task<GitDiffPreview> GetDiffAsync(
            string workspaceDirectory,
            string workspaceRelativePath,
            GitDiffScope scope,
            CancellationToken cancellationToken = default)
        {
            DiffCallCount++;
            LastDiffScope = scope;
            return Task.FromResult(new GitDiffPreview
            {
                Path = workspaceRelativePath,
                Scope = scope,
                State = DiffState,
                Content = DiffContent,
                SecurityWarningCount = DiffSecurityWarningCount,
            });
        }

        public Task<GitStageResult> StageFileAsync(
            string workspaceDirectory,
            string workspaceRelativePath,
            CancellationToken cancellationToken = default)
        {
            StageCallCount++;
            LastStagedPath = workspaceRelativePath;
            return Task.FromResult(new GitStageResult
            {
                Path = workspaceRelativePath,
                State = StageState,
            });
        }

        public Task<GitCommitPreflight> GetCommitPreflightAsync(
            string workspaceDirectory,
            CancellationToken cancellationToken = default)
        {
            CommitPreflightCallCount++;
            return Task.FromResult(CommitPreflight);
        }

        public Task<GitCommitResult> CommitAsync(
            string workspaceDirectory,
            string message,
            CancellationToken cancellationToken = default)
        {
            CommitCallCount++;
            LastCommitMessage = message;
            return Task.FromResult(CommitResult);
        }

        public Task<GitRemotePreflight> GetRemotePreflightAsync(
            string workspaceDirectory,
            CancellationToken cancellationToken = default)
        {
            RemotePreflightCallCount++;
            return Task.FromResult(RemotePreflight);
        }

        public Task<GitFetchResult> FetchAsync(
            string workspaceDirectory,
            CancellationToken cancellationToken = default)
        {
            FetchCallCount++;
            return Task.FromResult(FetchResult);
        }

        public Task<GitFastForwardPreflight> GetFastForwardPreflightAsync(
            string workspaceDirectory,
            CancellationToken cancellationToken = default)
        {
            FastForwardPreflightCallCount++;
            return Task.FromResult(FastForwardPreflight);
        }

        public Task<GitFastForwardResult> FastForwardAsync(
            string workspaceDirectory,
            CancellationToken cancellationToken = default)
        {
            FastForwardCallCount++;
            return Task.FromResult(FastForwardResult);
        }

        public Task<GitPushPreflight> GetPushPreflightAsync(
            string workspaceDirectory,
            CancellationToken cancellationToken = default)
        {
            PushPreflightCallCount++;
            return Task.FromResult(PushPreflight);
        }

        public Task<GitPushResult> PushAsync(
            string workspaceDirectory,
            CancellationToken cancellationToken = default)
        {
            PushCallCount++;
            return Task.FromResult(PushResult);
        }
    }

    private sealed class StubGitSecretScanner : IGitSecretScanner
    {
        public GitSecretScanResult Result { get; init; } = GitSecretScanResult.Empty;

        public IReadOnlyList<string> LastPaths { get; private set; } = [];

        public Task<GitSecretScanResult> ScanAsync(
            string workspaceDirectory,
            IReadOnlyList<string> workspaceRelativePaths,
            CancellationToken cancellationToken = default)
        {
            LastPaths = workspaceRelativePaths;
            return Task.FromResult(Result);
        }
    }

    private sealed class StubCollectionRunner : ICollectionRunner
    {
        public required CollectionRunResult Result { get; init; }

        public int CallCount { get; private set; }

        public CollectionRunDefinition? Definition { get; private set; }

        public Task<CollectionRunResult> RunAsync(
            CollectionRunDefinition definition,
            IProgress<CollectionRunProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            Definition = definition;
            return Task.FromResult(Result);
        }
    }

    private sealed class RecordingCollectionRunExportService : ICollectionRunExportService
    {
        public CollectionRunResult? Result { get; private set; }

        public CollectionRunExportFormat? Format { get; private set; }

        public string? SuggestedFileName { get; private set; }

        public Task<bool> ExportAsync(
            CollectionRunResult result,
            CollectionRunExportFormat format,
            string suggestedFileName,
            CancellationToken cancellationToken = default)
        {
            Result = result;
            Format = format;
            SuggestedFileName = suggestedFileName;
            return Task.FromResult(true);
        }
    }

    private sealed class StubCollectionRunDataFileService : ICollectionRunDataFileService
    {
        public CollectionRunDataFile? Selection { get; init; }

        public Task<CollectionRunDataFile?> LoadAsync(
            CancellationToken cancellationToken = default) => Task.FromResult(Selection);
    }

    private sealed class RecordingCollectionRunHistoryStore(
        IEnumerable<CollectionRunHistoryEntry>? initialEntries = null) : ICollectionRunHistoryStore
    {
        public List<CollectionRunHistoryEntry> Entries { get; } =
            initialEntries?.ToList() ?? [];

        public int? LastRetentionLimit { get; private set; }

        public Task AddAsync(
            CollectionRunHistoryEntry entry,
            int retentionLimit = 50,
            CancellationToken cancellationToken = default)
        {
            LastRetentionLimit = retentionLimit;
            Entries.Insert(0, entry);
            if (Entries.Count > retentionLimit)
            {
                Entries.RemoveRange(retentionLimit, Entries.Count - retentionLimit);
            }

            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<CollectionRunHistoryEntry>> ListAsync(
            Guid workspaceId,
            Guid collectionId,
            int take = 50,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CollectionRunHistoryEntry>>(Entries
                .Where(entry => entry.WorkspaceId == workspaceId
                    && entry.CollectionId == collectionId)
                .Take(take)
                .ToArray());

        public Task ClearAsync(
            Guid workspaceId,
            Guid collectionId,
            CancellationToken cancellationToken = default)
        {
            Entries.RemoveAll(entry => entry.WorkspaceId == workspaceId
                && entry.CollectionId == collectionId);
            return Task.CompletedTask;
        }
    }

    private sealed class StubCollectionRunHistoryClearPrompt : ICollectionRunHistoryClearPrompt
    {
        public bool Confirmed { get; init; }

        public int CallCount { get; private set; }

        public Task<bool> ShowAsync(string collectionName, int entryCount)
        {
            CallCount++;
            return Task.FromResult(Confirmed);
        }
    }

    private sealed class RecordingWorkspaceStore : IWorkspaceStore
    {
        public WorkspaceSnapshot? SnapshotToLoad { get; init; }

        public WorkspaceSnapshot? SavedSnapshot { get; private set; }

        public string? SavedDirectory { get; private set; }

        public void ForgetLastSave()
        {
            SavedSnapshot = null;
            SavedDirectory = null;
        }

        public Task<WorkspaceSnapshot> LoadAsync(
            string workspaceDirectory,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(SnapshotToLoad ?? throw new InvalidOperationException("No snapshot configured."));

        public Task SaveAsync(
            string workspaceDirectory,
            WorkspaceSnapshot snapshot,
            CancellationToken cancellationToken = default)
        {
            SavedDirectory = workspaceDirectory;
            SavedSnapshot = snapshot;
            return Task.CompletedTask;
        }
    }

    private sealed class NoOpRequestExecutor : IRequestExecutor
    {
        public Task<ApiResponse> ExecuteAsync(
            ApiRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingSecretVault : ISecretVault
    {
        public string? ValueToRead { get; init; }

        public List<(SecretReference Reference, string Value)> StoredValues { get; } = [];

        public Task<string?> GetAsync(
            SecretReference reference,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(ValueToRead);

        public Task SetAsync(
            SecretReference reference,
            string value,
            CancellationToken cancellationToken = default)
        {
            StoredValues.Add((reference, value));
            return Task.CompletedTask;
        }

        public Task DeleteAsync(
            SecretReference reference,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class RecordingRequestExecutor : IRequestExecutor
    {
        public ApiRequest? Request { get; private set; }

        public bool IsBodyTruncated { get; init; }

        public Task<ApiResponse> ExecuteAsync(
            ApiRequest request,
            CancellationToken cancellationToken = default)
        {
            Request = request;
            return Task.FromResult(new ApiResponse(
                200,
                "OK",
                new Dictionary<string, IReadOnlyList<string>>(),
                "{}",
                "application/json",
                TimeSpan.FromMilliseconds(12),
                IsBodyTruncated));
        }
    }

    private sealed class RecordingHistoryStore(
        IEnumerable<RequestHistoryEntry>? initialEntries = null) : IRequestHistoryStore
    {
        public List<RequestHistoryEntry> Entries { get; } = initialEntries?.ToList() ?? [];

        public int? LastRetentionLimit { get; private set; }

        public Task AddAsync(
            RequestHistoryEntry entry,
            int retentionLimit = 200,
            CancellationToken cancellationToken = default)
        {
            LastRetentionLimit = retentionLimit;
            Entries.Insert(0, entry);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<RequestHistoryEntry>> ListAsync(
            Guid workspaceId,
            int take = 100,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<RequestHistoryEntry>>(
                Entries.Where(entry => entry.WorkspaceId == workspaceId).Take(take).ToArray());

        public Task ClearAsync(Guid workspaceId, CancellationToken cancellationToken = default)
        {
            Entries.RemoveAll(entry => entry.WorkspaceId == workspaceId);
            return Task.CompletedTask;
        }
    }
}

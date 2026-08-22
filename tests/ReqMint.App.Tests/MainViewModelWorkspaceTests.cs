using ReqMint.App.Services;
using ReqMint.App.ViewModels;
using ReqMint.Core.Git;
using ReqMint.Core.History;
using ReqMint.Core.Requests;
using ReqMint.Core.Security;
using ReqMint.Core.Templates;
using ReqMint.Core.Workspaces;

namespace ReqMint.App.Tests;

public sealed class MainViewModelWorkspaceTests
{
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
            Headers = [new RequestField("Authorization", "Bearer {{TOKEN}}")],
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
            Assert.Single(executor.Request!.Headers).Value);
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
        Assert.Equal("2 changed files · ahead 1", viewModel.GitSummary);
        Assert.Equal(2, viewModel.GitChanges.Count);
        Assert.Equal("C:/repos/commerce", viewModel.GitRepositoryRoot);
        Assert.True(git.CallCount >= 2);
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

    [Theory]
    [InlineData(UnsavedChangesChoice.Cancel, "https://api.example.com/changed")]
    [InlineData(UnsavedChangesChoice.Discard, "")]
    public async Task NewRequestCommand_ProtectsUnsavedChanges(
        UnsavedChangesChoice choice,
        string expectedUrl)
    {
        var store = new RecordingWorkspaceStore { SnapshotToLoad = CreateSnapshot() };
        var prompt = new StubUnsavedChangesPrompt { Choice = choice };
        var viewModel = CreateViewModel(store, CreateWorkspacePath(), prompt: prompt);
        await viewModel.OpenWorkspaceCommand.ExecuteAsync(null);
        await viewModel.Collections[0].Requests[0].OpenCommand.ExecuteAsync(null);
        viewModel.Url = "https://api.example.com/changed";

        await viewModel.NewRequestCommand.ExecuteAsync(null);

        Assert.Equal(expectedUrl, viewModel.Url);
        Assert.Equal(1, prompt.CallCount);
    }

    [Fact]
    public async Task NewRequestCommand_SavesDirtyRequestBeforeNavigating()
    {
        var store = new RecordingWorkspaceStore { SnapshotToLoad = CreateSnapshot() };
        var prompt = new StubUnsavedChangesPrompt { Choice = UnsavedChangesChoice.Save };
        var viewModel = CreateViewModel(store, CreateWorkspacePath(), prompt: prompt);
        await viewModel.OpenWorkspaceCommand.ExecuteAsync(null);
        await viewModel.Collections[0].Requests[0].OpenCommand.ExecuteAsync(null);
        viewModel.Url = "https://api.example.com/changed";

        await viewModel.NewRequestCommand.ExecuteAsync(null);

        Assert.Equal("https://api.example.com/changed", store.SavedSnapshot!.Collections[0].Requests[0].Url);
        Assert.Equal(string.Empty, viewModel.Url);
    }

    private static MainViewModel CreateViewModel(
        RecordingWorkspaceStore store,
        string directory,
        IRequestExecutor? executor = null,
        RecordingSecretVault? vault = null,
        StubUnsavedChangesPrompt? prompt = null,
        RecordingHistoryStore? historyStore = null,
        StubHistoryClearPrompt? historyClearPrompt = null,
        StubAppSettingsService? appSettings = null,
        StubGitService? gitService = null)
    {
        vault ??= new RecordingSecretVault();
        return new MainViewModel(
            executor ?? new NoOpRequestExecutor(),
            store,
            new StubFolderPicker(directory),
            new RequestTemplateResolver(vault),
            vault,
            localization: null!,
            prompt ?? new StubUnsavedChangesPrompt(),
            historyStore ?? new RecordingHistoryStore(),
            historyClearPrompt ?? new StubHistoryClearPrompt(),
            appSettings ?? new StubAppSettingsService(),
            gitService ?? new StubGitService());
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

    private sealed class StubFolderPicker(string directory) : IWorkspaceFolderPicker
    {
        public Task<string?> PickFolderAsync(string title) => Task.FromResult<string?>(directory);
    }

    private sealed class StubUnsavedChangesPrompt : IUnsavedChangesPrompt
    {
        public UnsavedChangesChoice Choice { get; init; } = UnsavedChangesChoice.Discard;

        public int CallCount { get; private set; }

        public Task<UnsavedChangesChoice> ShowAsync(string requestName, bool canSave)
        {
            CallCount++;
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
        public AppSettings Current { get; private set; } = new();

        public void Update(AppSettings settings) => Current = settings;
    }

    private sealed class StubGitService : IGitService
    {
        public GitRepositoryStatus? Status { get; init; }

        public int CallCount { get; private set; }

        public Task<GitRepositoryStatus?> GetStatusAsync(
            string workspaceDirectory,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(Status);
        }
    }

    private sealed class RecordingWorkspaceStore : IWorkspaceStore
    {
        public WorkspaceSnapshot? SnapshotToLoad { get; init; }

        public WorkspaceSnapshot? SavedSnapshot { get; private set; }

        public string? SavedDirectory { get; private set; }

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

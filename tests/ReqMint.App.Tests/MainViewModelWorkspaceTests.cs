using ReqMint.App.Services;
using ReqMint.App.ViewModels;
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
        viewModel.Collections[0].Requests[0].OpenCommand.Execute(null);

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
        viewModel.Collections[0].Requests[0].OpenCommand.Execute(null);
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
        viewModel.Collections[0].Requests[0].OpenCommand.Execute(null);

        viewModel.NewRequestCommand.Execute(null);
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
        viewModel.Collections[0].Requests[0].OpenCommand.Execute(null);

        await viewModel.SendCommand.ExecuteAsync(null);

        Assert.Equal("https://api.example.com/orders", executor.Request?.Url.AbsoluteUri);
        Assert.Equal(
            "Bearer secret-token",
            Assert.Single(executor.Request!.Headers).Value);
        Assert.Equal("200 OK", viewModel.ResponseStatus);
    }

    private static MainViewModel CreateViewModel(
        RecordingWorkspaceStore store,
        string directory,
        IRequestExecutor? executor = null,
        RecordingSecretVault? vault = null)
    {
        vault ??= new RecordingSecretVault();
        return new MainViewModel(
            executor ?? new NoOpRequestExecutor(),
            store,
            new StubFolderPicker(directory),
            new RequestTemplateResolver(vault),
            vault);
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

    private sealed class StubFolderPicker(string directory) : IWorkspaceFolderPicker
    {
        public Task<string?> PickFolderAsync(string title) => Task.FromResult<string?>(directory);
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
                IsBodyTruncated: false));
        }
    }
}

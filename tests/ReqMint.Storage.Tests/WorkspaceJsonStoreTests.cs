using ReqMint.Core.Requests;
using ReqMint.Core.Workspaces;
using ReqMint.Storage;

namespace ReqMint.Storage.Tests;

public sealed class WorkspaceJsonStoreTests
{
    [Fact]
    public async Task SaveAndLoadAsync_RoundTripsWorkspaceLayout()
    {
        using var directory = new TemporaryDirectory();
        var store = new WorkspaceJsonStore();
        var snapshot = CreateSnapshot();

        await store.SaveAsync(directory.Path, snapshot, CancellationToken.None);
        var loaded = await store.LoadAsync(directory.Path, CancellationToken.None);

        Assert.True(File.Exists(System.IO.Path.Combine(directory.Path, WorkspaceJsonStore.WorkspaceFileName)));
        Assert.True(File.Exists(System.IO.Path.Combine(directory.Path, "collections", "sample.json")));
        Assert.True(File.Exists(System.IO.Path.Combine(directory.Path, "environments", "local.json")));
        Assert.Equal(snapshot.Workspace.Id, loaded.Workspace.Id);
        Assert.Equal(snapshot.Workspace.Name, loaded.Workspace.Name);
        Assert.Equal(snapshot.Workspace.Collections, loaded.Workspace.Collections);
        Assert.Equal(snapshot.Workspace.Environments, loaded.Workspace.Environments);
        Assert.Equal(snapshot.Collections[0].Id, loaded.Collections[0].Id);
        Assert.Equal(snapshot.Collections[0].Name, loaded.Collections[0].Name);
        var expectedRequest = snapshot.Collections[0].Requests[0];
        var actualRequest = loaded.Collections[0].Requests[0];
        Assert.Equal(expectedRequest.Id, actualRequest.Id);
        Assert.Equal(expectedRequest.Name, actualRequest.Name);
        Assert.Equal(expectedRequest.Method, actualRequest.Method);
        Assert.Equal(expectedRequest.Url, actualRequest.Url);
        Assert.Equal(expectedRequest.QueryParameters, actualRequest.QueryParameters);
        Assert.Equal(expectedRequest.Headers, actualRequest.Headers);
        Assert.Equal(expectedRequest.Body, actualRequest.Body);
        Assert.Equal(expectedRequest.TimeoutSeconds, actualRequest.TimeoutSeconds);
        Assert.Equal(snapshot.Environments[0].Id, loaded.Environments[0].Id);
        Assert.Equal(snapshot.Environments[0].Name, loaded.Environments[0].Name);
        Assert.Equal(snapshot.Environments[0].Variables, loaded.Environments[0].Variables);
        Assert.Null(loaded.Environments[0].Variables[1].Value);
    }

    [Fact]
    public async Task SaveAsync_RejectsSecretValuesBeforeWritingFiles()
    {
        using var directory = new TemporaryDirectory();
        var store = new WorkspaceJsonStore();
        var snapshot = CreateSnapshot(secretValue: "do-not-persist");

        var exception = await Assert.ThrowsAsync<WorkspaceFormatException>(
            () => store.SaveAsync(directory.Path, snapshot, CancellationToken.None));

        Assert.Contains("cannot be persisted", exception.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(System.IO.Path.Combine(directory.Path, WorkspaceJsonStore.WorkspaceFileName)));
    }

    [Fact]
    public async Task SaveAsync_RejectsReferencesOutsideWorkspaceFolder()
    {
        using var directory = new TemporaryDirectory();
        var store = new WorkspaceJsonStore();
        var snapshot = CreateSnapshot(collectionFile: "../outside.json");
        var outsidePath = System.IO.Path.GetFullPath(
            System.IO.Path.Combine(directory.Path, "../outside.json"));

        await Assert.ThrowsAsync<WorkspaceFormatException>(
            () => store.SaveAsync(directory.Path, snapshot, CancellationToken.None));

        Assert.False(File.Exists(outsidePath));
    }

    [Fact]
    public async Task SaveAsync_RejectsUnsupportedSchemaVersion()
    {
        using var directory = new TemporaryDirectory();
        var store = new WorkspaceJsonStore();
        var snapshot = CreateSnapshot() with
        {
            Workspace = CreateSnapshot().Workspace with { SchemaVersion = 99 },
        };

        var exception = await Assert.ThrowsAsync<WorkspaceFormatException>(
            () => store.SaveAsync(directory.Path, snapshot, CancellationToken.None));

        Assert.Contains("Unsupported workspace schema version 99", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SaveAsync_RejectsReferencesThatShareTheSameFile()
    {
        using var directory = new TemporaryDirectory();
        var store = new WorkspaceJsonStore();
        var snapshot = CreateSnapshot();
        var duplicateId = Guid.Parse("55555555-5555-5555-5555-555555555555");
        var duplicateReference = new WorkspaceFileReference(
            duplicateId,
            "Duplicate collection",
            "collections/sample.json");
        var duplicateCollection = new CollectionDocument
        {
            Id = duplicateId,
            Name = duplicateReference.Name,
        };
        var invalidSnapshot = snapshot with
        {
            Workspace = snapshot.Workspace with
            {
                Collections = [snapshot.Workspace.Collections[0], duplicateReference],
            },
            Collections = [snapshot.Collections[0], duplicateCollection],
        };

        var exception = await Assert.ThrowsAsync<WorkspaceFormatException>(
            () => store.SaveAsync(directory.Path, invalidSnapshot, CancellationToken.None));

        Assert.Contains("same file", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SaveAsync_ReplacesDocumentsWithoutLeavingTemporaryFiles()
    {
        using var directory = new TemporaryDirectory();
        var store = new WorkspaceJsonStore();
        var snapshot = CreateSnapshot();
        await store.SaveAsync(directory.Path, snapshot, CancellationToken.None);

        var updatedCollection = snapshot.Collections[0] with { Name = "Renamed collection" };
        var updatedReference = snapshot.Workspace.Collections[0] with { Name = updatedCollection.Name };
        var updatedSnapshot = snapshot with
        {
            Workspace = snapshot.Workspace with { Collections = [updatedReference] },
            Collections = [updatedCollection],
        };

        await store.SaveAsync(directory.Path, updatedSnapshot, CancellationToken.None);
        var loaded = await store.LoadAsync(directory.Path, CancellationToken.None);

        Assert.Equal("Renamed collection", loaded.Collections[0].Name);
        Assert.Empty(Directory.EnumerateFiles(directory.Path, "*.tmp-*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task SaveAndLoadAsync_PreservesTemplatedRequestUrls()
    {
        using var directory = new TemporaryDirectory();
        var store = new WorkspaceJsonStore();
        var snapshot = CreateSnapshot();
        var collection = snapshot.Collections[0] with
        {
            Requests =
            [
                snapshot.Collections[0].Requests[0] with
                {
                    Url = "{{BASE_URL}}/items/{{ITEM_ID}}",
                },
            ],
        };
        snapshot = snapshot with { Collections = [collection] };

        await store.SaveAsync(directory.Path, snapshot, CancellationToken.None);
        var loaded = await store.LoadAsync(directory.Path, CancellationToken.None);

        Assert.Equal("{{BASE_URL}}/items/{{ITEM_ID}}", loaded.Collections[0].Requests[0].Url);
    }

    private static WorkspaceSnapshot CreateSnapshot(
        string? secretValue = null,
        string collectionFile = "collections/sample.json")
    {
        var workspaceId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var collectionId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var environmentId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var requestId = Guid.Parse("44444444-4444-4444-4444-444444444444");

        var workspace = new WorkspaceDocument
        {
            Id = workspaceId,
            Name = "Sample workspace",
            Collections =
            [
                new WorkspaceFileReference(collectionId, "Sample collection", collectionFile),
            ],
            Environments =
            [
                new WorkspaceFileReference(environmentId, "Local", "environments/local.json"),
            ],
        };

        var collection = new CollectionDocument
        {
            Id = collectionId,
            Name = "Sample collection",
            Requests =
            [
                new RequestDocument
                {
                    Id = requestId,
                    Name = "Create item",
                    Method = "POST",
                    Url = "https://api.example.com/items",
                    QueryParameters = [new RequestField("preview", "true")],
                    Headers = [new RequestField("Accept", "application/json")],
                    Body = new ApiRequestBody("{\"name\":\"ReqMint\"}", "application/json"),
                    TimeoutSeconds = 45,
                },
            ],
        };

        var environment = new EnvironmentDocument
        {
            Id = environmentId,
            Name = "Local",
            Variables =
            [
                new EnvironmentVariable("BASE_URL", "https://localhost:7001"),
                new EnvironmentVariable("API_TOKEN", secretValue, IsSecret: true),
            ],
        };

        return new WorkspaceSnapshot(workspace, [collection], [environment]);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "ReqMint.Tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}

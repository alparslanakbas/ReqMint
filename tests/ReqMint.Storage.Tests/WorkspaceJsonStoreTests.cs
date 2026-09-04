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
        Assert.Equal(expectedRequest.Authentication, actualRequest.Authentication);
        Assert.Equal(expectedRequest.Body?.Content, actualRequest.Body?.Content);
        Assert.Equal(expectedRequest.Body?.ContentType, actualRequest.Body?.ContentType);
        Assert.Equal(expectedRequest.Body?.FormFields, actualRequest.Body?.FormFields);
        Assert.Equal(expectedRequest.TimeoutSeconds, actualRequest.TimeoutSeconds);
        Assert.Equal(expectedRequest.Assertions, actualRequest.Assertions);
        Assert.Equal(snapshot.Environments[0].Id, loaded.Environments[0].Id);
        Assert.Equal(snapshot.Environments[0].Name, loaded.Environments[0].Name);
        Assert.Equal(snapshot.Environments[0].Variables, loaded.Environments[0].Variables);
        Assert.Null(loaded.Environments[0].Variables[1].Value);
        var collectionJson = await File.ReadAllTextAsync(
            System.IO.Path.Combine(directory.Path, "collections", "sample.json"));
        Assert.Contains("\"type\": \"Bearer\"", collectionJson, StringComparison.Ordinal);
        Assert.DoesNotContain("basicPassword", collectionJson, StringComparison.Ordinal);
        Assert.DoesNotContain("apiKeyValue", collectionJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SaveAndLoadAsync_PersistsFileMetadataWithoutLocalPath()
    {
        using var directory = new TemporaryDirectory();
        var original = CreateSnapshot();
        var request = original.Collections[0].Requests[0] with
        {
            Body = new ApiRequestBody(string.Empty, "multipart/form-data")
            {
                FileFields =
                [
                    new RequestFileField("attachment", "sample.txt")
                    {
                        LocalPath = "C:/Users/private/sample.txt",
                    },
                ],
            },
        };
        var snapshot = original with
        {
            Collections = [original.Collections[0] with { Requests = [request] }],
        };
        var store = new WorkspaceJsonStore();

        await store.SaveAsync(directory.Path, snapshot, CancellationToken.None);
        var loaded = await store.LoadAsync(directory.Path, CancellationToken.None);
        var json = await File.ReadAllTextAsync(
            Path.Combine(directory.Path, "collections", "sample.json"));

        var file = Assert.Single(loaded.Collections[0].Requests[0].Body!.FileFields);
        Assert.Equal("attachment", file.Name);
        Assert.Equal("sample.txt", file.FileName);
        Assert.Null(file.LocalPath);
        Assert.DoesNotContain("C:/Users/private", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("localPath", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LoadAsync_TreatsFieldsWrittenBeforeTheEnabledFlagAsEnabled()
    {
        using var directory = new TemporaryDirectory();
        var store = new WorkspaceJsonStore();
        var workspaceId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var collectionId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var requestId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        Directory.CreateDirectory(System.IO.Path.Combine(directory.Path, "collections"));
        await File.WriteAllTextAsync(
            System.IO.Path.Combine(directory.Path, WorkspaceJsonStore.WorkspaceFileName),
            $$"""
            {
              "schemaVersion": 1,
              "id": "{{workspaceId}}",
              "name": "Legacy",
              "collections": [
                { "id": "{{collectionId}}", "name": "Requests", "file": "collections/requests.json" }
              ],
              "environments": []
            }
            """);
        await File.WriteAllTextAsync(
            System.IO.Path.Combine(directory.Path, "collections", "requests.json"),
            $$"""
            {
              "schemaVersion": 1,
              "id": "{{collectionId}}",
              "name": "Requests",
              "requests": [
                {
                  "id": "{{requestId}}",
                  "name": "Legacy request",
                  "method": "GET",
                  "url": "https://api.example.com/orders",
                  "queryParameters": [ { "name": "include", "value": "items" } ],
                  "headers": [ { "name": "Accept", "value": "application/json" } ],
                  "body": null,
                  "timeoutSeconds": 30,
                  "assertions": []
                }
              ]
            }
            """);

        var loaded = await store.LoadAsync(directory.Path, CancellationToken.None);

        var request = loaded.Collections[0].Requests[0];
        Assert.True(request.QueryParameters[0].IsEnabled);
        Assert.True(request.Headers[0].IsEnabled);
        Assert.Null(request.Authentication);
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
    public async Task SaveAsync_RejectsLiteralAuthenticationSecretsBeforeWritingFiles()
    {
        using var directory = new TemporaryDirectory();
        var store = new WorkspaceJsonStore();
        var snapshot = CreateSnapshot();
        var collection = snapshot.Collections[0];
        var request = collection.Requests[0] with
        {
            Authentication = new RequestAuthentication
            {
                Type = RequestAuthenticationType.Bearer,
                BearerToken = "literal-secret-must-not-be-written",
            },
        };
        snapshot = snapshot with
        {
            Collections = [collection with { Requests = [request] }],
        };

        var exception = await Assert.ThrowsAsync<WorkspaceFormatException>(
            () => store.SaveAsync(directory.Path, snapshot, CancellationToken.None));

        Assert.Contains("cannot be persisted", exception.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(System.IO.Path.Combine(directory.Path, WorkspaceJsonStore.WorkspaceFileName)));
    }

    [Fact]
    public async Task SaveAsync_RejectsLiteralSecretsInInactiveAuthenticationFields()
    {
        using var directory = new TemporaryDirectory();
        var store = new WorkspaceJsonStore();
        var snapshot = CreateSnapshot();
        var collection = snapshot.Collections[0];
        var request = collection.Requests[0] with
        {
            Authentication = new RequestAuthentication
            {
                Type = RequestAuthenticationType.Bearer,
                BearerToken = "{{API_TOKEN}}",
                BasicPassword = "unused-but-still-sensitive",
            },
        };
        snapshot = snapshot with
        {
            Collections = [collection with { Requests = [request] }],
        };

        var exception = await Assert.ThrowsAsync<WorkspaceFormatException>(
            () => store.SaveAsync(directory.Path, snapshot, CancellationToken.None));

        Assert.Contains("cannot be persisted", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SaveAsync_RejectsInvalidRunnerAssertionsBeforeWritingFiles()
    {
        using var directory = new TemporaryDirectory();
        var store = new WorkspaceJsonStore();
        var snapshot = CreateSnapshot();
        var request = snapshot.Collections[0].Requests[0] with
        {
            Assertions =
            [
                new RequestAssertion
                {
                    Kind = RequestAssertionKind.JsonPointerExists,
                    JsonPointer = "data/id",
                },
            ],
        };
        snapshot = snapshot with
        {
            Collections =
            [
                snapshot.Collections[0] with { Requests = [request] },
            ],
        };

        await Assert.ThrowsAsync<WorkspaceFormatException>(
            () => store.SaveAsync(directory.Path, snapshot, CancellationToken.None));

        Assert.False(File.Exists(
            System.IO.Path.Combine(directory.Path, WorkspaceJsonStore.WorkspaceFileName)));
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
    public async Task LoadAsync_RejectsCollectionFileSymbolicLinks()
    {
        using var directory = new TemporaryDirectory();
        using var outsideDirectory = new TemporaryDirectory();
        var store = new WorkspaceJsonStore();
        var snapshot = CreateSnapshot();
        await store.SaveAsync(directory.Path, snapshot, CancellationToken.None);

        var outsideCollection = System.IO.Path.Combine(outsideDirectory.Path, "outside.json");
        await File.WriteAllTextAsync(outsideCollection, "{}");
        var collectionPath = System.IO.Path.Combine(directory.Path, "collections", "sample.json");
        File.Delete(collectionPath);
        if (!TryCreateFileSymbolicLink(collectionPath, outsideCollection))
        {
            return;
        }

        var exception = await Assert.ThrowsAsync<WorkspaceFormatException>(
            () => store.LoadAsync(directory.Path, CancellationToken.None));

        Assert.Contains("symbolic link", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SaveAsync_RejectsCollectionDirectorySymbolicLinks()
    {
        using var directory = new TemporaryDirectory();
        using var outsideDirectory = new TemporaryDirectory();
        var collectionsPath = System.IO.Path.Combine(directory.Path, "collections");
        if (!TryCreateDirectorySymbolicLink(collectionsPath, outsideDirectory.Path))
        {
            return;
        }

        var store = new WorkspaceJsonStore();
        var exception = await Assert.ThrowsAsync<WorkspaceFormatException>(
            () => store.SaveAsync(directory.Path, CreateSnapshot(), CancellationToken.None));

        Assert.Contains("symbolic link", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(System.IO.Path.Combine(outsideDirectory.Path, "sample.json")));
    }

    [Fact]
    public async Task LoadAsync_RejectsOversizedWorkspaceDocuments()
    {
        using var directory = new TemporaryDirectory();
        var workspacePath = System.IO.Path.Combine(
            directory.Path,
            WorkspaceJsonStore.WorkspaceFileName);
        await using (var stream = new FileStream(
            workspacePath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None))
        {
            stream.SetLength(WorkspaceJsonStore.MaximumDocumentBytes + 1L);
        }

        var store = new WorkspaceJsonStore();
        var exception = await Assert.ThrowsAsync<WorkspaceFormatException>(
            () => store.LoadAsync(directory.Path, CancellationToken.None));

        Assert.Contains("exceeds", exception.Message, StringComparison.OrdinalIgnoreCase);
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
                    Authentication = new RequestAuthentication
                    {
                        Type = RequestAuthenticationType.Bearer,
                        BearerToken = "{{API_TOKEN}}",
                    },
                    Body = new ApiRequestBody("{\"name\":\"ReqMint\"}", "application/json"),
                    TimeoutSeconds = 45,
                    Assertions =
                    [
                        new RequestAssertion
                        {
                            Kind = RequestAssertionKind.StatusCodeEquals,
                            ExpectedStatusCode = 201,
                        },
                        new RequestAssertion
                        {
                            Kind = RequestAssertionKind.JsonPointerExists,
                            JsonPointer = "/id",
                        },
                    ],
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

    private static bool TryCreateFileSymbolicLink(string path, string target)
    {
        try
        {
            File.CreateSymbolicLink(path, target);
            return true;
        }
        catch (Exception exception) when (
            exception is IOException
                or UnauthorizedAccessException
                or PlatformNotSupportedException
                or NotSupportedException)
        {
            return false;
        }
    }

    private static bool TryCreateDirectorySymbolicLink(string path, string target)
    {
        try
        {
            Directory.CreateSymbolicLink(path, target);
            return true;
        }
        catch (Exception exception) when (
            exception is IOException
                or UnauthorizedAccessException
                or PlatformNotSupportedException
                or NotSupportedException)
        {
            return false;
        }
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

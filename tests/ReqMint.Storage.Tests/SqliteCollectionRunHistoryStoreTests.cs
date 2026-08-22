using Microsoft.Data.Sqlite;
using ReqMint.Core.Runner;
using ReqMint.Core.Workspaces;
using ReqMint.Storage;

namespace ReqMint.Storage.Tests;

public sealed class SqliteCollectionRunHistoryStoreTests
{
    [Fact]
    public async Task AddAndListAsync_RoundTripsSanitizedResultsNewestFirst()
    {
        using var database = new TemporaryDatabase();
        var store = new SqliteCollectionRunHistoryStore(database.Path);
        var workspaceId = Guid.NewGuid();
        var collectionId = Guid.NewGuid();
        var older = CreateEntry(
            workspaceId,
            collectionId,
            DateTimeOffset.UtcNow.AddMinutes(-1),
            "Older",
            200);
        var newer = CreateEntry(
            workspaceId,
            collectionId,
            DateTimeOffset.UtcNow,
            "Newer",
            422);

        await store.AddAsync(older);
        await store.AddAsync(newer);
        var entries = await store.ListAsync(workspaceId, collectionId);

        Assert.Collection(
            entries,
            entry => Assert.Equal("Newer", Assert.Single(entry.Requests).RequestName),
            entry => Assert.Equal("Older", Assert.Single(entry.Requests).RequestName));
        var request = Assert.Single(entries[0].Requests);
        Assert.Equal(422, request.StatusCode);
        Assert.Equal(RequestAssertionKind.StatusCodeEquals, Assert.Single(request.Assertions).Kind);
        Assert.Equal(2, entries[0].IterationCount);
    }

    [Fact]
    public async Task AddAsync_TrimsOnlyTheTargetWorkspaceAndCollection()
    {
        using var database = new TemporaryDatabase();
        var store = new SqliteCollectionRunHistoryStore(database.Path);
        var workspaceId = Guid.NewGuid();
        var collectionId = Guid.NewGuid();
        var otherCollectionId = Guid.NewGuid();

        await store.AddAsync(CreateEntry(workspaceId, collectionId, DateTimeOffset.UtcNow.AddMinutes(-2), "One", 200), 2);
        await store.AddAsync(CreateEntry(workspaceId, otherCollectionId, DateTimeOffset.UtcNow, "Other", 200), 2);
        await store.AddAsync(CreateEntry(workspaceId, collectionId, DateTimeOffset.UtcNow.AddMinutes(-1), "Two", 200), 2);
        await store.AddAsync(CreateEntry(workspaceId, collectionId, DateTimeOffset.UtcNow, "Three", 200), 2);

        var entries = await store.ListAsync(workspaceId, collectionId);

        Assert.Equal(
            ["Three", "Two"],
            entries.Select(entry => Assert.Single(entry.Requests).RequestName));
        Assert.Single(await store.ListAsync(workspaceId, otherCollectionId));
    }

    [Fact]
    public async Task ClearAsync_RemovesOnlyTheSelectedCollection()
    {
        using var database = new TemporaryDatabase();
        var store = new SqliteCollectionRunHistoryStore(database.Path);
        var workspaceId = Guid.NewGuid();
        var collectionId = Guid.NewGuid();
        var otherCollectionId = Guid.NewGuid();
        await store.AddAsync(CreateEntry(workspaceId, collectionId, DateTimeOffset.UtcNow, "Target", 200));
        await store.AddAsync(CreateEntry(workspaceId, otherCollectionId, DateTimeOffset.UtcNow, "Other", 200));

        await store.ClearAsync(workspaceId, collectionId);

        Assert.Empty(await store.ListAsync(workspaceId, collectionId));
        Assert.Single(await store.ListAsync(workspaceId, otherCollectionId));
    }

    [Fact]
    public async Task DatabaseSchema_DoesNotCreateSensitiveRequestOrResponseColumns()
    {
        using var database = new TemporaryDatabase();
        var store = new SqliteCollectionRunHistoryStore(database.Path);
        var entry = CreateEntry(
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            "Safe request",
            200);
        await store.AddAsync(entry);

        await using var connection = new SqliteConnection($"Data Source={database.Path};Pooling=False");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT sql FROM sqlite_master WHERE type = 'table' AND name = 'collection_run_history';";
        var schema = Assert.IsType<string>(await command.ExecuteScalarAsync());

        Assert.DoesNotContain("url", schema, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("header", schema, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("body", schema, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", schema, StringComparison.OrdinalIgnoreCase);
    }

    private static CollectionRunHistoryEntry CreateEntry(
        Guid workspaceId,
        Guid collectionId,
        DateTimeOffset recordedAt,
        string requestName,
        int statusCode) => new()
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            RecordedAtUtc = recordedAt,
            CollectionId = collectionId,
            CollectionName = "Commerce",
            DurationMilliseconds = 25,
            IterationCount = 2,
            Requests =
            [
                new CollectionRunHistoryRequest
                {
                    RequestId = Guid.NewGuid(),
                    RequestName = requestName,
                    IterationNumber = 1,
                    State = statusCode < 400
                        ? CollectionRequestRunState.Passed
                        : CollectionRequestRunState.Failed,
                    StatusCode = statusCode,
                    DurationMilliseconds = 12,
                    Assertions =
                    [
                        new CollectionRunHistoryAssertion(
                            RequestAssertionKind.StatusCodeEquals,
                            statusCode < 400
                                ? CollectionAssertionOutcome.Passed
                                : CollectionAssertionOutcome.Failed),
                    ],
                },
            ],
        };

    private sealed class TemporaryDatabase : IDisposable
    {
        private readonly string _directory = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "ReqMint.Tests",
            Guid.NewGuid().ToString("N"));

        public TemporaryDatabase()
        {
            Directory.CreateDirectory(_directory);
            Path = System.IO.Path.Combine(_directory, "history.db");
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }
    }
}

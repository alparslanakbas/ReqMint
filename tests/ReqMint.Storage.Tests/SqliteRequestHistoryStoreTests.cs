using ReqMint.Core.History;
using ReqMint.Core.Requests;
using ReqMint.Core.Workspaces;
using ReqMint.Storage;

namespace ReqMint.Storage.Tests;

public sealed class SqliteRequestHistoryStoreTests
{
    [Fact]
    public async Task AddAndListAsync_RoundTripsEntriesNewestFirst()
    {
        using var database = new TemporaryDatabase();
        var store = new SqliteRequestHistoryStore(database.Path);
        var workspaceId = Guid.NewGuid();
        var older = CreateEntry(workspaceId, DateTimeOffset.UtcNow.AddMinutes(-1), "Older");
        var newer = CreateEntry(workspaceId, DateTimeOffset.UtcNow, "Newer");

        await store.AddAsync(older);
        await store.AddAsync(newer);
        var entries = await store.ListAsync(workspaceId);

        Assert.Collection(
            entries,
            entry => Assert.Equal("Newer", entry.Request.Name),
            entry => Assert.Equal("Older", entry.Request.Name));
        Assert.Equal(200, entries[0].StatusCode);
        Assert.Equal(12, entries[0].DurationMilliseconds);
    }

    [Fact]
    public async Task AddAsync_TrimsOnlyTheTargetWorkspaceToItsRetentionLimit()
    {
        using var database = new TemporaryDatabase();
        var store = new SqliteRequestHistoryStore(database.Path);
        var workspaceId = Guid.NewGuid();
        var otherWorkspaceId = Guid.NewGuid();

        await store.AddAsync(CreateEntry(workspaceId, DateTimeOffset.UtcNow.AddMinutes(-2), "One"), 2);
        await store.AddAsync(CreateEntry(otherWorkspaceId, DateTimeOffset.UtcNow, "Other"), 2);
        await store.AddAsync(CreateEntry(workspaceId, DateTimeOffset.UtcNow.AddMinutes(-1), "Two"), 2);
        await store.AddAsync(CreateEntry(workspaceId, DateTimeOffset.UtcNow, "Three"), 2);

        var entries = await store.ListAsync(workspaceId);
        var otherEntries = await store.ListAsync(otherWorkspaceId);

        Assert.Equal(["Three", "Two"], entries.Select(entry => entry.Request.Name));
        Assert.Single(otherEntries);
    }

    [Fact]
    public async Task ClearAsync_RemovesOnlyTheSelectedWorkspace()
    {
        using var database = new TemporaryDatabase();
        var store = new SqliteRequestHistoryStore(database.Path);
        var workspaceId = Guid.NewGuid();
        var otherWorkspaceId = Guid.NewGuid();
        await store.AddAsync(CreateEntry(workspaceId, DateTimeOffset.UtcNow, "Target"));
        await store.AddAsync(CreateEntry(otherWorkspaceId, DateTimeOffset.UtcNow, "Other"));

        await store.ClearAsync(workspaceId);

        Assert.Empty(await store.ListAsync(workspaceId));
        Assert.Single(await store.ListAsync(otherWorkspaceId));
    }

    private static RequestHistoryEntry CreateEntry(
        Guid workspaceId,
        DateTimeOffset sentAt,
        string name) => new()
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            SentAtUtc = sentAt,
            Outcome = "completed",
            StatusCode = 200,
            ReasonPhrase = "OK",
            DurationMilliseconds = 12,
            ContentType = "application/json",
            ResponseBytes = 2,
            Request = new RequestDocument
            {
                Id = Guid.NewGuid(),
                Name = name,
                Method = "GET",
                Url = "https://api.example.com/items",
                Headers = [new RequestField("Accept", "application/json")],
            },
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

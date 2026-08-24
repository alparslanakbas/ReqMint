using System.Text.Json;
using ReqMint.App.Services;
using ReqMint.Storage;

namespace ReqMint.App.Tests;

public sealed class LoopbackTutorialSessionServiceTests
{
    [Fact]
    public async Task StartAsync_CreatesDisposableWorkspaceAndServesOnlyTheLocalTutorial()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "ReqMint.Tests",
            Guid.NewGuid().ToString("N"));
        var store = new WorkspaceJsonStore();
        string workspaceDirectory;
        using (var service = new LoopbackTutorialSessionService(store, root))
        {
            var session = await service.StartAsync();
            workspaceDirectory = session.WorkspaceDirectory;
            var loaded = await store.LoadAsync(workspaceDirectory);

            Assert.Equal("ReqMint Tutorial", loaded.Workspace.Name);
            Assert.Empty(Assert.Single(loaded.Collections).Requests);
            var variable = Assert.Single(Assert.Single(loaded.Environments).Variables);
            Assert.Equal("TUTORIAL_BASE_URL", variable.Name);
            Assert.Equal(session.BaseUri.GetLeftPart(UriPartial.Authority), variable.Value);
            Assert.Equal("{{TUTORIAL_BASE_URL}}/api/hello", session.DraftRequest.Url);

            using var client = new HttpClient { BaseAddress = session.BaseUri };
            using var response = await client.GetAsync("api/hello");
            var body = await response.Content.ReadAsStringAsync();
            using var json = JsonDocument.Parse(body);

            Assert.True(response.IsSuccessStatusCode);
            Assert.Equal("Hello from ReqMint", json.RootElement.GetProperty("message").GetString());
            Assert.Equal("local-tutorial", json.RootElement.GetProperty("source").GetString());

            using var missingResponse = await client.GetAsync("api/missing");
            Assert.Equal(System.Net.HttpStatusCode.NotFound, missingResponse.StatusCode);
        }

        Assert.False(Directory.Exists(workspaceDirectory));
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}

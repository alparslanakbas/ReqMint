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

            Assert.Equal("ReqMint Local Demo", loaded.Workspace.Name);
            var demoRequests = Assert.Single(loaded.Collections).Requests;
            Assert.Collection(
                demoRequests,
                request => Assert.Equal("Check service health", request.Name),
                request => Assert.Equal("List active API projects", request.Name),
                request => Assert.Equal("Inspect current release", request.Name));
            Assert.All(
                demoRequests,
                request => Assert.StartsWith("{{TUTORIAL_BASE_URL}}/api/", request.Url, StringComparison.Ordinal));
            Assert.All(demoRequests, request => Assert.NotEmpty(request.Assertions));
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

            using var healthResponse = await client.GetAsync("api/health");
            using var healthJson = JsonDocument.Parse(await healthResponse.Content.ReadAsStringAsync());
            Assert.True(healthResponse.IsSuccessStatusCode);
            Assert.Equal("healthy", healthJson.RootElement.GetProperty("status").GetString());

            using var projectsResponse = await client.GetAsync("api/projects?status=active");
            using var projectsJson = JsonDocument.Parse(await projectsResponse.Content.ReadAsStringAsync());
            Assert.True(projectsResponse.IsSuccessStatusCode);
            Assert.Equal(3, projectsJson.RootElement.GetProperty("data").GetArrayLength());

            using var releaseResponse = await client.GetAsync("api/releases/current");
            using var releaseJson = JsonDocument.Parse(await releaseResponse.Content.ReadAsStringAsync());
            Assert.True(releaseResponse.IsSuccessStatusCode);
            Assert.Equal(
                "Community Preview",
                releaseJson.RootElement.GetProperty("channel").GetString());

            using var missingResponse = await client.GetAsync("api/missing");
            Assert.Equal(System.Net.HttpStatusCode.NotFound, missingResponse.StatusCode);
        }

        Assert.False(Directory.Exists(workspaceDirectory));
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task StartAsync_LocalizesDemoWorkspaceFromCurrentApplicationLanguage()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "ReqMint.Tests",
            Guid.NewGuid().ToString("N"));
        var store = new WorkspaceJsonStore();

        using (var service = new LoopbackTutorialSessionService(store, root, () => "tr"))
        {
            var session = await service.StartAsync();
            var loaded = await store.LoadAsync(session.WorkspaceDirectory);

            Assert.Equal("ReqMint Yerel Demo", loaded.Workspace.Name);
            Assert.Equal("Başlangıç", Assert.Single(loaded.Collections).Name);
            Assert.Equal("Yerel Demo", Assert.Single(loaded.Environments).Name);
            Assert.Equal("ReqMint'e merhaba de", session.DraftRequest.Name);
            Assert.Collection(
                Assert.Single(loaded.Collections).Requests,
                request => Assert.Equal("Servis sağlığını kontrol et", request.Name),
                request => Assert.Equal("Aktif projeleri listele", request.Name),
                request => Assert.Equal("Güncel sürümü incele", request.Name));
        }

        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task StartAsync_DemoWorkspaceContainsNoPersistedSecretsOrThirdPartyUrls()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "ReqMint.Tests",
            Guid.NewGuid().ToString("N"));
        var store = new WorkspaceJsonStore();

        using (var service = new LoopbackTutorialSessionService(store, root))
        {
            var session = await service.StartAsync();
            var loaded = await store.LoadAsync(session.WorkspaceDirectory);

            Assert.All(
                loaded.Collections.SelectMany(collection => collection.Requests),
                request => Assert.StartsWith(
                    "{{TUTORIAL_BASE_URL}}/",
                    request.Url,
                    StringComparison.Ordinal));
            Assert.All(
                loaded.Environments.SelectMany(environment => environment.Variables),
                variable =>
                {
                    Assert.False(variable.IsSecret);
                    Assert.DoesNotContain("https://", variable.Value ?? string.Empty, StringComparison.OrdinalIgnoreCase);
                });
        }

        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}

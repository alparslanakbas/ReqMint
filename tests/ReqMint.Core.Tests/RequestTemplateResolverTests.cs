using ReqMint.Core.Requests;
using ReqMint.Core.Security;
using ReqMint.Core.Templates;
using ReqMint.Core.Workspaces;

namespace ReqMint.Core.Tests;

public sealed class RequestTemplateResolverTests
{
    [Fact]
    public async Task ResolveAsync_ResolvesUrlFieldsBodyAndSecretValues()
    {
        var workspaceId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var environment = new EnvironmentDocument
        {
            Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Name = "Development",
            Variables =
            [
                new EnvironmentVariable("BASE_URL", "https://api.example.com"),
                new EnvironmentVariable("VERSION", "v2"),
                new EnvironmentVariable("TOKEN", null, IsSecret: true),
            ],
        };
        var request = CreateRequest() with
        {
            Url = "{{BASE_URL}}/{{VERSION}}/orders",
            QueryParameters = [new RequestField("tenant", "{{VERSION}}")],
            Headers = [new RequestField("Authorization", "Bearer {{TOKEN}}")],
            Body = new ApiRequestBody("{\"source\":\"{{VERSION}}\"}", "application/json"),
        };
        var vault = new StubSecretVault("secret-token");
        var resolver = new RequestTemplateResolver(vault);

        var resolved = await resolver.ResolveAsync(workspaceId, environment, request);

        Assert.Equal("https://api.example.com/v2/orders", resolved.Url.AbsoluteUri);
        Assert.Equal(new RequestField("tenant", "v2"), resolved.QueryParameters[0]);
        Assert.Equal(new RequestField("Authorization", "Bearer secret-token"), resolved.Headers[0]);
        Assert.Equal("{\"source\":\"v2\"}", resolved.Body?.Content);
        Assert.Equal("TOKEN", Assert.Single(vault.ReadReferences).VariableName);
    }

    [Fact]
    public async Task ResolveAsync_ReportsAllMissingVariablesWithoutReadingUnknownSecrets()
    {
        var request = CreateRequest() with
        {
            Url = "{{BASE_URL}}/orders/{{ORDER_ID}}",
            Headers = [new RequestField("X-Region", "{{REGION}}")],
        };
        var resolver = new RequestTemplateResolver(new StubSecretVault(null));

        var exception = await Assert.ThrowsAsync<RequestTemplateResolutionException>(
            () => resolver.ResolveAsync(Guid.NewGuid(), environment: null, request));

        Assert.Equal(["BASE_URL", "ORDER_ID", "REGION"], exception.MissingVariables);
    }

    [Fact]
    public async Task ResolveAsync_AllowsRequestsWithoutTemplatesOrEnvironment()
    {
        var resolver = new RequestTemplateResolver(new StubSecretVault(null));

        var resolved = await resolver.ResolveAsync(
            Guid.NewGuid(),
            environment: null,
            CreateRequest());

        Assert.Equal("https://api.example.com/orders", resolved.Url.AbsoluteUri);
        Assert.Empty(resolved.QueryParameters);
    }

    private static RequestDocument CreateRequest() => new()
    {
        Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
        Name = "Orders",
        Method = "GET",
        Url = "https://api.example.com/orders",
    };

    private sealed class StubSecretVault(string? value) : ISecretVault
    {
        public List<SecretReference> ReadReferences { get; } = [];

        public Task<string?> GetAsync(
            SecretReference reference,
            CancellationToken cancellationToken = default)
        {
            ReadReferences.Add(reference);
            return Task.FromResult(value);
        }

        public Task SetAsync(
            SecretReference reference,
            string value,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task DeleteAsync(
            SecretReference reference,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}

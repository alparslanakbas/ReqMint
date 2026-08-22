using ReqMint.Core.Requests;
using ReqMint.Core.Runner;
using ReqMint.Core.Security;
using ReqMint.Core.Templates;
using ReqMint.Core.Workspaces;

namespace ReqMint.Core.Tests;

public sealed class CollectionRunnerTests
{
    [Fact]
    public async Task RunAsync_ExecutesInOrderWithEnvironmentResolutionAndSafeResults()
    {
        const string secret = "must-not-appear-in-run-results";
        var workspaceId = Guid.NewGuid();
        var environment = new EnvironmentDocument
        {
            Id = Guid.NewGuid(),
            Name = "Development",
            Variables =
            [
                new EnvironmentVariable("BASE_URL", "https://api.example.com"),
                new EnvironmentVariable("TOKEN", null, IsSecret: true),
            ],
        };
        var collection = CreateCollection(
            CreateRequest("First", "{{BASE_URL}}/first") with
            {
                Headers = [new RequestField("Authorization", "Bearer {{TOKEN}}")],
            },
            CreateRequest("Second", "{{BASE_URL}}/second"));
        var executor = new RecordingExecutor((_, index, _) => Task.FromResult(
            Response(index == 0 ? 200 : 204)));
        var progress = new InlineProgress();
        var runner = new CollectionRunner(
            executor,
            new RequestTemplateResolver(new StubSecretVault(secret)));

        var result = await runner.RunAsync(
            new CollectionRunDefinition
            {
                WorkspaceId = workspaceId,
                Collection = collection,
                Environment = environment,
            },
            progress);

        Assert.Equal(
            ["https://api.example.com/first", "https://api.example.com/second"],
            executor.Requests.Select(request => request.Url.AbsoluteUri.TrimEnd('/')));
        Assert.Equal("Bearer " + secret, executor.Requests[0].Headers[0].Value);
        Assert.Equal(
            [CollectionRequestRunState.Passed, CollectionRequestRunState.Passed],
            result.Results.Select(item => item.State));
        Assert.Equal(2, result.PassedCount);
        Assert.Equal(2, result.CompletedCount);
        Assert.False(result.WasCancelled);
        Assert.Equal([1, 2], progress.Items.Select(item => item.CompletedRequestCount));
        Assert.DoesNotContain(secret, result.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(secret, string.Join('|', result.Results), StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_ContinuesAfterARequestCannotResolveVariables()
    {
        var collection = CreateCollection(
            CreateRequest("Missing", "{{UNKNOWN_URL}}/first"),
            CreateRequest("Healthy", "https://api.example.com/second"));
        var executor = new RecordingExecutor((_, _, _) => Task.FromResult(Response(200)));
        var runner = new CollectionRunner(
            executor,
            new RequestTemplateResolver(new StubSecretVault(null)));

        var result = await runner.RunAsync(new CollectionRunDefinition
        {
            WorkspaceId = Guid.NewGuid(),
            Collection = collection,
        });

        Assert.Equal(
            [CollectionRequestRunState.Error, CollectionRequestRunState.Passed],
            result.Results.Select(item => item.State));
        Assert.Equal(CollectionRunErrorKind.MissingVariables, result.Results[0].ErrorKind);
        Assert.Single(executor.Requests);
        Assert.Equal(1, result.PassedCount);
        Assert.Equal(1, result.FailedCount);
    }

    [Fact]
    public async Task RunAsync_ReturnsPartialResultWhenCancelledDuringARequest()
    {
        var collection = CreateCollection(
            CreateRequest("First", "https://api.example.com/first"),
            CreateRequest("Second", "https://api.example.com/second"),
            CreateRequest("Third", "https://api.example.com/third"));
        var secondStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var executor = new RecordingExecutor(async (_, index, cancellationToken) =>
        {
            if (index == 1)
            {
                secondStarted.SetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }

            return Response(200);
        });
        var runner = new CollectionRunner(
            executor,
            new RequestTemplateResolver(new StubSecretVault(null)));
        using var cancellationSource = new CancellationTokenSource();

        var running = runner.RunAsync(
            new CollectionRunDefinition
            {
                WorkspaceId = Guid.NewGuid(),
                Collection = collection,
            },
            cancellationToken: cancellationSource.Token);
        await secondStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await cancellationSource.CancelAsync();
        var result = await running;

        Assert.True(result.WasCancelled);
        Assert.Equal(2, executor.Requests.Count);
        Assert.Equal(
            [
                CollectionRequestRunState.Passed,
                CollectionRequestRunState.Cancelled,
                CollectionRequestRunState.NotRun,
            ],
            result.Results.Select(item => item.State));
        Assert.Equal(1, result.CompletedCount);
    }

    [Fact]
    public async Task RunAsync_StopOnFailureLeavesRemainingRequestsUnexecuted()
    {
        var collection = CreateCollection(
            CreateRequest("Failure", "https://api.example.com/failure"),
            CreateRequest("Skipped", "https://api.example.com/skipped"));
        var executor = new RecordingExecutor((_, _, _) => Task.FromResult(Response(500)));
        var runner = new CollectionRunner(
            executor,
            new RequestTemplateResolver(new StubSecretVault(null)));

        var result = await runner.RunAsync(new CollectionRunDefinition
        {
            WorkspaceId = Guid.NewGuid(),
            Collection = collection,
            StopOnFailure = true,
        });

        Assert.Single(executor.Requests);
        Assert.Equal(CollectionRequestRunState.Failed, result.Results[0].State);
        Assert.Equal(CollectionRequestRunState.NotRun, result.Results[1].State);
        Assert.Equal(1, result.FailedCount);
        Assert.Equal(1, result.CompletedCount);
    }

    [Fact]
    public async Task RunAsync_UsesAssertionsAsTheRequestOutcomeWithoutRetainingBodyValues()
    {
        const string sensitiveResponseValue = "response-value-must-not-be-retained";
        var request = CreateRequest("Expected missing order", "https://api.example.com/orders/42") with
        {
            Assertions =
            [
                new RequestAssertion
                {
                    Kind = RequestAssertionKind.StatusCodeEquals,
                    ExpectedStatusCode = 404,
                },
                new RequestAssertion
                {
                    Kind = RequestAssertionKind.MaximumDuration,
                    MaximumDurationMilliseconds = 100,
                },
                new RequestAssertion
                {
                    Kind = RequestAssertionKind.JsonPointerExists,
                    JsonPointer = "/error/details/0/code",
                },
            ],
        };
        var executor = new RecordingExecutor((_, _, _) => Task.FromResult(Response(
            404,
            $"{{\"error\":{{\"details\":[{{\"code\":\"{sensitiveResponseValue}\"}}]}}}}",
            TimeSpan.FromMilliseconds(25))));
        var runner = new CollectionRunner(
            executor,
            new RequestTemplateResolver(new StubSecretVault(null)));

        var result = await runner.RunAsync(new CollectionRunDefinition
        {
            WorkspaceId = Guid.NewGuid(),
            Collection = CreateCollection(request),
        });

        var requestResult = Assert.Single(result.Results);
        Assert.Equal(CollectionRequestRunState.Passed, requestResult.State);
        Assert.All(
            requestResult.Assertions,
            assertion => Assert.Equal(CollectionAssertionOutcome.Passed, assertion.Outcome));
        Assert.DoesNotContain(sensitiveResponseValue, requestResult.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(sensitiveResponseValue, result.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_FailsJsonAssertionsClosedForTruncatedBodies()
    {
        var request = CreateRequest("Large response", "https://api.example.com/large") with
        {
            Assertions =
            [
                new RequestAssertion
                {
                    Kind = RequestAssertionKind.JsonPointerExists,
                    JsonPointer = "/data/id",
                },
            ],
        };
        var executor = new RecordingExecutor((_, _, _) => Task.FromResult(Response(
            200,
            "{\"data\":{\"id\":42}}",
            TimeSpan.FromMilliseconds(10),
            isBodyTruncated: true)));
        var runner = new CollectionRunner(
            executor,
            new RequestTemplateResolver(new StubSecretVault(null)));

        var result = await runner.RunAsync(new CollectionRunDefinition
        {
            WorkspaceId = Guid.NewGuid(),
            Collection = CreateCollection(request),
        });

        var requestResult = Assert.Single(result.Results);
        Assert.Equal(CollectionRequestRunState.Failed, requestResult.State);
        Assert.Equal(
            CollectionAssertionOutcome.UnableToEvaluate,
            Assert.Single(requestResult.Assertions).Outcome);
    }

    [Fact]
    public async Task RunAsync_ExecutesEveryDataRowWithIterationValuesTakingPrecedence()
    {
        var environment = new EnvironmentDocument
        {
            Id = Guid.NewGuid(),
            Name = "Development",
            Variables =
            [
                new EnvironmentVariable("BASE_URL", "https://environment.example.com"),
                new EnvironmentVariable("shared", "environment"),
            ],
        };
        var collection = CreateCollection(
            CreateRequest("Order", "{{BASE_URL}}/orders/{{orderId}}?shared={{shared}}"),
            CreateRequest("Audit", "{{BASE_URL}}/audit/{{orderId}}"));
        var executor = new RecordingExecutor((_, _, _) => Task.FromResult(Response(200)));
        var progress = new InlineProgress();
        var runner = new CollectionRunner(
            executor,
            new RequestTemplateResolver(new StubSecretVault(null)));

        var result = await runner.RunAsync(
            new CollectionRunDefinition
            {
                WorkspaceId = Guid.NewGuid(),
                Collection = collection,
                Environment = environment,
                DataRows =
                [
                    DataRow(("orderId", "A-1"), ("shared", "row-one")),
                    DataRow(
                        ("orderId", "A-2"),
                        ("BASE_URL", "https://iteration.example.com"),
                        ("shared", "row-two")),
                ],
            },
            progress);

        Assert.Equal(2, result.IterationCount);
        Assert.Equal(4, result.Results.Count);
        Assert.Equal([1, 1, 2, 2], result.Results.Select(item => item.IterationNumber));
        Assert.Equal(
            [
                "https://environment.example.com/orders/A-1?shared=row-one",
                "https://environment.example.com/audit/A-1",
                "https://iteration.example.com/orders/A-2?shared=row-two",
                "https://iteration.example.com/audit/A-2",
            ],
            executor.Requests.Select(request => request.Url.AbsoluteUri.TrimEnd('/')));
        Assert.Equal([4, 4, 4, 4], progress.Items.Select(item => item.TotalRequestCount));
        Assert.DoesNotContain("row-one", result.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("row-two", result.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_RejectsRunsAboveTheTotalExecutionLimit()
    {
        var requests = Enumerable.Range(0, 51)
            .Select(index => CreateRequest($"Request {index}", $"https://api.example.com/{index}"))
            .ToArray();
        var rows = Enumerable.Range(0, 100)
            .Select(index => DataRow(("id", index.ToString())))
            .ToArray();
        var runner = new CollectionRunner(
            new RecordingExecutor((_, _, _) => Task.FromResult(Response(200))),
            new RequestTemplateResolver(new StubSecretVault(null)));

        await Assert.ThrowsAsync<ArgumentException>(() => runner.RunAsync(
            new CollectionRunDefinition
            {
                WorkspaceId = Guid.NewGuid(),
                Collection = CreateCollection(requests),
                DataRows = rows,
            }));
    }

    [Fact]
    public async Task RunAsync_RerunsOnlySelectedExecutionsInOriginalOrder()
    {
        var first = CreateRequest("First", "https://api.example.com/{{id}}/first");
        var second = CreateRequest("Second", "https://api.example.com/{{id}}/second");
        var executor = new RecordingExecutor((_, _, _) => Task.FromResult(Response(200)));
        var runner = new CollectionRunner(
            executor,
            new RequestTemplateResolver(new StubSecretVault(null)));

        var result = await runner.RunAsync(new CollectionRunDefinition
        {
            WorkspaceId = Guid.NewGuid(),
            Collection = CreateCollection(first, second),
            DataRows =
            [
                DataRow(("id", "row-1")),
                DataRow(("id", "row-2")),
            ],
            ExecutionSelection =
            [
                new CollectionRunExecutionKey(first.Id, 2),
                new CollectionRunExecutionKey(second.Id, 1),
            ],
        });

        Assert.True(result.WasRerun);
        Assert.True(result.UsedDataFile);
        Assert.Equal(2, result.IterationCount);
        Assert.Equal(
            [
                "https://api.example.com/row-1/second",
                "https://api.example.com/row-2/first",
            ],
            executor.Requests.Select(request => request.Url.AbsoluteUri.TrimEnd('/')));
        Assert.Equal(
            [(second.Id, 1), (first.Id, 2)],
            result.Results.Select(item => (item.RequestId, item.IterationNumber)));
    }

    [Fact]
    public async Task RunAsync_RejectsDuplicateRerunSelections()
    {
        var request = CreateRequest("First", "https://api.example.com/first");
        var key = new CollectionRunExecutionKey(request.Id, 1);
        var runner = new CollectionRunner(
            new RecordingExecutor((_, _, _) => Task.FromResult(Response(200))),
            new RequestTemplateResolver(new StubSecretVault(null)));

        await Assert.ThrowsAsync<ArgumentException>(() => runner.RunAsync(
            new CollectionRunDefinition
            {
                WorkspaceId = Guid.NewGuid(),
                Collection = CreateCollection(request),
                ExecutionSelection = [key, key],
            }));
    }

    private static CollectionDocument CreateCollection(params RequestDocument[] requests) => new()
    {
        Id = Guid.NewGuid(),
        Name = "Commerce",
        Requests = requests,
    };

    private static RequestDocument CreateRequest(string name, string url) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Method = "GET",
        Url = url,
    };

    private static CollectionRunDataRow DataRow(
        params (string Name, string Value)[] variables) => new()
        {
            Variables = variables.ToDictionary(
                variable => variable.Name,
                variable => variable.Value,
                StringComparer.OrdinalIgnoreCase),
        };

    private static ApiResponse Response(
        int statusCode,
        string body = "",
        TimeSpan? duration = null,
        bool isBodyTruncated = false) => new(
        statusCode,
        statusCode is >= 200 and <= 299 ? "OK" : "Failure",
        new Dictionary<string, IReadOnlyList<string>>(),
        body,
        "application/json",
        duration ?? TimeSpan.FromMilliseconds(10),
        isBodyTruncated);

    private sealed class RecordingExecutor(
        Func<ApiRequest, int, CancellationToken, Task<ApiResponse>> handler) : IRequestExecutor
    {
        public List<ApiRequest> Requests { get; } = [];

        public Task<ApiResponse> ExecuteAsync(
            ApiRequest request,
            CancellationToken cancellationToken = default)
        {
            var index = Requests.Count;
            Requests.Add(request);
            return handler(request, index, cancellationToken);
        }
    }

    private sealed class InlineProgress : IProgress<CollectionRunProgress>
    {
        public List<CollectionRunProgress> Items { get; } = [];

        public void Report(CollectionRunProgress value) => Items.Add(value);
    }

    private sealed class StubSecretVault(string? value) : ISecretVault
    {
        public Task<string?> GetAsync(
            SecretReference reference,
            CancellationToken cancellationToken = default) => Task.FromResult(value);

        public Task SetAsync(
            SecretReference reference,
            string value,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task DeleteAsync(
            SecretReference reference,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}

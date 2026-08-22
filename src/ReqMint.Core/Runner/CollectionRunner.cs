using System.Diagnostics;
using ReqMint.Core.Requests;
using ReqMint.Core.Templates;
using ReqMint.Core.Workspaces;

namespace ReqMint.Core.Runner;

public interface ICollectionRunner
{
    Task<CollectionRunResult> RunAsync(
        CollectionRunDefinition definition,
        IProgress<CollectionRunProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

public sealed class CollectionRunner(
    IRequestExecutor requestExecutor,
    RequestTemplateResolver templateResolver) : ICollectionRunner
{
    public const int MaximumRequestCount = 1000;

    public async Task<CollectionRunResult> RunAsync(
        CollectionRunDefinition definition,
        IProgress<CollectionRunProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        Validate(definition);

        var runTimer = Stopwatch.StartNew();
        var results = new List<CollectionRequestRunResult>(definition.Collection.Requests.Count);
        for (var index = 0; index < definition.Collection.Requests.Count; index++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                AddNotRunResults(definition.Collection.Requests, index, results);
                return CreateResult(definition, results, runTimer.Elapsed, wasCancelled: true);
            }

            var request = definition.Collection.Requests[index];
            var requestTimer = Stopwatch.StartNew();
            CollectionRequestRunResult result;
            try
            {
                var resolvedRequest = await templateResolver.ResolveAsync(
                    definition.WorkspaceId,
                    definition.Environment,
                    request,
                    cancellationToken);
                var response = await requestExecutor.ExecuteAsync(
                    resolvedRequest,
                    cancellationToken);
                result = new CollectionRequestRunResult
                {
                    RequestId = request.Id,
                    RequestName = request.Name,
                    State = response.IsSuccessStatusCode
                        ? CollectionRequestRunState.Passed
                        : CollectionRequestRunState.Failed,
                    StatusCode = response.StatusCode,
                    Duration = response.Duration,
                };
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                result = ErrorResult(
                    request,
                    CollectionRequestRunState.Cancelled,
                    CollectionRunErrorKind.None,
                    requestTimer.Elapsed);
                results.Add(result);
                progress?.Report(new CollectionRunProgress(
                    index + 1,
                    definition.Collection.Requests.Count,
                    result));
                AddNotRunResults(definition.Collection.Requests, index + 1, results);
                return CreateResult(definition, results, runTimer.Elapsed, wasCancelled: true);
            }
            catch (RequestTemplateResolutionException)
            {
                result = ErrorResult(
                    request,
                    CollectionRequestRunState.Error,
                    CollectionRunErrorKind.MissingVariables,
                    requestTimer.Elapsed);
            }
            catch (TimeoutException)
            {
                result = ErrorResult(
                    request,
                    CollectionRequestRunState.Error,
                    CollectionRunErrorKind.Timeout,
                    requestTimer.Elapsed);
            }
            catch (HttpRequestException)
            {
                result = ErrorResult(
                    request,
                    CollectionRequestRunState.Error,
                    CollectionRunErrorKind.Transport,
                    requestTimer.Elapsed);
            }
            catch (Exception exception) when (IsInvalidRequestException(exception))
            {
                result = ErrorResult(
                    request,
                    CollectionRequestRunState.Error,
                    CollectionRunErrorKind.InvalidRequest,
                    requestTimer.Elapsed);
            }

            results.Add(result);
            progress?.Report(new CollectionRunProgress(
                index + 1,
                definition.Collection.Requests.Count,
                result));

            if (definition.StopOnFailure && result.State is
                CollectionRequestRunState.Failed or CollectionRequestRunState.Error)
            {
                AddNotRunResults(definition.Collection.Requests, index + 1, results);
                break;
            }
        }

        return CreateResult(definition, results, runTimer.Elapsed, wasCancelled: false);
    }

    private static void Validate(CollectionRunDefinition definition)
    {
        if (definition.WorkspaceId == Guid.Empty)
        {
            throw new ArgumentException("A workspace identifier is required.", nameof(definition));
        }

        if (definition.Collection.Requests.Count > MaximumRequestCount)
        {
            throw new ArgumentException(
                $"A collection run is limited to {MaximumRequestCount} requests.",
                nameof(definition));
        }

        if (definition.Collection.Requests
            .GroupBy(request => request.Id)
            .Any(group => group.Key == Guid.Empty || group.Count() > 1))
        {
            throw new ArgumentException(
                "Every collection request requires a unique identifier.",
                nameof(definition));
        }
    }

    private static bool IsInvalidRequestException(Exception exception) => exception is
        ArgumentException or InvalidOperationException or NotSupportedException or FormatException;

    private static CollectionRequestRunResult ErrorResult(
        RequestDocument request,
        CollectionRequestRunState state,
        CollectionRunErrorKind errorKind,
        TimeSpan duration) => new()
        {
            RequestId = request.Id,
            RequestName = request.Name,
            State = state,
            ErrorKind = errorKind,
            Duration = duration,
        };

    private static void AddNotRunResults(
        IReadOnlyList<RequestDocument> requests,
        int startIndex,
        ICollection<CollectionRequestRunResult> results)
    {
        for (var index = startIndex; index < requests.Count; index++)
        {
            results.Add(ErrorResult(
                requests[index],
                CollectionRequestRunState.NotRun,
                CollectionRunErrorKind.None,
                TimeSpan.Zero));
        }
    }

    private static CollectionRunResult CreateResult(
        CollectionRunDefinition definition,
        IReadOnlyList<CollectionRequestRunResult> results,
        TimeSpan duration,
        bool wasCancelled) => new()
        {
            CollectionId = definition.Collection.Id,
            CollectionName = definition.Collection.Name,
            EnvironmentId = definition.Environment?.Id,
            Results = results,
            Duration = duration,
            WasCancelled = wasCancelled,
        };
}

public sealed record CollectionRunDefinition
{
    public required Guid WorkspaceId { get; init; }

    public required CollectionDocument Collection { get; init; }

    public EnvironmentDocument? Environment { get; init; }

    public bool StopOnFailure { get; init; }
}

public sealed record CollectionRunResult
{
    public required Guid CollectionId { get; init; }

    public required string CollectionName { get; init; }

    public Guid? EnvironmentId { get; init; }

    public IReadOnlyList<CollectionRequestRunResult> Results { get; init; } = [];

    public TimeSpan Duration { get; init; }

    public bool WasCancelled { get; init; }

    public int PassedCount => Results.Count(result => result.State == CollectionRequestRunState.Passed);

    public int FailedCount => Results.Count(result => result.State is
        CollectionRequestRunState.Failed or CollectionRequestRunState.Error);

    public int CompletedCount => PassedCount + FailedCount;
}

public sealed record CollectionRequestRunResult
{
    public required Guid RequestId { get; init; }

    public required string RequestName { get; init; }

    public CollectionRequestRunState State { get; init; } = CollectionRequestRunState.NotRun;

    public int? StatusCode { get; init; }

    public TimeSpan Duration { get; init; }

    public CollectionRunErrorKind ErrorKind { get; init; }
}

public sealed record CollectionRunProgress(
    int CompletedRequestCount,
    int TotalRequestCount,
    CollectionRequestRunResult LatestResult);

public enum CollectionRequestRunState
{
    NotRun,
    Passed,
    Failed,
    Error,
    Cancelled,
}

public enum CollectionRunErrorKind
{
    None,
    MissingVariables,
    Timeout,
    Transport,
    InvalidRequest,
}

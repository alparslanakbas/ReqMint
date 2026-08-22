namespace ReqMint.Core.Runner;

public interface ICollectionRunHistoryStore
{
    Task AddAsync(
        CollectionRunHistoryEntry entry,
        int retentionLimit = 50,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CollectionRunHistoryEntry>> ListAsync(
        Guid workspaceId,
        Guid collectionId,
        int take = 50,
        CancellationToken cancellationToken = default);

    Task ClearAsync(
        Guid workspaceId,
        Guid collectionId,
        CancellationToken cancellationToken = default);
}

public sealed record CollectionRunHistoryEntry
{
    public required Guid Id { get; init; }

    public required Guid WorkspaceId { get; init; }

    public required DateTimeOffset RecordedAtUtc { get; init; }

    public required Guid CollectionId { get; init; }

    public required string CollectionName { get; init; }

    public Guid? EnvironmentId { get; init; }

    public double DurationMilliseconds { get; init; }

    public bool WasCancelled { get; init; }

    public int IterationCount { get; init; } = 1;

    public IReadOnlyList<CollectionRunHistoryRequest> Requests { get; init; } = [];

    public int PassedCount => Requests.Count(request =>
        request.State == CollectionRequestRunState.Passed);

    public int FailedCount => Requests.Count(request => request.State is
        CollectionRequestRunState.Failed or CollectionRequestRunState.Error);

    public static CollectionRunHistoryEntry Create(
        Guid workspaceId,
        CollectionRunResult result,
        DateTimeOffset recordedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(result);
        var entry = new CollectionRunHistoryEntry
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            RecordedAtUtc = recordedAtUtc.ToUniversalTime(),
            CollectionId = result.CollectionId,
            CollectionName = result.CollectionName,
            EnvironmentId = result.EnvironmentId,
            DurationMilliseconds = Math.Max(0, result.Duration.TotalMilliseconds),
            WasCancelled = result.WasCancelled,
            IterationCount = result.IterationCount,
            Requests = result.Results.Select(request => new CollectionRunHistoryRequest
            {
                RequestId = request.RequestId,
                RequestName = request.RequestName,
                IterationNumber = request.IterationNumber,
                State = request.State,
                StatusCode = request.StatusCode,
                DurationMilliseconds = Math.Max(0, request.Duration.TotalMilliseconds),
                ErrorKind = request.ErrorKind,
                Assertions = request.Assertions.Select(assertion =>
                    new CollectionRunHistoryAssertion(assertion.Kind, assertion.Outcome)).ToArray(),
            }).ToArray(),
        };
        CollectionRunHistoryValidator.Validate(entry);
        return entry;
    }

    public CollectionRunResult ToRunResult()
    {
        CollectionRunHistoryValidator.Validate(this);
        return new CollectionRunResult
        {
            CollectionId = CollectionId,
            CollectionName = CollectionName,
            EnvironmentId = EnvironmentId,
            Duration = TimeSpan.FromMilliseconds(DurationMilliseconds),
            WasCancelled = WasCancelled,
            IterationCount = IterationCount,
            Results = Requests.Select(request => new CollectionRequestRunResult
            {
                RequestId = request.RequestId,
                RequestName = request.RequestName,
                IterationNumber = request.IterationNumber,
                State = request.State,
                StatusCode = request.StatusCode,
                Duration = TimeSpan.FromMilliseconds(request.DurationMilliseconds),
                ErrorKind = request.ErrorKind,
                Assertions = request.Assertions.Select(assertion => new CollectionAssertionResult(
                    assertion.Kind,
                    assertion.Outcome)).ToArray(),
            }).ToArray(),
        };
    }
}

public sealed record CollectionRunHistoryRequest
{
    public required Guid RequestId { get; init; }

    public required string RequestName { get; init; }

    public int IterationNumber { get; init; } = 1;

    public CollectionRequestRunState State { get; init; }

    public int? StatusCode { get; init; }

    public double DurationMilliseconds { get; init; }

    public CollectionRunErrorKind ErrorKind { get; init; }

    public IReadOnlyList<CollectionRunHistoryAssertion> Assertions { get; init; } = [];
}

public sealed record CollectionRunHistoryAssertion(
    Workspaces.RequestAssertionKind Kind,
    CollectionAssertionOutcome Outcome);

public static class CollectionRunHistoryValidator
{
    private const int MaximumNameLength = 256;
    private static readonly TimeSpan MaximumDuration = TimeSpan.FromDays(7);

    public static void Validate(CollectionRunHistoryEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (entry.Id == Guid.Empty
            || entry.WorkspaceId == Guid.Empty
            || entry.CollectionId == Guid.Empty
            || entry.EnvironmentId == Guid.Empty
            || entry.RecordedAtUtc == default)
        {
            throw new ArgumentException("Collection run history identifiers are invalid.", nameof(entry));
        }

        ValidateName(entry.CollectionName, nameof(entry));
        ValidateDuration(entry.DurationMilliseconds, nameof(entry));
        if (entry.IterationCount is < 1 or > CollectionRunDataParser.MaximumRowCount)
        {
            throw new ArgumentException("Collection run history iteration count is invalid.", nameof(entry));
        }

        if (entry.Requests is null
            || entry.Requests.Count > CollectionRunner.MaximumExecutionCount)
        {
            throw new ArgumentException("Collection run history contains too many results.", nameof(entry));
        }

        foreach (var request in entry.Requests)
        {
            if (request is null
                || request.RequestId == Guid.Empty
                || request.IterationNumber < 1
                || request.IterationNumber > entry.IterationCount)
            {
                throw new ArgumentException("Collection run history request identity is invalid.", nameof(entry));
            }

            ValidateName(request.RequestName, nameof(entry));
            ValidateDuration(request.DurationMilliseconds, nameof(entry));
            if (request.StatusCode is < 100 or > 599)
            {
                throw new ArgumentException("Collection run history status code is invalid.", nameof(entry));
            }

            if (request.Assertions is null
                || request.Assertions.Count > Workspaces.RequestAssertionValidator.MaximumAssertionCount)
            {
                throw new ArgumentException("Collection run history has too many assertions.", nameof(entry));
            }

            if (!Enum.IsDefined(request.State)
                || !Enum.IsDefined(request.ErrorKind)
                || request.Assertions.Any(assertion =>
                    !Enum.IsDefined(assertion.Kind) || !Enum.IsDefined(assertion.Outcome)))
            {
                throw new ArgumentException("Collection run history contains an invalid outcome.", nameof(entry));
            }
        }
    }

    private static void ValidateName(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Length > MaximumNameLength
            || value.Any(char.IsControl))
        {
            throw new ArgumentException("Collection run history names are invalid.", parameterName);
        }
    }

    private static void ValidateDuration(double milliseconds, string parameterName)
    {
        if (!double.IsFinite(milliseconds)
            || milliseconds < 0
            || milliseconds > MaximumDuration.TotalMilliseconds)
        {
            throw new ArgumentException("Collection run history duration is invalid.", parameterName);
        }
    }
}

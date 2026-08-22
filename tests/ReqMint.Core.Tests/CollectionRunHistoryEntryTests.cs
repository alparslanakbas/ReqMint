using ReqMint.Core.Runner;
using ReqMint.Core.Workspaces;

namespace ReqMint.Core.Tests;

public sealed class CollectionRunHistoryEntryTests
{
    [Fact]
    public void CreateAndToRunResult_RoundTripsOnlySanitizedResultFields()
    {
        var workspaceId = Guid.NewGuid();
        var result = new CollectionRunResult
        {
            CollectionId = Guid.NewGuid(),
            CollectionName = "Commerce",
            EnvironmentId = Guid.NewGuid(),
            Duration = TimeSpan.FromMilliseconds(42),
            IterationCount = 2,
            Results =
            [
                new CollectionRequestRunResult
                {
                    RequestId = Guid.NewGuid(),
                    RequestName = "Create order",
                    IterationNumber = 2,
                    State = CollectionRequestRunState.Failed,
                    StatusCode = 422,
                    Duration = TimeSpan.FromMilliseconds(12),
                    Assertions =
                    [
                        new CollectionAssertionResult(
                            RequestAssertionKind.StatusCodeEquals,
                            CollectionAssertionOutcome.Failed),
                    ],
                },
            ],
        };

        var entry = CollectionRunHistoryEntry.Create(
            workspaceId,
            result,
            DateTimeOffset.Parse("2026-08-22T10:00:00Z"));
        var restored = entry.ToRunResult();

        Assert.Equal(workspaceId, entry.WorkspaceId);
        Assert.Equal(result.CollectionId, restored.CollectionId);
        Assert.Equal(result.EnvironmentId, restored.EnvironmentId);
        Assert.Equal(2, restored.IterationCount);
        var request = Assert.Single(restored.Results);
        Assert.Equal(2, request.IterationNumber);
        Assert.Equal(422, request.StatusCode);
        Assert.Equal(
            CollectionAssertionOutcome.Failed,
            Assert.Single(request.Assertions).Outcome);
    }

    [Fact]
    public void Validate_RejectsUnknownEnumValues()
    {
        var entry = CreateValidEntry() with
        {
            Requests =
            [
                CreateValidEntry().Requests[0] with
                {
                    State = (CollectionRequestRunState)999,
                },
            ],
        };

        Assert.Throws<ArgumentException>(() => CollectionRunHistoryValidator.Validate(entry));
    }

    [Fact]
    public void Validate_RejectsResultsOutsideTheIterationRange()
    {
        var valid = CreateValidEntry();
        var entry = valid with
        {
            Requests =
            [
                valid.Requests[0] with { IterationNumber = 2 },
            ],
        };

        Assert.Throws<ArgumentException>(() => CollectionRunHistoryValidator.Validate(entry));
    }

    private static CollectionRunHistoryEntry CreateValidEntry() => new()
    {
        Id = Guid.NewGuid(),
        WorkspaceId = Guid.NewGuid(),
        RecordedAtUtc = DateTimeOffset.UtcNow,
        CollectionId = Guid.NewGuid(),
        CollectionName = "Commerce",
        DurationMilliseconds = 10,
        Requests =
        [
            new CollectionRunHistoryRequest
            {
                RequestId = Guid.NewGuid(),
                RequestName = "List orders",
                State = CollectionRequestRunState.Passed,
                StatusCode = 200,
                DurationMilliseconds = 10,
            },
        ],
    };
}

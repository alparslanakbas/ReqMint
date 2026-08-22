using ReqMint.Core.Workspaces;

namespace ReqMint.Core.History;

public interface IRequestHistoryStore
{
    Task AddAsync(
        RequestHistoryEntry entry,
        int retentionLimit = 200,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RequestHistoryEntry>> ListAsync(
        Guid workspaceId,
        int take = 100,
        CancellationToken cancellationToken = default);

    Task ClearAsync(Guid workspaceId, CancellationToken cancellationToken = default);
}

public sealed record RequestHistoryEntry
{
    public required Guid Id { get; init; }

    public required Guid WorkspaceId { get; init; }

    public required DateTimeOffset SentAtUtc { get; init; }

    public required RequestDocument Request { get; init; }

    public required string Outcome { get; init; }

    public int? StatusCode { get; init; }

    public string? ReasonPhrase { get; init; }

    public double? DurationMilliseconds { get; init; }

    public string? ContentType { get; init; }

    public long? ResponseBytes { get; init; }
}

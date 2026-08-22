namespace ReqMint.Core.Git;

public interface IGitService
{
    Task<GitRepositoryStatus?> GetStatusAsync(
        string workspaceDirectory,
        CancellationToken cancellationToken = default);
}

public sealed record GitRepositoryStatus
{
    public required string RepositoryRoot { get; init; }

    public required string Branch { get; init; }

    public bool IsDetached { get; init; }

    public int AheadBy { get; init; }

    public int BehindBy { get; init; }

    public IReadOnlyList<GitFileChange> Changes { get; init; } = [];

    public bool IsClean => Changes.Count == 0;
}

public sealed record GitFileChange(string Path, string Status);

public sealed class GitUnavailableException(string message, Exception? innerException = null)
    : Exception(message, innerException);

public sealed class GitCommandException(string message) : Exception(message);

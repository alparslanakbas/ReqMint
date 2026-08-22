namespace ReqMint.Core.Git;

public static class ReqMintGitFileClassifier
{
    private static readonly string[] ManagedDirectories =
    [
        "collections/",
        "environments/",
        "data/",
    ];

    public static bool IsManaged(string? repositoryRelativePath)
    {
        if (string.IsNullOrWhiteSpace(repositoryRelativePath))
        {
            return false;
        }

        var path = repositoryRelativePath.Replace('\\', '/');
        while (path.StartsWith("./", StringComparison.Ordinal))
        {
            path = path[2..];
        }

        if (path.StartsWith("/", StringComparison.Ordinal)
            || Path.IsPathRooted(path)
            || path.Contains(':'))
        {
            return false;
        }

        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0 || segments.Any(segment => segment is "." or ".."))
        {
            return false;
        }

        if (path.Equals("reqmint.workspace.json", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            && ManagedDirectories.Any(
                directory => path.StartsWith(directory, StringComparison.OrdinalIgnoreCase));
    }
}

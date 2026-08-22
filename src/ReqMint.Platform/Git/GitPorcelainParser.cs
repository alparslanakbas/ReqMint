using System.Globalization;
using ReqMint.Core.Git;

namespace ReqMint.Platform.Git;

internal static class GitPorcelainParser
{
    public static GitRepositoryStatus Parse(string repositoryRoot, string output)
    {
        var records = output.Split('\0', StringSplitOptions.RemoveEmptyEntries);
        if (records.Length == 0 || !records[0].StartsWith("## ", StringComparison.Ordinal))
        {
            throw new GitCommandException("Git returned an invalid status response.");
        }

        var (branch, detached, ahead, behind) = ParseBranch(records[0][3..]);
        var changes = new List<GitFileChange>();

        for (var index = 1; index < records.Length; index++)
        {
            var record = records[index];
            if (record.Length < 3)
            {
                continue;
            }

            var status = record[..2];
            var path = record.Length > 3 ? record[3..] : string.Empty;
            if (!string.IsNullOrEmpty(path))
            {
                changes.Add(new GitFileChange(path, status));
            }

            if ((status.Contains('R') || status.Contains('C')) && index + 1 < records.Length)
            {
                index++;
            }
        }

        return new GitRepositoryStatus
        {
            RepositoryRoot = repositoryRoot,
            Branch = branch,
            IsDetached = detached,
            AheadBy = ahead,
            BehindBy = behind,
            Changes = changes,
        };
    }

    private static (string Branch, bool Detached, int Ahead, int Behind) ParseBranch(string value)
    {
        if (value.StartsWith("HEAD (", StringComparison.Ordinal))
        {
            return ("HEAD", true, 0, 0);
        }

        const string noCommitsPrefix = "No commits yet on ";
        if (value.StartsWith(noCommitsPrefix, StringComparison.Ordinal))
        {
            return (value[noCommitsPrefix.Length..], false, 0, 0);
        }

        const string initialCommitPrefix = "Initial commit on ";
        if (value.StartsWith(initialCommitPrefix, StringComparison.Ordinal))
        {
            return (value[initialCommitPrefix.Length..], false, 0, 0);
        }

        var divergenceStart = value.LastIndexOf(" [", StringComparison.Ordinal);
        var divergence = divergenceStart >= 0 ? value[(divergenceStart + 2)..^1] : string.Empty;
        var branchAndUpstream = divergenceStart >= 0 ? value[..divergenceStart] : value;
        var upstreamStart = branchAndUpstream.IndexOf("...", StringComparison.Ordinal);
        var branch = upstreamStart >= 0 ? branchAndUpstream[..upstreamStart] : branchAndUpstream;

        return (
            branch,
            false,
            ParseDivergence(divergence, "ahead "),
            ParseDivergence(divergence, "behind "));
    }

    private static int ParseDivergence(string value, string label)
    {
        var start = value.IndexOf(label, StringComparison.Ordinal);
        if (start < 0)
        {
            return 0;
        }

        start += label.Length;
        var end = value.IndexOfAny([',', ']'], start);
        var number = end < 0 ? value[start..] : value[start..end];
        return int.TryParse(number.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var result)
            ? result
            : 0;
    }
}
